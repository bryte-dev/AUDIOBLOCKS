using AudioBlocks.App.Audio;
using System;

namespace AudioBlocks.App.Effects
{
    /// <summary>
    /// Asymmetric fuzz — dark, gated, velcro-like.
    /// Inspired by the silicon Fuzz Face sound on Radiohead's "Exit Music (For a Film)".
    /// </summary>
    public class FuzzEffect : IAudioEffect
    {
        public string Name => "Fuzz";
        public bool Enabled { get; set; } = true;

        /// <summary>Fuzz intensity: 0 = light breakup, 1 = full velcro fuzz</summary>
        public float Fuzz { get; set; } = 0.7f;

        /// <summary>Tone: 0 = very dark (muffled, woolly), 1 = bright (cutting)</summary>
        public float Tone { get; set; } = 0.25f;

        /// <summary>Gate threshold — kills fizzy noise below this level (0..0.1)</summary>
        public float Gate { get; set; } = 0.02f;

        /// <summary>Output volume: 0..1</summary>
        public float Level { get; set; } = 0.6f;

        /// <summary>Mix: 0 = full dry, 1 = full wet</summary>
        public float Mix { get; set; } = 1.0f;

        // Internal state for stereo processing
        private float lpStateL, lpStateR;
        private float gateEnvL, gateEnvR;
        private float dcBlockL, dcBlockR;
        private float dcPrevL, dcPrevR;
        private int sampleRate = 48000;

        public void SetSampleRate(int sr) => sampleRate = sr;

        public void Process(float[] buffer, int count)
        {
            if (!Enabled) return;

            // Fuzz gain: steep curve — gets destroyed fast
                float gain = 1f + Fuzz * Fuzz * 120f;

                // Asymmetry: how much harder the positive half clips (silicon transistor)
                float asymmetry = 0.4f + Fuzz * 0.6f;

                // Tone filter: much darker range than distortion (200Hz–3kHz vs 500Hz–15kHz)
                float toneFreq = 200f + Tone * 2800f;
                float lpCoeff = 1f - MathF.Exp(-2f * MathF.PI * toneFreq / sampleRate);

            // Gate envelope follower speed
            float gateAttack = 0.002f;
            float gateRelease = 0.0001f;

            for (int i = 0; i < count; i += 2)
            {
                // Process Left Channel
                float dryL = buffer[i];
                float wetL = ProcessSample(dryL, ref gateEnvL, ref lpStateL, ref dcPrevL, ref dcBlockL, gain, asymmetry, lpCoeff, gateAttack, gateRelease);
                buffer[i] = Math.Clamp(dryL * (1f - Mix) + wetL * Mix, -1f, 1f);

                // Process Right Channel
                if (i + 1 < count)
                {
                    float dryR = buffer[i + 1];
                    float wetR = ProcessSample(dryR, ref gateEnvR, ref lpStateR, ref dcPrevR, ref dcBlockR, gain, asymmetry, lpCoeff, gateAttack, gateRelease);
                    buffer[i + 1] = Math.Clamp(dryR * (1f - Mix) + wetR * Mix, -1f, 1f);
                }
            }
        }

        private float ProcessSample(float x, ref float gateEnv, ref float lpState, ref float dcPrev, ref float dcBlock, float gain, float asymmetry, float lpCoeff, float gateAttack, float gateRelease)
        {
            // === Input gate — kill signal below threshold (removes fizzy tail) ===
            float absX = MathF.Abs(x);
            if (absX > gateEnv)
                gateEnv += gateAttack * (absX - gateEnv);
            else
                gateEnv += gateRelease * (absX - gateEnv);

            float gateMask = gateEnv > Gate ? 1f : gateEnv / (Gate + 1e-8f);
            gateMask *= gateMask;
            x *= gateMask;

            // === Amplify ===
            x *= gain;

            // === Partial rectification — octave-up artifacts at high fuzz ===
            float rectMix = asymmetry * 0.3f;
            x = x * (1f - rectMix) + MathF.Abs(x) * rectMix;

            // === Hard asymmetric clipping (silicon fuzz — flat-top, not smooth like tanh) ===
            float posClip = 0.8f / (1f + asymmetry);   // positive clips earlier
            float negClip = 0.8f + asymmetry * 0.3f;    // negative clips later

            float wet;
            if (x > posClip)
                wet = posClip;
            else if (x < -negClip)
                wet = -negClip;
            else
                wet = x;

            // Normalize to [-1, 1] range
            wet /= MathF.Max(posClip, negClip);

            // === Tone filter (low-pass for dark woolly sound) ===
            lpState += lpCoeff * (wet - lpState);
            wet = lpState;

            // === DC blocking filter ===
            float dcOut = wet - dcPrev + 0.995f * dcBlock;
            dcPrev = wet;
            dcBlock = dcOut;
            wet = dcOut;

            return wet * Level;
        }
    }
}
