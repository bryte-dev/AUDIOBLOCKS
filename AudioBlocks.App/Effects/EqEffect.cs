using AudioBlocks.App.Audio;
using System;

namespace AudioBlocks.App.Effects
{
    /// <summary>
    /// 3-band EQ with sample-rate-aware crossover frequencies.
    /// Low: ~300Hz shelving, Mid: 300Hz-3kHz peaking, High: ~3kHz shelving
    /// </summary>
    public class EqEffect : IAudioEffect
    {
        public string Name => "EQ";
        public bool Enabled { get; set; } = true;

        /// <summary>Low gain: -1 = full cut, 0 = flat, +1 = full boost</summary>
        public float Low { get; set; } = 0f;

        /// <summary>Mid gain: -1 = full cut, 0 = flat, +1 = full boost</summary>
        public float Mid { get; set; } = 0f;

        /// <summary>High gain: -1 = full cut, 0 = flat, +1 = full boost</summary>
        public float High { get; set; } = 0f;

        // Two-pole filter states for steeper crossovers
        private float lp1, lp2;
        private float hp1, hp2;
        private int sampleRate = 48000;
        private float lastLpCoeff = -1f;

        // Band RMS levels for UI metering
        private float lowRms, midRms, highRms;

        public float LowLevel => lowRms;
        public float MidLevel => midRms;
        public float HighLevel => highRms;

        private float CalcCoeff(float freqHz)
        {
            // One-pole coefficient from cutoff frequency
            // c = 1 - exp(-2pi * f / sr)
            return 1f - MathF.Exp(-2f * MathF.PI * freqHz / sampleRate);
        }

        public void Process(float[] buffer, int count)
        {
            // Recalculate coefficients (cheap, just two exp calls)
            float lpCoeff = CalcCoeff(300f);   // Low crossover at 300Hz
            float hpCoeff = CalcCoeff(3000f);  // High crossover at 3kHz

            // Gain mapping: -1..+1 -> 0.15..1..6.3 (approx -16dB to +16dB)
            float lowGain = MathF.Pow(10f, Low * 0.8f);   // ~-16dB to +16dB
            float midGain = MathF.Pow(10f, Mid * 0.8f);
            float highGain = MathF.Pow(10f, High * 0.8f);

            float sumLow = 0f, sumMid = 0f, sumHigh = 0f;

            for (int i = 0; i < count; i++)
            {
                float input = buffer[i];

                // Two-pass LP for steeper low-band separation
                lp1 += lpCoeff * (input - lp1);
                lp2 += lpCoeff * (lp1 - lp2);
                float low = lp2;

                // Two-pass HP for steeper high-band separation
                hp1 += hpCoeff * (input - hp1);
                hp2 += hpCoeff * (hp1 - hp2);
                float high = input - hp2;

                // Mid is what remains
                float mid = input - low - high;

                sumLow += low * low;
                sumMid += mid * mid;
                sumHigh += high * high;

                buffer[i] = Math.Clamp(
                    low * lowGain + mid * midGain + high * highGain,
                    -1f, 1f);
            }

            if (count > 0)
            {
                lowRms = MathF.Sqrt(sumLow / count);
                midRms = MathF.Sqrt(sumMid / count);
                highRms = MathF.Sqrt(sumHigh / count);
            }
        }
    }
}
