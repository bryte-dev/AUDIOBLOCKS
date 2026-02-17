using AudioBlocks.App.Audio;
using System;

namespace AudioBlocks.App.Effects
{
    /// <summary>
    /// Gain/boost effect for the chain. Separate from master volume.
    /// Can be used as a boost pedal before distortion, etc.
    /// </summary>
    public class GainEffect : IAudioEffect
    {
        public string Name => "Gain Boost";
        public bool Enabled { get; set; } = true;

        /// <summary>Gain multiplier: 0 = silence, 1 = unity, 4 = +12dB boost</summary>
        public float Gain { get; set; } = 1.0f;

        public float GainDb
        {
            get => Gain <= 0 ? -96f : 20f * (float)Math.Log10(Gain);
            set => Gain = (float)Math.Pow(10, value / 20f);
        }

        public void Process(float[] buffer, int count)
        {
            float g = Gain;
            for (int i = 0; i < count; i++)
                buffer[i] *= g;
        }
    }
}
