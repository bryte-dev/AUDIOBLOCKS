using AudioBlocks.App.Audio;
using System;

namespace AudioBlocks.App.Effects
{
    public class CompressorEffect : IAudioEffect
    {
        public string Name => "Compressor";
        public bool Enabled { get; set; } = true;

        /// <summary>Threshold in linear (0..1). Signal above this gets compressed.</summary>
        public float Threshold { get; set; } = 0.5f;

        /// <summary>Ratio: 1 = no compression, higher = more squash. Range 1..20</summary>
        public float Ratio { get; set; } = 4f;

        /// <summary>Attack speed: 0 = instant, 1 = slow</summary>
        public float Attack { get; set; } = 0.3f;

        /// <summary>Release speed: 0 = instant, 1 = slow</summary>
        public float Release { get; set; } = 0.5f;

        /// <summary>Makeup gain in linear (1 = unity)</summary>
        public float Makeup { get; set; } = 1.5f;

        private float envelope;

        public void Process(float[] buffer, int count)
        {
            float attackCoeff = (float)Math.Exp(-1.0 / (1 + Attack * 500));
            float releaseCoeff = (float)Math.Exp(-1.0 / (1 + Release * 2000));

            for (int i = 0; i < count; i++)
            {
                float abs = Math.Abs(buffer[i]);

                // Envelope follower
                if (abs > envelope)
                    envelope = attackCoeff * envelope + (1 - attackCoeff) * abs;
                else
                    envelope = releaseCoeff * envelope + (1 - releaseCoeff) * abs;

                // Gain computation
                float gain = 1f;
                if (envelope > Threshold && Threshold > 0)
                {
                    float dbOver = 20f * (float)Math.Log10(envelope / Threshold);
                    float dbReduced = dbOver * (1f - 1f / Ratio);
                    gain = (float)Math.Pow(10, -dbReduced / 20f);
                }

                buffer[i] = Math.Clamp(buffer[i] * gain * Makeup, -1f, 1f);
            }
        }
    }
}
