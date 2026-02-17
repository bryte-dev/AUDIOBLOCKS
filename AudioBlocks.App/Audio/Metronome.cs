using System;

namespace AudioBlocks.App.Audio
{
    /// <summary>
    /// Synthesized metronome with configurable BPM.
    /// Generates click sounds mixed into the output buffer.
    /// </summary>
    public class Metronome
    {
        private volatile bool enabled;
        private volatile int bpm = 120;
        private int sampleRate = 48000;
        private long sampleCounter;
        private int beatsPerBar = 4;
        private int currentBeat; // 0-based within bar

        // Click synthesis
        private const int ClickDurationSamples = 800;  // ~17ms
        private const float ClickFreqHigh = 1500f;     // downbeat (1)
        private const float ClickFreqLow = 1000f;      // other beats
        private const float ClickAmplitude = 0.35f;

        // State
        private int clickRemaining;
        private float clickFreq;
        private double clickPhase;

        public bool Enabled { get => enabled; set => enabled = value; }
        public int BPM { get => bpm; set => bpm = Math.Clamp(value, 30, 300); }
        public int BeatsPerBar { get => beatsPerBar; set => beatsPerBar = Math.Clamp(value, 1, 16); }
        public int CurrentBeat => currentBeat + 1; // 1-based for display

        public event Action<int>? OnBeat; // fires on each beat (1-based)

        public void SetSampleRate(int sr) => sampleRate = sr > 0 ? sr : 48000;

        public void Reset()
        {
            sampleCounter = 0;
            currentBeat = 0;
            clickRemaining = 0;
            clickPhase = 0;
        }

        /// <summary>
        /// Mix metronome clicks into the buffer. Called from audio callback.
        /// </summary>
        public void Process(float[] buffer, int count, float volume = 1f)
        {
            if (!enabled) return;

            int samplesPerBeat = (int)((60.0 / bpm) * sampleRate);
            if (samplesPerBeat <= 0) return;

            for (int i = 0; i < count; i++)
            {
                // Check if we hit a new beat
                if (sampleCounter % samplesPerBeat == 0)
                {
                    currentBeat = (int)((sampleCounter / samplesPerBeat) % beatsPerBar);
                    clickFreq = currentBeat == 0 ? ClickFreqHigh : ClickFreqLow;
                    clickRemaining = ClickDurationSamples;
                    clickPhase = 0;

                    // Fire event (will be on audio thread — UI must dispatch)
                    try { OnBeat?.Invoke(currentBeat + 1); } catch { }
                }

                // Synthesize click
                if (clickRemaining > 0)
                {
                    float envelope = (float)clickRemaining / ClickDurationSamples; // decay
                    envelope *= envelope; // quadratic decay — snappier
                    float click = (float)(Math.Sin(clickPhase) * envelope * ClickAmplitude * volume);
                    clickPhase += 2 * Math.PI * clickFreq / sampleRate;

                    buffer[i] = Math.Clamp(buffer[i] + click, -1f, 1f);
                    clickRemaining--;
                }

                sampleCounter++;
            }
        }
    }
}
