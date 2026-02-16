using AudioBlocks.App.Audio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AudioBlocks.App.Effects
{
    public class GainEffect : IAudioEffect
    {
        public string Name => "Gain";
        public bool Enabled { get; set; } = true;

        public float Gain { get; set; } = 1.0f;

        public void Process(float[] buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] *= Gain;
            }
        }
    }
}
