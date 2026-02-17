using AudioBlocks.App.Audio;
using System;

namespace AudioBlocks.App.Effects
{
    public class DelayEffect : IAudioEffect
    {
        public string Name => "Delay";
        public bool Enabled { get; set; } = true;

        /// <summary>Delay time: 0..1 maps to 50ms..1000ms</summary>
        public float Time { get; set; } = 0.4f;

        /// <summary>Feedback: 0 = single echo, ~0.95 = long tail</summary>
        public float Feedback { get; set; } = 0.4f;

        /// <summary>Dry/wet mix</summary>
        public float Mix { get; set; } = 0.35f;

        // Buffer large enough for ~2s at 96kHz
        private readonly float[] delayBuf = new float[192000];
        private int writePos;
        // HP filter in feedback loop to prevent mud buildup
        private float fbHpState;
        private int sampleRate = 48000;

        public void Process(float[] buffer, int count)
        {
            // Calculate delay in samples from time parameter and actual sample rate
            float delayMs = 50f + Time * 950f;
            int delaySamples = Math.Clamp((int)(delayMs * 0.001f * sampleRate), 1, delayBuf.Length - 1);

            // HP cutoff in feedback (~80Hz to cut rumble)
            float hpCoeff = 1f - MathF.Exp(-2f * MathF.PI * 80f / sampleRate);

            // Clamp feedback to prevent runaway
            float fb = Math.Clamp(Feedback, 0f, 0.95f);

            for (int i = 0; i < count; i++)
            {
                int readPos = writePos - delaySamples;
                if (readPos < 0) readPos += delayBuf.Length;

                float delayed = delayBuf[readPos];
                float dry = buffer[i];

                // HP filter on feedback to keep it clean
                fbHpState += hpCoeff * (delayed - fbHpState);
                float filteredFb = delayed - fbHpState;

                delayBuf[writePos] = Math.Clamp(dry + filteredFb * fb, -1f, 1f);

                if (++writePos >= delayBuf.Length) writePos = 0;

                buffer[i] = dry * (1f - Mix) + delayed * Mix;
            }
        }
    }
}
