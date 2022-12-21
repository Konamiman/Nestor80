namespace Konamiman.Nestor80.Assembler.Output
{
    public class ChangeAreaLine : ProcessedSourceLine, IChangesLocationCounter
    {
        public AddressType NewLocationArea { get; set; }
        public ushort NewLocationCounter { get; set; }

        public string CommonBlockName { get; set; }

        public override string ToString() => $"{base.ToString()}, {NewLocationArea} {NewLocationCounter} {CommonBlockName}";
    }
}
