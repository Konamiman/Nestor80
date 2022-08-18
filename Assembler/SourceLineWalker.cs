using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Konamiman.Nestor80.Assembler
{
    internal class SourceLineWalker
    {
        static byte[] terminatorByte = new byte[] {10};
        private byte[] lineBytes;
        private Encoding lineEncoding;

        public SourceLineWalker(byte[] lineBytes, Encoding lineEncoding)
        {
            this.lineBytes = lineBytes.Concat(terminatorByte).ToArray();
            this.lineEncoding = lineEncoding;
        }
    }
}
