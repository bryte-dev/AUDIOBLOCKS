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

        /// <summary>Feedback: 0 = single echo, 1 = infinite repeats</summary>
        public float Feedback { get; set; } = 0.4f;

        /// <summary>Dry/wet mix</summary>
        public float Mix { get; set; } = 0.35f;

        private readonly float[] delayBuf = new float[48000]; // max ~1s at 48kHz
        private int writePos;

        public void Process(float[] buffer, int count)
        {
            int delaySamples = Math.Clamp((int)(50 + Time * 950) * 48, 1, delayBuf.Length - 1);
            // Approximate for different sample rates — 48 samples per ms at 48kHz

            for (int i = 0; i < count; i++)
            {
                int readPos = writePos - delaySamples;
                if (readPos < 0) readPos += delayBuf.Length;

                float delayed = delayBuf[readPos];
                float dry = buffer[i];
                float wet = delayed;

                delayBuf[writePos] = Math.Clamp(dry + delayed * Feedback, -1f, 1f);

                if (++writePos >= delayBuf.Length) writePos = 0;

                buffer[i] = dry * (1f - Mix) + wet * Mix;
            }
        }
    }
}
