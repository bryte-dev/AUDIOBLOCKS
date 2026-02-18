using AudioBlocks.App.Audio;
using System;

namespace AudioBlocks.App.Effects
{
    /// <summary>
    /// 10-band graphic EQ using cascaded biquad peaking filters.
    /// Standard ISO octave bands: 31, 63, 125, 250, 500, 1k, 2k, 4k, 8k, 16k Hz.
    /// </summary>
    public class GraphicEqEffect : IAudioEffect
    {
        public string Name => "Graphic EQ";
        public bool Enabled { get; set; } = true;

        public const int BandCount = 10;

        public static readonly float[] Frequencies =
            { 31.25f, 62.5f, 125f, 250f, 500f, 1000f, 2000f, 4000f, 8000f, 16000f };

        public static readonly string[] Labels =
            { "31", "63", "125", "250", "500", "1k", "2k", "4k", "8k", "16k" };

        /// <summary>Per-band gain in dB. Range: -12 to +12.</summary>
        public float[] Gains { get; } = new float[BandCount];

        /// <summary>Per-band peak level for metering (0..1).</summary>
        public float[] Levels { get; } = new float[BandCount];

        // Biquad filter state per band
        private readonly float[] x1 = new float[BandCount];
        private readonly float[] x2 = new float[BandCount];
        private readonly float[] y1 = new float[BandCount];
        private readonly float[] y2 = new float[BandCount];

        // Biquad coefficients per band
        private readonly float[] cb0 = new float[BandCount];
        private readonly float[] cb1 = new float[BandCount];
        private readonly float[] cb2 = new float[BandCount];
        private readonly float[] ca1 = new float[BandCount];
        private readonly float[] ca2 = new float[BandCount];

        private int lastSampleRate;
        private readonly float[] lastGains = new float[BandCount];

        public GraphicEqEffect()
        {
            // Initialize to passthrough
            for (int i = 0; i < BandCount; i++)
            {
                cb0[i] = 1f;
                lastGains[i] = float.NaN; // Force first update
            }
        }

        private void UpdateCoefficients(int sampleRate)
        {
            for (int i = 0; i < BandCount; i++)
            {
                if (lastSampleRate == sampleRate && lastGains[i] == Gains[i])
                    continue;

                float freq = Frequencies[i];
                float gainDb = Gains[i];
                float Q = 1.41f; // ~1 octave bandwidth

                if (MathF.Abs(gainDb) < 0.05f)
                {
                    // Flat — passthrough
                    cb0[i] = 1f; cb1[i] = 0f; cb2[i] = 0f;
                    ca1[i] = 0f; ca2[i] = 0f;
                }
                else
                {
                    float A = MathF.Pow(10f, gainDb / 40f);
                    float w0 = 2f * MathF.PI * freq / sampleRate;
                    float sinW0 = MathF.Sin(w0);
                    float cosW0 = MathF.Cos(w0);
                    float alpha = sinW0 / (2f * Q);

                    float a0 = 1f + alpha / A;
                    cb0[i] = (1f + alpha * A) / a0;
                    cb1[i] = (-2f * cosW0) / a0;
                    cb2[i] = (1f - alpha * A) / a0;
                    ca1[i] = (-2f * cosW0) / a0;
                    ca2[i] = (1f - alpha / A) / a0;
                }

                lastGains[i] = Gains[i];
            }
            lastSampleRate = sampleRate;
        }

        public void Process(float[] buffer, int count)
        {
            if (!Enabled) return;

            UpdateCoefficients(48000);

            // Reset peak levels
            for (int i = 0; i < BandCount; i++) Levels[i] = 0f;

            for (int n = 0; n < count; n++)
            {
                float sample = buffer[n];

                for (int i = 0; i < BandCount; i++)
                {
                    float output = cb0[i] * sample + cb1[i] * x1[i] + cb2[i] * x2[i]
                                 - ca1[i] * y1[i] - ca2[i] * y2[i];

                    x2[i] = x1[i];
                    x1[i] = sample;
                    y2[i] = y1[i];
                    y1[i] = output;

                    sample = output;

                    float absOut = MathF.Abs(output);
                    if (absOut > Levels[i]) Levels[i] = absOut;
                }

                buffer[n] = Math.Clamp(sample, -1f, 1f);
            }
        }
    }
}