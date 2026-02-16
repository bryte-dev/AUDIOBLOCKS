using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace AudioBlocks.App.Audio
{
    public class SineWaveProvider : ISampleProvider
    {
        private readonly WaveFormat waveFormat;
        private double phase;

        public float Frequency { get; set; } = 100f;
        public float Amplitude { get; set; } = 0.8f;

        public SineWaveProvider(int sampleRate = 44100)
        {
            waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
        }

        public WaveFormat WaveFormat => waveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            for (int n = 0; n < count; n++)
            {
                buffer[offset + n] =
                    (float)(Amplitude * Math.Sin(phase));

                phase += 2 * Math.PI * Frequency / waveFormat.SampleRate;

                if (phase > 2 * Math.PI)
                    phase -= 2 * Math.PI;
            }

            return count;
        }
    }
}
