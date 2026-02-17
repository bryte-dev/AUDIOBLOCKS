using System;
using System.Threading;

namespace AudioBlocks.App.Audio
{
    /// <summary>
    /// High-precision synthesized metronome.
    /// Uses fractional sample accumulation to avoid integer drift at all BPM values.
    /// </summary>
    public class Metronome
    {
        private volatile bool enabled;
        private volatile int bpm = 120;
        private volatile float volume = 0.5f;
        private int sampleRate = 48000;
        private int beatsPerBar = 4;
        private int currentBeat;

        // Sub-sample precision: fractional accumulator
        private double sampleAccumulator;

        // Click synthesis
        private int clickDurationSamples = 800;
        private const float DownbeatFreq = 1800f;
        private const float BeatFreq = 1200f;
        private const float BaseAmplitude = 0.7f;

        // State
        private int clickRemaining;
        private float clickFreq;
        private double clickPhase;

        public bool Enabled { get => enabled; set => enabled = value; }
        public int BPM { get => bpm; set => bpm = Math.Clamp(value, 30, 300); }
        public int BeatsPerBar { get => beatsPerBar; set => beatsPerBar = Math.Clamp(value, 1, 16); }
        public int CurrentBeat => currentBeat + 1;

        /// <summary>Volume 0..1. Thread-safe.</summary>
        public float Volume { get => volume; set => volume = Math.Clamp(value, 0f, 1f); }

        public event Action<int>? OnBeat;

        public void SetSampleRate(int sr)
        {
            sampleRate = sr > 0 ? sr : 48000;
            // Scale click duration to ~16ms regardless of sample rate
            clickDurationSamples = Math.Max(200, (int)(sampleRate * 0.016));
        }

        public void Reset()
        {
            sampleAccumulator = 0;
            currentBeat = 0;
            clickRemaining = 0;
            clickPhase = 0;
        }

        /// <summary>
        /// Mix metronome clicks into the buffer. Called from audio callback.
        /// Uses fractional accumulation for drift-free timing at any BPM.
        /// </summary>
        public void Process(float[] buffer, int count)
        {
            if (!enabled) return;

            float vol = volume;
            if (vol <= 0.001f) return;

            // Fractional samples per beat for sub-sample precision
            double samplesPerBeat = (60.0 / bpm) * sampleRate;
            if (samplesPerBeat <= 0) return;

            int clickDur = clickDurationSamples;
            double phaseInc;

            for (int i = 0; i < count; i++)
            {
                // Fractional beat boundary detection
                if (sampleAccumulator >= samplesPerBeat)
                {
                    sampleAccumulator -= samplesPerBeat;
                    currentBeat = (currentBeat + 1) % beatsPerBar;

                    clickFreq = currentBeat == 0 ? DownbeatFreq : BeatFreq;
                    clickRemaining = clickDur;
                    clickPhase = 0;

                    try { OnBeat?.Invoke(currentBeat + 1); } catch { }
                }

                // Synthesize click with shaped envelope
                if (clickRemaining > 0)
                {
                    float t = 1f - (float)clickRemaining / clickDur;

                    // Attack-decay envelope: fast 1ms attack, then exponential decay
                    float attackSamples = sampleRate * 0.001f;
                    float envelope;
                    if (clickRemaining > clickDur - (int)attackSamples)
                        envelope = 1f - (float)(clickDur - clickRemaining) / attackSamples; // ramp up
                    else
                        envelope = MathF.Exp(-t * 5f); // exponential decay

                    // Add slight harmonic content for downbeat
                    float click;
                    phaseInc = 2.0 * Math.PI * clickFreq / sampleRate;
                    if (currentBeat == 0)
                    {
                        // Downbeat: fundamental + octave for punch
                        click = (float)(
                            Math.Sin(clickPhase) * 0.7 +
                            Math.Sin(clickPhase * 2.0) * 0.3
                        );
                    }
                    else
                    {
                        click = (float)Math.Sin(clickPhase);
                    }

                    click *= envelope * BaseAmplitude * vol;
                    clickPhase += phaseInc;

                    buffer[i] = Math.Clamp(buffer[i] + click, -1f, 1f);
                    clickRemaining--;
                }

                sampleAccumulator += 1.0;
            }
        }
    }
}
