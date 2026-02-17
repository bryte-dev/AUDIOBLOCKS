using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AudioBlocks.App.Audio
{
    public interface IAudioEffect
    {
        string Name { get; }
        bool Enabled { get; set; }
        void Process(float[] buffer, int count);

        /// <summary>
        /// Current gain reduction in dB (0 = no reduction, negative = reducing).
        /// Used for metering. Effects that don't reduce gain return 0.
        /// </summary>
        float GainReductionDb => 0f;
    }
}
