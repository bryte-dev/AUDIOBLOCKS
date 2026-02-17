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
    }
}
