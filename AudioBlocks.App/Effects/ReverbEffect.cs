using AudioBlocks.App.Audio;
using System;

namespace AudioBlocks.App.Effects
{
    public class ReverbEffect : IAudioEffect
    {
        public string Name => "Reverb";
        public bool Enabled { get; set; } = true;

        /// <summary>Dry/Wet mix: 0 = dry, 1 = full reverb</summary>
        public float Mix { get; set; } = 0.25f;

        /// <summary>Decay time: 0 = short, 1 = long tail</summary>
        public float Decay { get; set; } = 0.5f;

        /// <summary>Damping: 0 = bright reflections, 1 = dark/muffled</summary>
        public float Damping { get; set; } = 0.5f;

        // Multi-tap delay for richer reverb (4 taps at prime-ish intervals)
        private readonly float[] buf1 = new float[7919];
        private readonly float[] buf2 = new float[5413];
        private readonly float[] buf3 = new float[3571];
        private readonly float[] buf4 = new float[1907];
        private int pos1, pos2, pos3, pos4;
        private float damp1, damp2, damp3, damp4;

        public void Process(float[] buffer, int count)
        {
            float fb = 0.3f + Decay * 0.55f; // feedback: 0.3..0.85
            float damp = Damping * 0.7f;      // damping coeff: 0..0.7
            float wet = Mix;
            float dry = 1f - Mix;

            for (int i = 0; i < count; i++)
            {
                float input = buffer[i];

                // Read from each delay line
                float r1 = buf1[pos1];
                float r2 = buf2[pos2];
                float r3 = buf3[pos3];
                float r4 = buf4[pos4];

                // Sum reverb taps
                float reverbSum = (r1 + r2 + r3 + r4) * 0.25f;

                // Write back with feedback + damping
                float fb1 = input + r1 * fb;
                damp1 = damp1 * damp + fb1 * (1f - damp);
                buf1[pos1] = Math.Clamp(damp1, -1f, 1f);

                float fb2 = input + r2 * fb;
                damp2 = damp2 * damp + fb2 * (1f - damp);
                buf2[pos2] = Math.Clamp(damp2, -1f, 1f);

                float fb3 = input + r3 * fb;
                damp3 = damp3 * damp + fb3 * (1f - damp);
                buf3[pos3] = Math.Clamp(damp3, -1f, 1f);

                float fb4 = input + r4 * fb;
                damp4 = damp4 * damp + fb4 * (1f - damp);
                buf4[pos4] = Math.Clamp(damp4, -1f, 1f);

                // Advance positions
                if (++pos1 >= buf1.Length) pos1 = 0;
                if (++pos2 >= buf2.Length) pos2 = 0;
                if (++pos3 >= buf3.Length) pos3 = 0;
                if (++pos4 >= buf4.Length) pos4 = 0;

                buffer[i] = Math.Clamp(input * dry + reverbSum * wet, -1f, 1f);
            }
        }
    }
}
