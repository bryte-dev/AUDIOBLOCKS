using AudioBlocks.App.Audio;
using System;

namespace AudioBlocks.App.Effects
{
    public class CompressorEffect : IAudioEffect
    {
        public string Name => "Compressor";
        public bool Enabled { get; set; } = true;

        /// <summary>Threshold in dB (-60..0)</summary>
        public float Threshold { get; set; } = 0.5f;

        /// <summary>Ratio: 1 = no compression, higher = more squash. Range 1..20</summary>
        public float Ratio { get; set; } = 4f;

        /// <summary>Attack speed: 0 = instant, 1 = slow (~100ms)</summary>
        public float Attack { get; set; } = 0.3f;

        /// <summary>Release speed: 0 = instant, 1 = slow (~1s)</summary>
        public float Release { get; set; } = 0.5f;

        /// <summary>Makeup gain in linear (1 = unity)</summary>
        public float Makeup { get; set; } = 1.5f;

        /// <summary>Knee width in dB (0 = hard knee, higher = softer)</summary>
        public float KneeDb { get; set; } = 6f;

        private float envDb = -96f;
        private float lastGainReductionDb;
        private int sampleRate = 48000;

        /// <summary>Current gain reduction in dB (negative when compressing).</summary>
        public float GainReductionDb => lastGainReductionDb;

        public void Process(float[] buffer, int count)
        {
            // Convert normalized 0..1 knobs to real time constants
            // Attack: 0.1ms (instant) to 100ms (slow)
            float attackMs = 0.1f + Attack * 99.9f;
            // Release: 5ms (instant) to 1000ms (slow)
            float releaseMs = 5f + Release * 995f;

            float attackCoeff = MathF.Exp(-1f / (attackMs * 0.001f * sampleRate));
            float releaseCoeff = MathF.Exp(-1f / (releaseMs * 0.001f * sampleRate));

            // Threshold from linear 0..1 to dB
            float threshDb = Threshold <= 0.001f ? -60f : 20f * MathF.Log10(Threshold);
            float knee = KneeDb;
            float ratio = Ratio;
            float makeup = Makeup;

            float peakGr = 0f;

            for (int i = 0; i < count; i++)
            {
                float abs = MathF.Abs(buffer[i]);

                // Convert input to dB
                float inputDb = abs < 1e-8f ? -160f : 20f * MathF.Log10(abs);

                // Envelope follower in dB domain (peak-detecting)
                if (inputDb > envDb)
                    envDb = attackCoeff * envDb + (1f - attackCoeff) * inputDb;
                else
                    envDb = releaseCoeff * envDb + (1f - releaseCoeff) * inputDb;

                // Gain computation with soft knee
                float overDb = envDb - threshDb;
                float gainDb;

                if (knee > 0f && MathF.Abs(overDb) < knee / 2f)
                {
                    // Soft knee region
                    float x = overDb + knee / 2f;
                    gainDb = -(x * x) / (2f * knee) * (1f - 1f / ratio);
                }
                else if (overDb > 0f)
                {
                    // Above threshold
                    gainDb = -overDb * (1f - 1f / ratio);
                }
                else
                {
                    gainDb = 0f;
                }

                if (gainDb < peakGr) peakGr = gainDb;

                float gain = MathF.Pow(10f, gainDb / 20f);
                buffer[i] = Math.Clamp(buffer[i] * gain * makeup, -1f, 1f);
            }

            lastGainReductionDb = peakGr;
        }
    }
}
