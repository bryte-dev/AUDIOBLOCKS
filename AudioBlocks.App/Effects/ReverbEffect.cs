using AudioBlocks.App.Audio;
using System;

namespace AudioBlocks.App.Effects
{
    public class ReverbEffect : IAudioEffect
    {
        public string Name => "Reverb";
        public bool Enabled { get; set; } = true;

        // 0 = dry, 1 = fully wet
        public float Mix { get; set; } = 0.25f;

        // buffer large pour supporter différents sample rates (1s @ 96k)
        private readonly float[] delayBuffer = new float[96000];
        private int writePos = 0;

        // paramètres simples: délai et feedback/damping
        private readonly int delaySamples = 22050; // ~500ms @44.1k (approx)
        private readonly float feedback = 0.45f;
        private readonly float damping = 0.6f;
        private float dampingState = 0f;

        public ReverbEffect() { }

        public void Process(float[] buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                float dry = buffer[i];

                // lecture à distance (delaySamples derrière la position écriture)
                int readPos = writePos - delaySamples;
                if (readPos < 0) readPos += delayBuffer.Length;

                float delayed = delayBuffer[readPos];

                // sortie mix dry/wet
                float outSample = dry * (1f - Mix) + delayed * Mix;

                // calcule feedback avec un simple filtre passe-bas (damping) pour atténuer les hautes fréquences
                float newFeedback = dry + delayed * feedback;
                dampingState = dampingState * damping + newFeedback * (1f - damping);

                // écriture dans le buffer en veillant à clamp pour éviter clipping/crackle
                delayBuffer[writePos] = Math.Clamp(dampingState, -1f, 1f);

                // avance du pointeur
                writePos++;
                if (writePos >= delayBuffer.Length) writePos = 0;

                // écriture finale (clamp pour sécurité)
                buffer[i] = Math.Clamp(outSample, -1f, 1f);
            }
        }
    }
}
