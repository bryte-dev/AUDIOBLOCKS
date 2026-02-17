using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace AudioBlocks.App.Audio
{
    /// <summary>
    /// Thread-safe audio recorder that captures processed float samples.
    /// Records mono post-FX signal. Supports playback and WAV export.
    /// </summary>
    public class AudioRecorder
    {
        private readonly object lockObj = new();
        private List<float> recordBuffer = new();
        private int playbackPosition;
        private volatile bool isRecording;
        private volatile bool isPlaying;

        // ===== STATE =====
        public bool IsRecording => isRecording;
        public bool IsPlaying => isPlaying;
        public bool HasRecording { get { lock (lockObj) return recordBuffer.Count > 0; } }

        public int RecordedSamples { get { lock (lockObj) return recordBuffer.Count; } }

        public double RecordedDurationMs(int sampleRate) =>
            sampleRate > 0 ? (double)RecordedSamples / sampleRate * 1000.0 : 0;

        // ===== EVENTS =====
        public event Action? OnStateChanged;
        public event Action<string>? OnLog;

        // =========================================================
        // RECORDING
        // =========================================================

        public void StartRecording()
        {
            lock (lockObj)
            {
                recordBuffer.Clear();
                playbackPosition = 0;
            }
            isRecording = true;
            isPlaying = false;
            OnLog?.Invoke("Recording started");
            OnStateChanged?.Invoke();
        }

        public void StopRecording()
        {
            isRecording = false;
            OnLog?.Invoke($"Recording stopped — {RecordedSamples} samples");
            OnStateChanged?.Invoke();
        }

        /// <summary>
        /// Called from the audio callback thread. Appends processed samples.
        /// </summary>
        public void WriteSamples(float[] buffer, int count)
        {
            if (!isRecording) return;

            lock (lockObj)
            {
                // Pre-allocate in chunks to reduce GC pressure
                if (recordBuffer.Capacity < recordBuffer.Count + count)
                    recordBuffer.Capacity = Math.Max(recordBuffer.Capacity * 2, recordBuffer.Count + count + 48000);

                for (int i = 0; i < count; i++)
                    recordBuffer.Add(buffer[i]);
            }
        }

        // =========================================================
        // PLAYBACK
        // =========================================================

        public void StartPlayback()
        {
            if (!HasRecording) { OnLog?.Invoke("Nothing to play"); return; }
            isRecording = false;
            playbackPosition = 0;
            isPlaying = true;
            OnLog?.Invoke("Playback started");
            OnStateChanged?.Invoke();
        }

        public void StopPlayback()
        {
            isPlaying = false;
            playbackPosition = 0;
            OnLog?.Invoke("Playback stopped");
            OnStateChanged?.Invoke();
        }

        /// <summary>
        /// Called from the audio callback. Reads recorded samples into the output buffer.
        /// Returns the number of samples actually read (0 = done).
        /// </summary>
        public int ReadPlayback(float[] buffer, int count)
        {
            if (!isPlaying) return 0;

            int read;
            lock (lockObj)
            {
                int available = recordBuffer.Count - playbackPosition;
                read = Math.Min(count, available);

                for (int i = 0; i < read; i++)
                    buffer[i] = recordBuffer[playbackPosition + i];
            }

            playbackPosition += read;

            // End of recording
            if (read < count)
            {
                // Fill remainder with silence
                for (int i = read; i < count; i++)
                    buffer[i] = 0f;

                isPlaying = false;
                OnStateChanged?.Invoke();
            }

            return read;
        }

        /// <summary>
        /// Returns current playback progress 0..1
        /// </summary>
        public double PlaybackProgress
        {
            get
            {
                lock (lockObj)
                {
                    if (recordBuffer.Count == 0) return 0;
                    return (double)playbackPosition / recordBuffer.Count;
                }
            }
        }

        // =========================================================
        // EXPORT
        // =========================================================

        /// <summary>
        /// Export recorded audio to a WAV file (mono, float32).
        /// </summary>
        public bool ExportWav(string filePath, int sampleRate)
        {
            float[] data;
            lock (lockObj)
            {
                if (recordBuffer.Count == 0) { OnLog?.Invoke("Nothing to export"); return false; }
                data = recordBuffer.ToArray();
            }

            try
            {
                var format = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
                using var writer = new WaveFileWriter(filePath, format);
                writer.WriteSamples(data, 0, data.Length);
                writer.Flush();

                double durSec = (double)data.Length / sampleRate;
                long fileSize = new FileInfo(filePath).Length;
                OnLog?.Invoke($"Exported: {filePath} ({durSec:0.0}s, {fileSize / 1024}KB)");
                return true;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"Export failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Export as 16-bit PCM WAV (smaller file, compatible everywhere).
        /// </summary>
        public bool ExportWav16(string filePath, int sampleRate)
        {
            float[] data;
            lock (lockObj)
            {
                if (recordBuffer.Count == 0) { OnLog?.Invoke("Nothing to export"); return false; }
                data = recordBuffer.ToArray();
            }

            try
            {
                var format = new WaveFormat(sampleRate, 16, 1);
                using var writer = new WaveFileWriter(filePath, format);

                // Write in chunks to avoid huge byte[] allocation
                const int chunkSize = 4096;
                var buf16 = new short[chunkSize];

                for (int offset = 0; offset < data.Length; offset += chunkSize)
                {
                    int count = Math.Min(chunkSize, data.Length - offset);
                    for (int i = 0; i < count; i++)
                        buf16[i] = (short)(Math.Clamp(data[offset + i], -1f, 1f) * 32767);

                    var bytes = new byte[count * 2];
                    Buffer.BlockCopy(buf16, 0, bytes, 0, count * 2);
                    writer.Write(bytes, 0, bytes.Length);
                }

                writer.Flush();

                double durSec = (double)data.Length / sampleRate;
                long fileSize = new FileInfo(filePath).Length;
                OnLog?.Invoke($"Exported 16-bit: {filePath} ({durSec:0.0}s, {fileSize / 1024}KB)");
                return true;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"Export failed: {ex.Message}");
                return false;
            }
        }

        // =========================================================
        // CLEAR
        // =========================================================

        public void Clear()
        {
            isRecording = false;
            isPlaying = false;
            lock (lockObj)
            {
                recordBuffer.Clear();
                recordBuffer.Capacity = 0; // release memory
                playbackPosition = 0;
            }
            OnStateChanged?.Invoke();
        }
    }
}
