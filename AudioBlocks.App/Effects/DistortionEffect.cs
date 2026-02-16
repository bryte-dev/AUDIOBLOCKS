using AudioBlocks.App.Audio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AudioBlocks.App.Effects
{
    public class DistortionEffect : IAudioEffect
    {
        public string Name => "Distortion";
        public bool Enabled { get; set; } = true;

        public float Amount { get; set; } = 0.5f;

        public void Process(float[] buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                float sample = buffer[i] * (1f + Amount * 10f);
                // Clamp entre -1 et 1
                buffer[i] = Math.Clamp(sample, -1f, 1f);
            }
        }
    }
}
