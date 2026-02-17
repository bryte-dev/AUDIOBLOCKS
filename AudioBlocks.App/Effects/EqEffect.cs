using AudioBlocks.App.Audio;
using System;

namespace AudioBlocks.App.Effects
{
    /// <summary>
    /// Simple 3-band EQ (Low/Mid/High shelving)
    /// </summary>
    public class EqEffect : IAudioEffect
    {
        public string Name => "EQ";
        public bool Enabled { get; set; } = true;

        /// <summary>Low gain: -1 = cut, 0 = flat, +1 = boost</summary>
        public float Low { get; set; } = 0f;

        /// <summary>Mid gain: -1 = cut, 0 = flat, +1 = boost</summary>
        public float Mid { get; set; } = 0f;

        /// <summary>High gain: -1 = cut, 0 = flat, +1 = boost</summary>
        public float High { get; set; } = 0f;

        // Filter states
        private float lpState, hpState;

        public void Process(float[] buffer, int count)
        {
            // LP crossover ~300Hz, HP crossover ~3kHz (approximate for 48kHz SR)
            float lpCoeff = 0.04f;  // ~300Hz
            float hpCoeff = 0.15f;  // ~3kHz

            float lowGain = 1f + Low * 0.8f;   // 0.2..1.8
            float midGain = 1f + Mid * 0.8f;
            float highGain = 1f + High * 0.8f;

            for (int i = 0; i < count; i++)
            {
                float input = buffer[i];

                // Split into 3 bands
                lpState += lpCoeff * (input - lpState);
                float low = lpState;

                hpState += hpCoeff * (input - hpState);
                float high = input - hpState;

                float mid = input - low - high;

                // Recombine with gains
                buffer[i] = Math.Clamp(
                    low * lowGain + mid * midGain + high * highGain,
                    -1f, 1f);
            }
        }
    }
}
