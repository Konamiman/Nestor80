namespace Konamiman.Nestor80.Assembler.Output
{
    public class PhaseLine : ProcessedSourceLine
    {
        public ushort Address { get; set; }

        public override string ToString() => $"{base.ToString()} {Address:X4}";
    }
}
