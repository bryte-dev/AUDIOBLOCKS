using AudioBlocks.App.Audio;
using System;

namespace AudioBlocks.App.Effects
{
    public class NoiseGateEffect : IAudioEffect
    {
        public string Name => "Noise Gate";
        public bool Enabled { get; set; } = true;

        /// <summary>Threshold below which signal is muted (0..0.2 linear)</summary>
        public float Threshold { get; set; } = 0.02f;

        /// <summary>Attack: how fast gate opens (0=instant, 1=slow)</summary>
        public float Attack { get; set; } = 0.1f;

        /// <summary>Release: how fast gate closes (0=instant, 1=slow)</summary>
        public float Release { get; set; } = 0.4f;

        /// <summary>Hold time before gate starts closing (ms)</summary>
        public float HoldMs { get; set; } = 20f;

        /// <summary>Hysteresis: gate re-opens at Threshold, closes at Threshold * (1 - hysteresis)</summary>
        private const float Hysteresis = 0.4f;

        private float gateGain;
        private bool gateOpen;
        private int holdCounter;
        private float envelope;
        private int sampleRate = 48000;

        /// <summary>Current gate gain (0=closed, 1=open). Read from UI for metering.</summary>
        public float CurrentGateGain => gateGain;

        public float GainReductionDb => gateGain <= 0.001f ? -96f : 20f * MathF.Log10(gateGain);

        public void Process(float[] buffer, int count)
        {
            // Real time constants
            float attackMs = 0.05f + Attack * 10f;    // 0.05ms to 10ms
            float releaseMs = 5f + Release * 500f;     // 5ms to 505ms

            float attackCoeff = 1f - MathF.Exp(-1f / (attackMs * 0.001f * sampleRate));
            float releaseCoeff = 1f - MathF.Exp(-1f / (releaseMs * 0.001f * sampleRate));

            float openThresh = Threshold;
            float closeThresh = Threshold * (1f - Hysteresis);
            int holdSamples = (int)(HoldMs * 0.001f * sampleRate);

            // Envelope detection coefficient (~2ms smoothing)
            float envCoeff = 1f - MathF.Exp(-1f / (0.002f * sampleRate));

            for (int i = 0; i < count; i++)
            {
                float abs = MathF.Abs(buffer[i]);

                // Smooth envelope follower for detection (avoids chatter)
                envelope += envCoeff * (abs - envelope);

                // Gate state machine with hysteresis
                if (!gateOpen && envelope > openThresh)
                {
                    gateOpen = true;
                    holdCounter = holdSamples;
                }
                else if (gateOpen && envelope < closeThresh)
                {
                    if (holdCounter > 0)
                        holdCounter--;
                    else
                        gateOpen = false;
                }
                else if (gateOpen)
                {
                    holdCounter = holdSamples;
                }

                float target = gateOpen ? 1f : 0f;

                // Smooth gain transitions
                if (target > gateGain)
                    gateGain += attackCoeff * (target - gateGain);
                else
                    gateGain += releaseCoeff * (target - gateGain);

                buffer[i] *= gateGain;
            }
        }
    }
}
