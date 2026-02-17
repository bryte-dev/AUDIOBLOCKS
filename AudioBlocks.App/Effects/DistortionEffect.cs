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
        // Anti-aliasing filter states (simple 2x oversample)
        private float upFilterState;
        private float downFilterState;

        public void Process(float[] buffer, int count)
        {
            float driveGain = 1f + Drive * 19f;  // wider range for more character
            float outLevel = Level;
            float compensation = 1f / MathF.Sqrt(driveGain);

            // Tone filter: map 0..1 to cutoff coefficient
            // 0 = very dark (~500Hz), 1 = wide open
            float toneFreq = 500f + Tone * 15000f;
            float toneCoeff = 1f - MathF.Exp(-2f * MathF.PI * toneFreq / 48000f);

            for (int i = 0; i < count; i++)
            {
                float dry = buffer[i];
                float clipped = Math.Clamp(dry, -1f, 1f);

                // 2x oversample: upsample, process, downsample
                // First sample (interpolated)
                float up1 = (upFilterState + clipped) * 0.5f;
                upFilterState = clipped;
                // Second sample (original)
                float up2 = clipped;

                // Waveshaping on both samples
                float ws1 = MathF.Tanh(up1 * driveGain) * compensation;
                float ws2 = MathF.Tanh(up2 * driveGain) * compensation;

                // Downsample with averaging (simple anti-alias)
                float wet = (ws1 + ws2) * 0.5f;

                // Tone filter
                lpState += toneCoeff * (wet - lpState);
                wet = lpState;

                wet *= outLevel;

                buffer[i] = Math.Clamp(dry * (1f - Mix) + wet * Mix, -1f, 1f);
            }
        }
    }
}
