using AudioBlocks.App.Audio;
using System;

namespace AudioBlocks.App.Effects
{
    public class ReverbEffect : IAudioEffect
    {
        public string Name => "Reverb";
        public bool Enabled { get; set; } = true;

        /// <summary>Dry/Wet mix: 0 = dry, 1 = full reverb</summary>
        public float Mix { get; set; } = 0.25f;

        /// <summary>Decay time: 0 = short, 1 = long tail</summary>
        public float Decay { get; set; } = 0.5f;

        /// <summary>Damping: 0 = bright reflections, 1 = dark/muffled</summary>
        public float Damping { get; set; } = 0.5f;

        // Comb filters (8 delay lines at prime-ish intervals for density)
        private readonly float[] c1 = new float[1557], c2 = new float[1617];
        private readonly float[] c3 = new float[1491], c4 = new float[1422];
        private readonly float[] c5 = new float[1277], c6 = new float[1356];
        private readonly float[] c7 = new float[1188], c8 = new float[1116];
        private int p1, p2, p3, p4, p5, p6, p7, p8;
        private float d1, d2, d3, d4, d5, d6, d7, d8;

        // 4 allpass filters per channel for diffusion (Freeverb standard)
        // Left channel
        private readonly float[] apL1 = new float[556], apL2 = new float[441];
        private readonly float[] apL3 = new float[341], apL4 = new float[225];
        private int apL1Pos, apL2Pos, apL3Pos, apL4Pos;
        // Right channel (sizes offset by +23 for stereo decorrelation)
        private readonly float[] apR1 = new float[579], apR2 = new float[464];
        private readonly float[] apR3 = new float[364], apR4 = new float[248];
        private int apR1Pos, apR2Pos, apR3Pos, apR4Pos;

        // HP filter state to prevent low-end mud
        private float hpStateL, hpStateR;
        private int sampleRate = 48000;

        public void SetSampleRate(int sr) => sampleRate = sr;

        public void Process(float[] buffer, int count)
        {
            float fb = 0.4f + Decay * 0.52f;  // 0.4..0.92
            float damp = Damping * 0.6f;
            float wet = Mix;
            float dry = 1f - Mix;
            const float apCoeff = 0.5f;
            // HP coefficient (~120Hz) to prevent low-end mud
            float hpCoeff = 1f - MathF.Exp(-2f * MathF.PI * 120f / sampleRate);

            for (int i = 0; i < count; i += 2)
            {
                float inL = buffer[i];
                float inR = (i + 1 < count) ? buffer[i + 1] : inL;
                // Mix to mono for comb input
                float input = (inL + inR) * 0.5f;

                // Read from all 8 combs
                float r1 = c1[p1], r2 = c2[p2], r3 = c3[p3], r4 = c4[p4];
                float r5 = c5[p5], r6 = c6[p6], r7 = c7[p7], r8 = c8[p8];

                // Split combs for stereo width: odd → L, even → R
                float combL = (r1 + r3 + r5 + r7) * 0.25f;
                float combR = (r2 + r4 + r6 + r8) * 0.25f;

                // Write back with lowpass damping
                WriteComb(c1, ref p1, ref d1, input, r1, fb, damp);
                WriteComb(c2, ref p2, ref d2, input, r2, fb, damp);
                WriteComb(c3, ref p3, ref d3, input, r3, fb, damp);
                WriteComb(c4, ref p4, ref d4, input, r4, fb, damp);
                WriteComb(c5, ref p5, ref d5, input, r5, fb, damp);
                WriteComb(c6, ref p6, ref d6, input, r6, fb, damp);
                WriteComb(c7, ref p7, ref d7, input, r7, fb, damp);
                WriteComb(c8, ref p8, ref d8, input, r8, fb, damp);

                // HP filter to cut low-end mud buildup
                hpStateL += hpCoeff * (combL - hpStateL);
                combL -= hpStateL;
                hpStateR += hpCoeff * (combR - hpStateR);
                combR -= hpStateR;

                // Left allpass diffusion chain (4 cascaded)
                float diffL = AllPass(apL1, ref apL1Pos, combL, apCoeff);
                diffL = AllPass(apL2, ref apL2Pos, diffL, apCoeff);
                diffL = AllPass(apL3, ref apL3Pos, diffL, apCoeff);
                diffL = AllPass(apL4, ref apL4Pos, diffL, apCoeff);

                // Right allpass diffusion chain (4 cascaded, offset sizes for decorrelation)
                float diffR = AllPass(apR1, ref apR1Pos, combR, apCoeff);
                diffR = AllPass(apR2, ref apR2Pos, diffR, apCoeff);
                diffR = AllPass(apR3, ref apR3Pos, diffR, apCoeff);
                diffR = AllPass(apR4, ref apR4Pos, diffR, apCoeff);

                // Soft limit reverb output to prevent harsh clipping
                diffL = MathF.Tanh(diffL);
                diffR = MathF.Tanh(diffR);

                buffer[i] = Math.Clamp(inL * dry + diffL * wet, -1f, 1f);
                if (i + 1 < count)
                    buffer[i + 1] = Math.Clamp(inR * dry + diffR * wet, -1f, 1f);
            }
        }

        private static void WriteComb(float[] buf, ref int pos, ref float dampState,
            float input, float readVal, float fb, float damp)
        {
            float fbSig = input + readVal * fb;
            dampState = dampState * damp + fbSig * (1f - damp);
            buf[pos] = Math.Clamp(dampState, -1f, 1f);
            if (++pos >= buf.Length) pos = 0;
        }

        private static float AllPass(float[] buf, ref int pos, float input, float coeff)
        {
            float delayed = buf[pos];
            float output = -input + delayed;
            buf[pos] = Math.Clamp(input + delayed * coeff, -1f, 1f);
            if (++pos >= buf.Length) pos = 0;
            return output;
        }
    }
}
