namespace Konamiman.Nestor80
{
    public class RelocatableAddress : RelocatableOutputPart
    {
        public AddressType Type { get; set; }

        public ushort Value { get; set; }

        public override string ToString() => $"{base.ToString()}, {Type} {Value:X4}";
    }
}
