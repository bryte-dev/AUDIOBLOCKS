using AudioBlocks.App.Audio;
using System;

namespace AudioBlocks.App.Effects
{
    public class ChorusEffect : IAudioEffect
    {
        public string Name => "Chorus";
        public bool Enabled { get; set; } = true;

        /// <summary>LFO rate: 0..1 maps to 0.1Hz..5Hz</summary>
        public float Rate { get; set; } = 0.4f;

        /// <summary>Modulation depth: 0..1</summary>
        public float Depth { get; set; } = 0.5f;

        /// <summary>Dry/wet mix</summary>
        public float Mix { get; set; } = 0.5f;

        private readonly float[] delayBuf = new float[4800]; // ~100ms
        private int writePos;
        private double lfoPhase;

        public void Process(float[] buffer, int count)
        {
            double lfoFreq = 0.1 + Rate * 4.9;
            float maxDelay = Depth * 400 + 10; // 10..410 samples

            for (int i = 0; i < count; i++)
            {
                // Write to circular buffer
                delayBuf[writePos] = buffer[i];

                // LFO modulates delay time
                float lfo = (float)(0.5 + 0.5 * Math.Sin(lfoPhase));
                lfoPhase += 2 * Math.PI * lfoFreq / 48000.0;
                if (lfoPhase > 2 * Math.PI) lfoPhase -= 2 * Math.PI;

                float delaySamples = 10 + lfo * maxDelay;

                // Fractional delay with linear interpolation
                float readPosF = writePos - delaySamples;
                if (readPosF < 0) readPosF += delayBuf.Length;

                int idx0 = (int)readPosF;
                int idx1 = (idx0 + 1) % delayBuf.Length;
                float frac = readPosF - idx0;
                idx0 %= delayBuf.Length;

                float delayed = delayBuf[idx0] * (1f - frac) + delayBuf[idx1] * frac;

                buffer[i] = buffer[i] * (1f - Mix) + delayed * Mix;

                if (++writePos >= delayBuf.Length) writePos = 0;
            }
        }
    }
}
