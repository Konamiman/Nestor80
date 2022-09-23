namespace Konamiman.Nestor80.Assembler.Output
{
    public class PhaseLine : ProcessedSourceLine, IChangesLocationCounter
    {
        public ushort Address { get; set; }
        public AddressType NewLocationArea { get; set; }
        public ushort NewLocationCounter { get; set; }

        public override string ToString() => $"{base.ToString()} {NewLocationCounter:X4}";
    }
}
