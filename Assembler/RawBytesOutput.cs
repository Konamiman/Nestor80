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
            Length = bytes.Length;
        }

        public int Length { get; }

        public static RawBytesOutput FromBytes(params byte[] bytes) => new(bytes);

        public static RawBytesOutput Empty => new(Array.Empty<byte>());

        public static ushort NumericValueFor(byte[] bytes) =>
            bytes.Length switch {
                0 => 0,
                1 => bytes[0],
                2 => (ushort)(bytes[0] | bytes[1] << 8),
                _ => throw new InvalidOperationException($"Can't convert a byte array of {bytes.Length} elements to a number")
            };

        public ushort NumericValue => NumericValueFor(this.ToArray());

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
