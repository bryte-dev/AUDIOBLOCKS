using AudioBlocks.App.Effects;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.Asio;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace AudioBlocks.App.Audio
{
    public enum AudioDriver
    {
        WASAPI_Shared,
        WASAPI_Exclusive,
        ASIO
    }

    internal class SilenceProvider : IWaveProvider
    {
        public WaveFormat WaveFormat { get; }
        public SilenceProvider(int sampleRate, int channels)
        { WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels); }
        public int Read(byte[] buffer, int offset, int count)
        { Array.Clear(buffer, offset, count); return count; }
    }

    public class AudioEngine
    {
        public AudioDriver Driver { get; set; } = AudioDriver.WASAPI_Shared;
        public MMDevice? InputDevice { get; set; }
        public MMDevice? OutputDevice { get; set; }
        public int SampleRate { get; set; } = 44100;
        public int BufferSize { get; set; } = 256;

        // ASIO
        private string? asioDriverName;
        private AsioOut? asio;
        private int asioInputOffset, asioOutputOffset;
        private int asioInputCount = 1, asioOutputCount = 2;

        // WASAPI
        private WasapiCapture? capture;
        private WasapiOut? playback;
        private BufferedWaveProvider? buffer;
        private WaveFormat? wasapiFormat;

        // Audio buffer
        private float[] floatBuffer = Array.Empty<float>();
        private bool floatBufferFromPool;
        private readonly ArrayPool<float> pool = ArrayPool<float>.Shared;

        // State
        public bool IsMonitoring { get; private set; }
        public float Level { get; private set; }

        // CPU
        private readonly Stopwatch processingTimer = new();
        public bool CpuOverload { get; private set; }
        public event Action<bool>? OnCpuOverloadChanged;
        public event Action<bool>? OnMonitoringChanged;

        // Log
        public event Action<string>? OnLog;

        // Effects
        public AudioEffects Effects { get; } = new();

        // ===== RECORDER =====
        public AudioRecorder Recorder { get; } = new();

        // ===== METRONOME =====
        public Metronome Metronome { get; } = new();

        // Test tone
        private volatile bool testActive;
        private volatile int testRemainingSamples;
        private double testPhase;
        private float testFrequency = 440f;
        private float testAmplitude = 0.6f;

        // EMA
        private double processingEmaMs;
        private readonly double emaAlpha = 0.2;
        public double SmoothedProcessingMs => processingEmaMs;

        public AudioEngine()
        {
            Effects.AddEffect(new GainEffect());
            Recorder.OnLog += msg => OnLog?.Invoke(msg);
        }

        private void EnsureFloatBuffer(int frames)
        {
            if (floatBuffer.Length >= frames) return;
            int newSize = Math.Max(frames, Math.Max(BufferSize * 2, floatBuffer.Length * 2));
            var newBuf = pool.Rent(newSize);
            if (floatBufferFromPool) pool.Return(floatBuffer, clearArray: true);
            floatBuffer = newBuf;
            floatBufferFromPool = true;
        }

        // =========================================================
        // CORE SIGNAL FLOW
        // =========================================================
        //
        // LIVE (recording):
        //   Input → Effects → Master → Record → Metronome → Output
        //                                         (not recorded)
        //
        // PLAYBACK:
        //   Recorded audio → (skip FX, already baked) → Metronome → Output
        //
        // =========================================================

        /// <summary>
        /// Unified processing pipeline for both WASAPI and ASIO callbacks.
        /// </summary>
        private void ProcessAudioPipeline(int frames)
        {
            bool playing = Recorder.IsPlaying;

            if (playing)
            {
                // Playback mode: recorded audio already has FX baked in.
                // Just read it directly — do NOT re-apply effects.
                int read = Recorder.ReadPlayback(floatBuffer, frames);
                if (read == 0)
                {
                    // Playback finished, buffer already cleared by ReadPlayback
                }

                // Apply only master volume (user might want to adjust playback volume)
                float vol = Effects.MasterVolume;
                if (vol != 1f)
                    for (int i = 0; i < frames; i++)
                        floatBuffer[i] *= vol;
            }
            else
            {
                // Live mode: apply full effect chain + master
                Effects.Process(floatBuffer, frames);

                // Record the processed signal BEFORE metronome
                Recorder.WriteSamples(floatBuffer, frames);
            }

            // Metronome is ALWAYS after recording — never captured in WAV
            Metronome.Process(floatBuffer, frames);
        }

        // =========================================================
        // DEVICE ENUMERATION
        // =========================================================
        public List<MMDevice> GetInputDevices() => new MMDeviceEnumerator().EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList();
        public List<MMDevice> GetOutputDevices() => new MMDeviceEnumerator().EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();
        public static List<string> GetAsioDrivers() => AsioOut.GetDriverNames().ToList();
        public void SetAsioDriver(string driverName) => asioDriverName = driverName;

        public void SetAsioRouting(int inputOffset, int outputOffset, int inputCount, int outputCount)
        {
            asioInputOffset = Math.Max(0, inputOffset);
            asioOutputOffset = Math.Max(0, outputOffset);
            asioInputCount = Math.Max(1, inputCount);
            asioOutputCount = Math.Max(1, outputCount);
        }

        // =========================================================
        // TEST TONE
        // =========================================================
        public void StartAsioTest(int durationMs = 1000, float frequency = 800f, float amplitude = 0.5f)
        {
            testFrequency = frequency; testAmplitude = amplitude;
            Interlocked.Exchange(ref testRemainingSamples, (int)((durationMs / 1000.0) * SampleRate));
            testPhase = 0.0; Volatile.Write(ref testActive, true);
            if (Driver != AudioDriver.ASIO) Driver = AudioDriver.ASIO;
            if (string.IsNullOrEmpty(asioDriverName)) { OnLog?.Invoke("No ASIO driver set."); return; }
            if (!IsMonitoring)
            {
                try { StartAudio(); OnLog?.Invoke("ASIO test tone started."); }
                catch (Exception ex) { OnLog?.Invoke("ASIO test failed: " + ex.Message); Volatile.Write(ref testActive, false); }
            }
        }

        public void StopAsioTest() { Volatile.Write(ref testActive, false); Interlocked.Exchange(ref testRemainingSamples, 0); }

        // =========================================================
        // PROBE ASIO
        // =========================================================
        public (int inputCount, int outputCount) ProbeAsioChannels(string driverName)
        {
            try
            {
                using var probe = new AsioOut(driverName);
                var inProp = typeof(AsioOut).GetProperty("DriverInputChannelCount");
                var outProp = typeof(AsioOut).GetProperty("DriverOutputChannelCount");
                if (inProp != null && outProp != null)
                {
                    int inC = (int)(inProp.GetValue(probe) ?? 0), outC = (int)(outProp.GetValue(probe) ?? 0);
                    OnLog?.Invoke($"ProbeAsio '{driverName}': {inC} in, {outC} out");
                    return (inC, outC);
                }
                return (0, 0);
            }
            catch (Exception ex) { OnLog?.Invoke($"ProbeAsio failed: {ex.Message}"); return (0, 0); }
        }

        // =========================================================
        // MONITORING
        // =========================================================
        public void StartMonitoring() { if (!IsMonitoring) StartAudio(); }
        public void StopMonitoring() { if (IsMonitoring) StopAudio(); }
        public void RebuildAudioGraph() { bool r = IsMonitoring; StopAudio(); if (r) StartAudio(); }

        // =========================================================
        // START / STOP
        // =========================================================
        public void StartAudio()
        {
            try
            {
                Metronome.SetSampleRate(SampleRate);
                Metronome.Reset();
                if (Driver == AudioDriver.ASIO) StartAsio(); else StartWasapi();
                IsMonitoring = true;
                OnMonitoringChanged?.Invoke(true);
            }
            catch (Exception ex) { OnLog?.Invoke("[AudioEngine] Start error: " + ex.Message); StopAudio(); }
        }

        public void StopAudio()
        {
            if (Recorder.IsRecording) Recorder.StopRecording();
            if (Recorder.IsPlaying) Recorder.StopPlayback();

            try { capture?.StopRecording(); } catch { }
            try { playback?.Stop(); } catch { }
            try { asio?.Stop(); } catch { }
            if (capture != null) try { capture.DataAvailable -= OnWasapiData; } catch { }
            if (asio != null) try { asio.AudioAvailable -= OnAsioAudioAvailable; } catch { }
            try { capture?.Dispose(); } catch { }
            try { playback?.Dispose(); } catch { }
            try { asio?.Dispose(); } catch { }
            capture = null; playback = null; asio = null; buffer = null; wasapiFormat = null;

            if (floatBufferFromPool)
            { try { pool.Return(floatBuffer, clearArray: true); } catch { } floatBuffer = Array.Empty<float>(); floatBufferFromPool = false; }
            else floatBuffer = Array.Empty<float>();

            IsMonitoring = false;
            OnMonitoringChanged?.Invoke(false);
        }

        // =========================================================
        // WASAPI
        // =========================================================
        private void StartWasapi()
        {
            if (InputDevice == null || OutputDevice == null) throw new InvalidOperationException("Input or Output device missing");
            var shareMode = Driver == AudioDriver.WASAPI_Exclusive ? AudioClientShareMode.Exclusive : AudioClientShareMode.Shared;
            capture = new WasapiCapture(InputDevice) { ShareMode = shareMode };
            wasapiFormat = capture.WaveFormat;
            OnLog?.Invoke($"WASAPI: {wasapiFormat.Encoding} {wasapiFormat.SampleRate}Hz {wasapiFormat.BitsPerSample}bit x{wasapiFormat.Channels}");
            if (wasapiFormat.SampleRate != SampleRate) { OnLog?.Invoke($"Using device rate {wasapiFormat.SampleRate}Hz"); SampleRate = wasapiFormat.SampleRate; }
            capture.DataAvailable += OnWasapiData;
            buffer = new BufferedWaveProvider(wasapiFormat) { BufferLength = BufferSize * wasapiFormat.BlockAlign * 10, DiscardOnBufferOverflow = true };
            int latencyMs = Math.Max(1, (int)((double)BufferSize / SampleRate * 1000));
            playback = new WasapiOut(OutputDevice, shareMode, false, latencyMs);
            playback.Init(buffer);
            capture.StartRecording();
            playback.Play();
        }

        private void OnWasapiData(object? sender, WaveInEventArgs e)
        {
            processingTimer.Restart();
            var wf = wasapiFormat ?? capture?.WaveFormat;
            if (wf == null) return;
            int blockAlign = wf.BlockAlign, channels = wf.Channels, bits = wf.BitsPerSample;
            bool isFloat = wf.Encoding == WaveFormatEncoding.IeeeFloat;
            int frames = e.BytesRecorded / blockAlign;
            EnsureFloatBuffer(frames);

            try
            {
                // Decode input to floatBuffer
                if (bits == 32 && isFloat)
                { for (int f = 0, o = 0; f < frames; f++, o += blockAlign) { float s = 0f; for (int c = 0; c < channels; c++) s += BitConverter.ToSingle(e.Buffer, o + c * 4); floatBuffer[f] = s / channels; } }
                else if (bits == 16 && !isFloat)
                { for (int f = 0, o = 0; f < frames; f++, o += blockAlign) { int s = 0; for (int c = 0; c < channels; c++) { int ix = o + c * 2; s += (short)(e.Buffer[ix] | (e.Buffer[ix + 1] << 8)); } floatBuffer[f] = (s / (float)channels) / 32768f; } }
                else Array.Clear(floatBuffer, 0, frames);

                // Unified pipeline: FX → Record → Metronome (or Playback → Metronome)
                ProcessAudioPipeline(frames);

                WriteToBuffer(frames, wf);
                UpdateMeters(frames);
            }
            catch (Exception ex) { OnLog?.Invoke("[WASAPI] error: " + ex.Message); }
        }

        private void WriteToBuffer(int frames, WaveFormat wf)
        {
            int channels = wf.Channels, bits = wf.BitsPerSample;
            bool isFloat = wf.Encoding == WaveFormatEncoding.IeeeFloat;
            byte[] outBuf = new byte[frames * wf.BlockAlign];
            if (bits == 32 && isFloat)
            { for (int f = 0, o = 0; f < frames; f++) { float v = Math.Clamp(floatBuffer[f], -1f, 1f); var b = BitConverter.GetBytes(v); for (int c = 0; c < channels; c++) { outBuf[o++] = b[0]; outBuf[o++] = b[1]; outBuf[o++] = b[2]; outBuf[o++] = b[3]; } } }
            else if (bits == 16 && !isFloat)
            { for (int f = 0, o = 0; f < frames; f++) { short s = (short)(Math.Clamp(floatBuffer[f], -1f, 1f) * 32767); for (int c = 0; c < channels; c++) { outBuf[o++] = (byte)(s & 0xFF); outBuf[o++] = (byte)(s >> 8); } } }
            else return;
            buffer?.AddSamples(outBuf, 0, outBuf.Length);
        }

        // =========================================================
        // ASIO
        // =========================================================
        private void StartAsio()
        {
            if (string.IsNullOrEmpty(asioDriverName)) throw new InvalidOperationException("No ASIO driver selected");
            asio = new AsioOut(asioDriverName);
            asio.AudioAvailable += OnAsioAudioAvailable;

            int driverIn = 0, driverOut = 0;
            var ip = typeof(AsioOut).GetProperty("DriverInputChannelCount");
            var op = typeof(AsioOut).GetProperty("DriverOutputChannelCount");
            if (ip != null) driverIn = (int)(ip.GetValue(asio) ?? 0);
            if (op != null) driverOut = (int)(op.GetValue(asio) ?? 0);
            OnLog?.Invoke($"Driver: {driverIn} in, {driverOut} out");

            int inOff = asioInputOffset, inCnt = asioInputCount, outOff = asioOutputOffset, outCnt = asioOutputCount;
            if (driverIn > 0) { if (inOff >= driverIn) inOff = 0; if (inOff + inCnt > driverIn) inCnt = driverIn - inOff; if (inCnt < 1) inCnt = 1; }
            if (driverOut > 0) { if (outOff >= driverOut) outOff = 0; if (outOff + outCnt > driverOut) outCnt = driverOut - outOff; if (outCnt < 1) outCnt = 1; }

            asio.InputChannelOffset = inOff; asio.ChannelOffset = outOff;
            OnLog?.Invoke($"ASIO Init: in[{inOff}+{inCnt}], out[{outOff}+{outCnt}], sr={SampleRate}");

            try { asio.InitRecordAndPlayback(new SilenceProvider(SampleRate, outCnt), inCnt, SampleRate); }
            catch (Exception ex)
            {
                OnLog?.Invoke($"ASIO Init failed: {ex.Message} — fallback");
                asio.Dispose(); asio = new AsioOut(asioDriverName);
                asio.AudioAvailable += OnAsioAudioAvailable;
                asio.InputChannelOffset = 0; asio.ChannelOffset = 0;
                asio.InitRecordAndPlayback(new SilenceProvider(SampleRate, 2), 1, SampleRate);
            }

            asio.Play();
            OnLog?.Invoke($"ASIO running: {asio.NumberOfInputChannels}in {asio.NumberOfOutputChannels}out");
        }

        private static unsafe void ReadAsioInput(IntPtr buf, float[] dest, int n, AsioSampleType t)
        {
            switch (t)
            {
                case AsioSampleType.Int32LSB: { int* s = (int*)buf; for (int i = 0; i < n; i++) dest[i] = s[i] / (float)int.MaxValue; } break;
                case AsioSampleType.Int24LSB: { byte* s = (byte*)buf; for (int i = 0; i < n; i++) { int v = s[i * 3] | (s[i * 3 + 1] << 8) | (s[i * 3 + 2] << 16); if ((v & 0x800000) != 0) v |= unchecked((int)0xFF000000); dest[i] = v / 8388608f; } } break;
                case AsioSampleType.Int16LSB: { short* s = (short*)buf; for (int i = 0; i < n; i++) dest[i] = s[i] / 32768f; } break;
                case AsioSampleType.Float32LSB: Marshal.Copy(buf, dest, 0, n); break;
                default: Array.Clear(dest, 0, n); break;
            }
        }

        private static unsafe void WriteAsioOutput(IntPtr buf, float[] src, int n, AsioSampleType t)
        {
            switch (t)
            {
                case AsioSampleType.Int32LSB: { int* d = (int*)buf; for (int i = 0; i < n; i++) d[i] = (int)(Math.Clamp(src[i], -1f, 1f) * int.MaxValue); } break;
                case AsioSampleType.Int24LSB: { byte* d = (byte*)buf; for (int i = 0; i < n; i++) { int v = (int)(Math.Clamp(src[i], -1f, 1f) * 8388607f); d[i * 3] = (byte)(v & 0xFF); d[i * 3 + 1] = (byte)((v >> 8) & 0xFF); d[i * 3 + 2] = (byte)((v >> 16) & 0xFF); } } break;
                case AsioSampleType.Int16LSB: { short* d = (short*)buf; for (int i = 0; i < n; i++) d[i] = (short)(Math.Clamp(src[i], -1f, 1f) * 32767f); } break;
                case AsioSampleType.Float32LSB: Marshal.Copy(src, 0, buf, n); break;
                default: break;
            }
        }

        private unsafe void OnAsioAudioAvailable(object? sender, AsioAudioAvailableEventArgs e)
        {
            try
            {
                processingTimer.Restart();
                int samples = e.SamplesPerBuffer;
                var localIn = e.InputBuffers; var localOut = e.OutputBuffers;
                int inCh = localIn?.Length ?? 0, outCh = localOut?.Length ?? 0;
                var st = e.AsioSampleType;
                EnsureFloatBuffer(samples);

                // Test tone (bypasses everything)
                if (Volatile.Read(ref testActive))
                {
                    for (int i = 0; i < samples; i++) { floatBuffer[i] = (float)(testAmplitude * Math.Sin(testPhase)); testPhase += 2 * Math.PI * testFrequency / SampleRate; if (testPhase > 2 * Math.PI) testPhase -= 2 * Math.PI; }
                    if (localOut != null) for (int c = 0; c < outCh; c++) if (localOut[c] != IntPtr.Zero) WriteAsioOutput(localOut[c], floatBuffer, samples, st);
                    if (Volatile.Read(ref testRemainingSamples) > 0 && Interlocked.Add(ref testRemainingSamples, -samples) <= 0) Volatile.Write(ref testActive, false);
                    UpdateMeters(samples); e.WrittenToOutputBuffers = true; return;
                }

                // Read input
                if (inCh >= 1 && localIn != null && localIn[0] != IntPtr.Zero)
                    ReadAsioInput(localIn[0], floatBuffer, samples, st);
                else
                    Array.Clear(floatBuffer, 0, samples);

                // Unified pipeline: FX → Record → Metronome (or Playback → Metronome)
                ProcessAudioPipeline(samples);

                // Output
                if (localOut != null)
                    for (int c = 0; c < outCh; c++)
                        if (localOut[c] != IntPtr.Zero)
                            WriteAsioOutput(localOut[c], floatBuffer, samples, st);

                UpdateMeters(samples);
                e.WrittenToOutputBuffers = true;
            }
            catch (Exception ex)
            { try { OnLog?.Invoke("[ASIO] error: " + ex.Message); } catch { } if (e != null) e.WrittenToOutputBuffers = false; }
        }

        // =========================================================
        // METERS
        // =========================================================
        private void UpdateMeters(int samples)
        {
            float sum = 0f; for (int i = 0; i < samples; i++) sum += floatBuffer[i] * floatBuffer[i];
            Level = (float)Math.Sqrt(sum / samples);
            processingTimer.Stop();
            double ms = processingTimer.Elapsed.TotalMilliseconds;
            processingEmaMs = processingEmaMs <= 0.0 ? ms : emaAlpha * ms + (1.0 - emaAlpha) * processingEmaMs;
            double bufMs = ((double)BufferSize / SampleRate) * 1000.0;
            bool ov = processingEmaMs > bufMs;
            if (ov != CpuOverload) { CpuOverload = ov; OnCpuOverloadChanged?.Invoke(ov); }
        }

        public double CalculatedLatencyMs
        {
            get
            {
                if (Driver == AudioDriver.ASIO) return ((double)BufferSize / SampleRate) * 1000.0 + processingEmaMs;
                double c = ((double)BufferSize / SampleRate) * 1000.0, o = 0;
                if (buffer != null && wasapiFormat != null && wasapiFormat.BlockAlign > 0) o = (double)(buffer.BufferedBytes / wasapiFormat.BlockAlign) / wasapiFormat.SampleRate * 1000.0;
                return c + o + processingEmaMs;
            }
        }

        public bool ShowAsioControlPanel()
        {
            if (asio != null) { try { asio.ShowControlPanel(); return true; } catch { } }
            else if (!string.IsNullOrEmpty(asioDriverName)) { try { using var t = new AsioOut(asioDriverName); t.ShowControlPanel(); return true; } catch { } }
            OnLog?.Invoke("ASIO control panel not supported. Use MiniFuse Control Center."); return false;
        }
    }
}
