using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MoreVoiceLines
{
    class AutoDisposeWaveProvider : IWaveProvider
    {
        protected IWaveProvider source;
        protected bool isDisposed = false;

        public AutoDisposeWaveProvider(IWaveProvider source)
        {
            this.source = source;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            if (isDisposed)
                return 0;
            int read = source.Read(buffer, offset, count);
            if (read == 0)
            {
                if (source is IDisposable disposable) {
                    disposable.Dispose();
                }
                isDisposed = true;
            }
            return read;
        }

        public WaveFormat WaveFormat {
            get { return source.WaveFormat; }
        }
    }
}
