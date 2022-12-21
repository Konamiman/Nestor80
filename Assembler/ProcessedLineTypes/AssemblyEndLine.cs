namespace Konamiman.Nestor80.Assembler.Output
{
    public class AssemblyEndLine : ProcessedSourceLine
    {
        public ushort EndAddress { get; set; }

        public AddressType EndAddressArea { get; set; }

        public override string ToString() => base.ToString() + EndAddress;
    }
}
