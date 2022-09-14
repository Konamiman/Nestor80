namespace Konamiman.Nestor80.Assembler.Output
{
    public class DefineSpaceLine : ProcessedSourceLine, IChangesLocationCounter
    {
        public AddressType NewLocationArea { get; set; }

        public ushort NewLocationCounter { get; set; }

        public ushort Size { get; set; }

        public byte? Value { get; set; }

        public override string ToString() => $"{base.ToString()}, {Size}, {Value}";
    }
}
