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

        // Buffer large enough for ~100ms at 96kHz
        private readonly float[] delayBuf = new float[9600];
        private int writePos;
        private double lfoPhase;
        private int sampleRate = 48000;

        public void Process(float[] buffer, int count)
        {
            double lfoFreq = 0.1 + Rate * 4.9;
            // Scale max delay with sample rate (~8ms max)
            float maxDelay = Depth * (sampleRate * 0.008f) + (sampleRate * 0.0002f);

            double phaseInc = 2.0 * Math.PI * lfoFreq / sampleRate;

            for (int i = 0; i < count; i++)
            {
                delayBuf[writePos] = buffer[i];

                float lfo = (float)(0.5 + 0.5 * Math.Sin(lfoPhase));
                lfoPhase += phaseInc;
                if (lfoPhase > 2 * Math.PI) lfoPhase -= 2 * Math.PI;

                float delaySamples = (sampleRate * 0.0002f) + lfo * maxDelay;

                // Cubic interpolation for cleaner modulation
                float readPosF = writePos - delaySamples;
                if (readPosF < 0) readPosF += delayBuf.Length;

                int idx0 = ((int)readPosF - 1 + delayBuf.Length) % delayBuf.Length;
                int idx1 = (int)readPosF % delayBuf.Length;
                int idx2 = (idx1 + 1) % delayBuf.Length;
                int idx3 = (idx1 + 2) % delayBuf.Length;
                float frac = readPosF - (int)readPosF;

                // Hermite interpolation
                float s0 = delayBuf[idx0], s1 = delayBuf[idx1];
                float s2 = delayBuf[idx2], s3 = delayBuf[idx3];
                float c0 = s1;
                float c1 = 0.5f * (s2 - s0);
                float c2 = s0 - 2.5f * s1 + 2f * s2 - 0.5f * s3;
                float c3 = 0.5f * (s3 - s0) + 1.5f * (s1 - s2);
                float delayed = ((c3 * frac + c2) * frac + c1) * frac + c0;

                buffer[i] = buffer[i] * (1f - Mix) + delayed * Mix;

                if (++writePos >= delayBuf.Length) writePos = 0;
            }
        }
    }
}
