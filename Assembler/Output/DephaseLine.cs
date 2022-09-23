namespace Konamiman.Nestor80.Assembler.Output
{
    public class DephaseLine : ProcessedSourceLine, IChangesLocationCounter
    {
        public AddressType NewLocationArea { get; set; }
        public ushort NewLocationCounter { get; set; }

        public override string ToString() => $"{base.ToString()}, back to {NewLocationArea} {NewLocationCounter:X4}";
    }
}
