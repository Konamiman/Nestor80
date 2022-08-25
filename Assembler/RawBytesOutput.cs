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

        public static RawBytesOutput FromBytes(params byte[] bytes) => new(bytes);

        public static bool operator ==(RawBytesOutput output1, RawBytesOutput output2)
        {
            if(output2 is not RawBytesOutput)
                return false;

            if(output1 is null)
                return output2 is null;

            return output1.Equals(output2);
        }

        public static bool operator !=(RawBytesOutput output1, RawBytesOutput output2)
        {
            return !(output1 == output2);
        }

        public override bool Equals(object obj)
        {
            if(obj == null || GetType() != obj.GetType())
                return false;

            var b2 = (RawBytesOutput)obj;

            return this.OrderBy(x => x).SequenceEqual(b2.OrderBy(x => x));
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override string ToString()
        {
            return string.Join(", ", this.Select(n => $"{n:X2}"));
        }
    }
}
