using AudioBlocks.App.Audio;
using System;

namespace AudioBlocks.App.Effects
{
    public class NoiseGateEffect : IAudioEffect
    {
        public string Name => "Noise Gate";
        public bool Enabled { get; set; } = true;

        /// <summary>Threshold below which signal is muted (0..1)</summary>
        public float Threshold { get; set; } = 0.02f;

        /// <summary>Attack: how fast gate opens (0=instant, 1=slow)</summary>
        public float Attack { get; set; } = 0.1f;

        /// <summary>Release: how fast gate closes (0=instant, 1=slow)</summary>
        public float Release { get; set; } = 0.4f;

        private float gateGain;

        public void Process(float[] buffer, int count)
        {
            float attackCoeff = 1f - (float)Math.Exp(-1.0 / (1 + Attack * 200));
            float releaseCoeff = 1f - (float)Math.Exp(-1.0 / (1 + Release * 1000));

            for (int i = 0; i < count; i++)
            {
                float abs = Math.Abs(buffer[i]);
                float target = abs > Threshold ? 1f : 0f;

                if (target > gateGain)
                    gateGain += attackCoeff * (target - gateGain);
                else
                    gateGain += releaseCoeff * (target - gateGain);

                buffer[i] *= gateGain;
            }
        }
    }
}
