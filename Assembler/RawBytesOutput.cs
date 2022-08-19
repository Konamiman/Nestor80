using System;
namespace Konamiman.Nestor80.Assembler
{
    internal class RawBytesOutput : List<byte>, IAssemblyOutputPart, IExpressionPart
    {
        public RawBytesOutput()
        {
        }

        public RawBytesOutput(byte[] bytes)
        {
            this.AddRange(bytes);
        }
    }
}
