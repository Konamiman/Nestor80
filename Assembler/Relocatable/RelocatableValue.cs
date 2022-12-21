namespace Konamiman.Nestor80.Assembler.Relocatable
{
    /// <summary>
    /// Represents a relocatable value as defined by the Link80 relocatable file format.
    /// </summary>
    public class RelocatableValue : RelocatableOutputPart
    {
        public AddressType Type { get; set; }

        public ushort Value { get; set; }

        public override string ToString() => $"{base.ToString()}, {Type} {Value:X4}";
    }
}
