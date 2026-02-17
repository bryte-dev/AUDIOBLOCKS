using AudioBlocks.App.Audio;
using System;

namespace AudioBlocks.App.Effects
{
    public class DistortionEffect : IAudioEffect
    {
        public string Name => "Distortion";
        public bool Enabled { get; set; } = true;

        /// <summary>Drive: 0 = clean, 1 = heavy overdrive</summary>
        public float Drive { get; set; } = 0.3f;

        /// <summary>Tone: 0 = dark (low-pass), 1 = bright (no filter)</summary>
        public float Tone { get; set; } = 0.6f;

        /// <summary>Mix: 0 = full dry, 1 = full wet</summary>
        public float Mix { get; set; } = 1.0f;

        /// <summary>Output level: 0..1</summary>
        public float Level { get; set; } = 0.7f;

        private float lpState;

        public void Process(float[] buffer, int count)
        {
            float driveGain = 1f + Drive * 14f;
            float lpCoeff = 0.05f + Tone * 0.95f;
            float outLevel = Level;

            // Compensate volume: tanh(x) ≈ x for small x, ≈ 1 for large x
            // Higher drive = more compression = louder perceived output
            // Reduce output proportionally to keep consistent volume
            float compensation = 1f / MathF.Sqrt(driveGain);

            for (int i = 0; i < count; i++)
            {
                float dry = buffer[i];

                // Pre-clip input to prevent extreme values feeding tanh
                float clipped = Math.Clamp(dry, -1f, 1f);

                // Soft-clip with tanh
                float wet = (float)Math.Tanh(clipped * driveGain);

                // Auto-compensate loudness based on drive amount
                wet *= compensation;

                // Tone filter (one-pole LP)
                lpState += lpCoeff * (wet - lpState);
                wet = lpState;

                // Output level
                wet *= outLevel;

                // Mix dry/wet
                buffer[i] = Math.Clamp(dry * (1f - Mix) + wet * Mix, -1f, 1f);
            }
        }
    }
}
