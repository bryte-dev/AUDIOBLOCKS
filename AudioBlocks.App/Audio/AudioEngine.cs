using AudioBlocks.App.Effects;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Reflection;

namespace AudioBlocks.App.Audio
{
    public enum AudioDriver
    {
        WASAPI_Shared,
        WASAPI_Exclusive,
        ASIO
    }

    public class AudioEngine
    {
        // ===== PARAMÈTRES =====
        public AudioDriver Driver { get; set; } = AudioDriver.WASAPI_Shared;
        public MMDevice? InputDevice { get; set; }
        public MMDevice? OutputDevice { get; set; }
        public int SampleRate { get; set; } = 44100;
        public int BufferSize { get; set; } = 256;

        // ===== ASIO =====
        private string? asioDriverName;
        private AsioOut? asio;
        // channel mapping fournis par l'UI (indices de canaux ASIO)
        private int[]? asioInputChannels;
        private int[]? asioOutputChannels;

        // ===== WASAPI =====
        private WasapiCapture? capture;
        private WasapiOut? playback;
        private BufferedWaveProvider? buffer;

        // ===== AUDIO =====
        private float[] floatBuffer = Array.Empty<float>();

        // ===== STATE =====
        public bool IsMonitoring { get; private set; }
        public float Level { get; private set; }

        // ===== CPU =====
        private readonly Stopwatch processingTimer = new();
        public bool CpuOverload { get; private set; }
        public event Action<bool>? OnCpuOverloadChanged;

        // ===== LOG / EVENTS =====
        public event Action<string>? OnLog;

        // ===== EFFECTS =====
        public AudioEffects Effects { get; } = new();

        // ===== TEST TONE =====
        private bool testActive = false;
        private int testRemainingSamples = 0;
        private double testPhase = 0.0;
        private float testFrequency = 440f;
        private float testAmplitude = 0.6f;
        private int[]? testOutputChannels;

        // ===== CONSTRUCTOR =====
        public AudioEngine()
        {
            Effects.AddEffect(new GainEffect());
        }

        // =========================================================
        // DEVICE ENUMERATION
        // =========================================================

        public List<MMDevice> GetInputDevices() =>
            new MMDeviceEnumerator()
                .EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                .ToList();

        public List<MMDevice> GetOutputDevices() =>
            new MMDeviceEnumerator()
                .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                .ToList();

        public static List<string> GetAsioDrivers() =>
            AsioOut.GetDriverNames().ToList();

        public void SetAsioDriver(string driverName)
        {
            asioDriverName = driverName;
        }

        // channel mapping setter (UI fournit les indices ASIO choisis)
        public void SetAsioChannels(int[]? inputChannels, int[]? outputChannels)
        {
            asioInputChannels = inputChannels;
            asioOutputChannels = outputChannels;
        }

        // Permet de lancer un test tone sur la paire de sorties ASIO spécifiée (indices zero-based).
        // Le test démarre ASIO si nécessaire et s'arrête automatiquement après durationMs.
        public void StartAsioTest(int[] outChannels, int durationMs = 1000, float frequency = 1000f, float amplitude = 0.6f)
        {
            testOutputChannels = outChannels?.ToArray();
            testFrequency = frequency;
            testAmplitude = amplitude;

            testRemainingSamples = (int)((durationMs / 1000.0) * SampleRate);
            testPhase = 0.0;
            testActive = true;

            // s'assurer que le moteur est en mode ASIO et que le driver est set
            if (Driver != AudioDriver.ASIO)
                Driver = AudioDriver.ASIO;

            if (string.IsNullOrEmpty(asioDriverName))
                OnLog?.Invoke("No ASIO driver set for test tone.");

            // appliquer mapping outChannels pour l'init Asio
            if (testOutputChannels != null)
                asioOutputChannels = testOutputChannels;

            if (!IsMonitoring)
            {
                try
                {
                    StartAudio(); // StartAsio sera appelé
                    OnLog?.Invoke("ASIO test started.");
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke("ASIO test start failed: " + ex.Message);
                    testActive = false;
                }
            }
        }

        public void StopAsioTest()
        {
            testActive = false;
            testRemainingSamples = 0;
            testOutputChannels = null;
            OnLog?.Invoke("ASIO test stopped.");
        }

        // NEW: probe simple pour récupérer le nombre d'entrées/sorties exposées par un driver ASIO
        // Retourne (inputCount, outputCount). En cas d'erreur retourne (0,0).
        public (int inputCount, int outputCount) ProbeAsioChannels(string driverName)
        {
            try
            {
                using var probe = new AsioOut(driverName);
                int inCount = 0;
                int outCount = 0;

                var inProp = typeof(AsioOut).GetProperty("DriverInputChannelCount");
                var outProp = typeof(AsioOut).GetProperty("DriverOutputChannelCount");

                if (inProp != null && outProp != null)
                {
                    inCount = (int)(inProp.GetValue(probe) ?? 0);
                    outCount = (int)(outProp.GetValue(probe) ?? 0);
                }
                else
                {
                    var methods = typeof(AsioOut).GetMethods(BindingFlags.Instance | BindingFlags.Public)
                        .Where(m => m.Name == "InitRecordAndPlayback")
                        .ToArray();

                    var chosen = methods.FirstOrDefault(m =>
                    {
                        var ps = m.GetParameters();
                        return ps.Length == 3
                            && ps[0].ParameterType == typeof(int[])
                            && ps[1].ParameterType == typeof(int[])
                            && ps[2].ParameterType == typeof(int);
                    });

                    if (chosen != null)
                    {
                        chosen.Invoke(probe, new object[] { new int[] { 0 }, new int[] { 0 }, SampleRate });
                        inProp = typeof(AsioOut).GetProperty("DriverInputChannelCount");
                        outProp = typeof(AsioOut).GetProperty("DriverOutputChannelCount");
                        if (inProp != null && outProp != null)
                        {
                            inCount = (int)(inProp.GetValue(probe) ?? 0);
                            outCount = (int)(outProp.GetValue(probe) ?? 0);
                        }
                    }
                }

                probe.Dispose();
                return (inCount, outCount);
            }
            catch
            {
                return (0, 0);
            }
        }

        // =========================================================
        // MONITORING CONTROL (API POUR L’UI)
        // =========================================================

        public void StartMonitoring()
        {
            if (IsMonitoring)
                return;

            StartAudio();
        }

        public void StopMonitoring()
        {
            if (!IsMonitoring)
                return;

            StopAudio();
        }

        public void RebuildAudioGraph()
        {
            bool wasRunning = IsMonitoring;
            StopAudio();

            if (wasRunning)
                StartAudio();
        }

        // =========================================================
        // AUDIO START / STOP
        // =========================================================

        public void StartAudio()
        {
            try
            {
                if (Driver == AudioDriver.ASIO)
                {
                    StartAsio();
                }
                else
                {
                    StartWasapi();
                }

                IsMonitoring = true;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke("[AudioEngine] Start error: " + ex.Message);
                StopAudio();
            }
        }

        public void StopAudio()
        {
            try
            {
                capture?.StopRecording();
                playback?.Stop();
                asio?.Stop();
            }
            catch { }

            capture?.Dispose();
            playback?.Dispose();
            asio?.Dispose();

            capture = null;
            playback = null;
            asio = null;
            buffer = null;
            floatBuffer = Array.Empty<float>();

            IsMonitoring = false;
        }

        // =========================================================
        // WASAPI
        // =========================================================

        private void StartWasapi()
        {
            if (InputDevice == null || OutputDevice == null)
                throw new InvalidOperationException("Input or Output device missing");

            var shareMode = Driver == AudioDriver.WASAPI_Exclusive
                ? AudioClientShareMode.Exclusive
                : AudioClientShareMode.Shared;

            capture = new WasapiCapture(InputDevice)
            {
                ShareMode = shareMode,
                WaveFormat = new WaveFormat(SampleRate, 1)
            };

            capture.DataAvailable += OnWasapiData;
            capture.RecordingStopped += (_, e) =>
            {
                if (e.Exception != null)
                    OnLog?.Invoke(e.Exception.Message);
            };

            buffer = new BufferedWaveProvider(capture.WaveFormat)
            {
                BufferLength = BufferSize * capture.WaveFormat.BlockAlign * 10,
                DiscardOnBufferOverflow = true
            };

            int latencyMs = (int)((double)BufferSize / SampleRate * 1000);
            playback = new WasapiOut(OutputDevice, shareMode, false, latencyMs);
            playback.Init(buffer);

            capture.StartRecording();
            playback.Play();
        }

        private void OnWasapiData(object? sender, WaveInEventArgs e)
        {
            processingTimer.Restart();

            int samples = e.BytesRecorded / 2;
            if (floatBuffer.Length < samples)
                floatBuffer = new float[samples];

            for (int i = 0, s = 0; i < e.BytesRecorded; i += 2, s++)
            {
                short sample = (short)(e.Buffer[i] | (e.Buffer[i + 1] << 8));
                floatBuffer[s] = sample / 32768f;
            }

            Effects.Process(floatBuffer);
            WriteToBuffer(samples);

            UpdateMeters(samples);
        }

        private void WriteToBuffer(int samples)
        {
            byte[] outBuf = new byte[samples * 2];

            for (int i = 0; i < samples; i++)
            {
                short s16 = (short)(Math.Clamp(floatBuffer[i], -1f, 1f) * 32767);
                outBuf[i * 2] = (byte)(s16 & 0xFF);
                outBuf[i * 2 + 1] = (byte)(s16 >> 8);
            }

            buffer?.AddSamples(outBuf, 0, outBuf.Length);
        }

        // =========================================================
        // ASIO
        // =========================================================

        private void StartAsio()
        {
            if (string.IsNullOrEmpty(asioDriverName))
                throw new InvalidOperationException("No ASIO driver selected");

            asio = new AsioOut(asioDriverName);
            asio.AudioAvailable += OnAsioAudioAvailable;

            // IMPORTANT : 2 = stereo output
            asio.InitRecordAndPlayback(null, 2, SampleRate);

            asio.Play();

            OnLog?.Invoke("ASIO started (NAudio 2.x mode)");
        }



        private unsafe void OnAsioAudioAvailable(object? sender, AsioAudioAvailableEventArgs e)
        {
            processingTimer.Restart();

            int samples = e.SamplesPerBuffer;
            var inputBuffers = e.InputBuffers;
            var outputBuffers = e.OutputBuffers;

            int inputChannels = inputBuffers?.Length ?? 0;
            int outputChannels = outputBuffers?.Length ?? 0;

            // log pour diagnostic
            OnLog?.Invoke($"ASIO callback: inputs={inputChannels}, outputs={outputChannels}, samples={samples}");

            if (floatBuffer.Length < samples)
                floatBuffer = new float[samples];

            // --- Test sine signal ---
            if (testActive)
            {
                for (int i = 0; i < samples; i++)
                {
                    floatBuffer[i] = (float)(testAmplitude * Math.Sin(testPhase));
                    testPhase += 2 * Math.PI * testFrequency / SampleRate;
                    if (testPhase > 2 * Math.PI) testPhase -= 2 * Math.PI;
                }

                if (outputBuffers != null)
                {
                    for (int ch = 0; ch < outputChannels; ch++)
                    {
                        try
                        {
                            if (outputBuffers[ch] == IntPtr.Zero)
                                continue;

                            // écrire uniquement sur les sorties demandées, sinon silence
                            if (testOutputChannels != null && Array.IndexOf(testOutputChannels, ch) < 0)
                                Marshal.Copy(new float[samples], 0, outputBuffers[ch], samples);
                            else
                                Marshal.Copy(floatBuffer, 0, outputBuffers[ch], samples);
                        }
                        catch (Exception ex)
                        {
                            OnLog?.Invoke("[AudioEngine] ASIO test output write error: " + ex.Message);
                        }
                    }
                }

                if (testRemainingSamples > 0)
                {
                    testRemainingSamples -= samples;
                    if (testRemainingSamples <= 0)
                        testActive = false;
                }

                UpdateMeters(samples);
                e.WrittenToOutputBuffers = true;
                return;
            }

            // --- Lire / mixer les entrées en mono ---
            if (inputChannels >= 1 && inputBuffers != null)
            {
                if (inputChannels == 1)
                {
                    Marshal.Copy(inputBuffers[0], floatBuffer, 0, samples);
                }
                else
                {
                    float[] temp = new float[samples];
                    Array.Clear(floatBuffer, 0, samples);

                    for (int ch = 0; ch < inputChannels; ch++)
                    {
                        if (inputBuffers[ch] == IntPtr.Zero)
                            continue;
                        Marshal.Copy(inputBuffers[ch], temp, 0, samples);
                        for (int i = 0; i < samples; i++)
                            floatBuffer[i] += temp[i];
                    }

                    float inv = 1f / inputChannels;
                    for (int i = 0; i < samples; i++)
                        floatBuffer[i] *= inv;
                }
            }
            else
            {
                Array.Clear(floatBuffer, 0, samples);
            }

            // --- Traitement effets ---
            Effects.Process(floatBuffer);

            // --- Écrire sur toutes les sorties ---
            if (outputBuffers != null)
            {
                for (int ch = 0; ch < outputChannels; ch++)
                {
                    try
                    {
                        if (outputBuffers[ch] != IntPtr.Zero)
                            Marshal.Copy(floatBuffer, 0, outputBuffers[ch], samples);
                    }
                    catch (Exception ex)
                    {
                        OnLog?.Invoke("[AudioEngine] ASIO output write error: " + ex.Message);
                    }
                }
            }

            UpdateMeters(samples);
            e.WrittenToOutputBuffers = true;
        }

        // =========================================================
        // METERS
        // =========================================================

        private void UpdateMeters(int samples)
        {
            float sum = 0f;
            for (int i = 0; i < samples; i++)
                sum += floatBuffer[i] * floatBuffer[i];

            Level = (float)Math.Sqrt(sum / samples);

            processingTimer.Stop();
            bool overload =
                processingTimer.Elapsed.TotalMilliseconds >
                ((double)BufferSize / SampleRate) * 1000;

            if (overload != CpuOverload)
            {
                CpuOverload = overload;
                OnCpuOverloadChanged?.Invoke(overload);
            }
        }

        // =========================================================
        // LATENCY (INFO UI)
        // =========================================================

        public double CalculatedLatencyMs =>
            Driver == AudioDriver.ASIO
                ? 0
                : ((double)BufferSize / SampleRate) * 2 * 1000;

        public void DescribeAsioDriver(string driverName)
        {
            try
            {
                OnLog?.Invoke($"DescribeAsioDriver: probing '{driverName}'...");

                using var probe = new AsioOut(driverName);

                // list InitRecordAndPlayback overloads
                var methods = typeof(AsioOut).GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .Where(m => m.Name == "InitRecordAndPlayback")
                    .ToArray();

                if (methods.Length == 0)
                {
                    OnLog?.Invoke("  No InitRecordAndPlayback methods found on AsioOut.");
                }
                else
                {
                    OnLog?.Invoke($"  Found {methods.Length} InitRecordAndPlayback overload(s):");
                    foreach (var m in methods)
                    {
                        var ps = m.GetParameters();
                        string sig = string.Join(", ", ps.Select(p => p.ParameterType.Name + " " + p.Name));
                        OnLog?.Invoke($"    - {m.Name}({sig})");
                    }
                }

                // try to read common driver properties
                var inProp = typeof(AsioOut).GetProperty("DriverInputChannelCount");
                var outProp = typeof(AsioOut).GetProperty("DriverOutputChannelCount");
                if (inProp != null && outProp != null)
                {
                    try
                    {
                        int inCnt = (int)(inProp.GetValue(probe) ?? 0);
                        int outCnt = (int)(outProp.GetValue(probe) ?? 0);
                        OnLog?.Invoke($"  DriverInputChannelCount = {inCnt}, DriverOutputChannelCount = {outCnt}");
                    }
                    catch { OnLog?.Invoke("  Could not read Driver channel count properties."); }
                }
                else
                {
                    OnLog?.Invoke("  Driver channel count properties not found on AsioOut.");
                }

                // try to find a sample-rate property
                var srProp = typeof(AsioOut).GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(p => p.PropertyType == typeof(int) &&
                                         (p.Name.IndexOf("SampleRate", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          p.Name.IndexOf("Rate", StringComparison.OrdinalIgnoreCase) >= 0));
                if (srProp != null)
                {
                    try
                    {
                        var val = srProp.GetValue(probe);
                        OnLog?.Invoke($"  Sample-rate property '{srProp.Name}' = {val}");
                    }
                    catch { OnLog?.Invoke($"  Could not read sample-rate property '{srProp.Name}'."); }
                }
                else
                {
                    OnLog?.Invoke("  No obvious sample-rate property found on AsioOut.");
                }

                // control panel method existence
                var show = typeof(AsioOut).GetMethod("ShowControlPanel", BindingFlags.Instance | BindingFlags.Public);
                if (show != null)
                    OnLog?.Invoke("  ShowControlPanel() method is available on AsioOut.");
                else
                    OnLog?.Invoke("  No ShowControlPanel() method on AsioOut (driver panel may require vendor app).");

                probe.Dispose();
                OnLog?.Invoke("DescribeAsioDriver: probe complete.");
            }
            catch (Exception ex)
            {
                OnLog?.Invoke("DescribeAsioDriver failed: " + ex.Message);
            }
        }
    }
}
