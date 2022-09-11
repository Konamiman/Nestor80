namespace Konamiman.Nestor80.Assembler.Output
{
    public class DefineSpaceLine : ProcessedSourceLine, IChangesLocationCounter
    {
        public ushort Size { get; set; }

        public byte? Value { get; set; }

        public Address NewLocationCounter { get; set; }

        public override string ToString() => $"{base.ToString()}, {Size}, {Value}";
    }
}
