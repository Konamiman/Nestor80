namespace Konamiman.Nestor80.Assembler.Output
{
    public class AssemblyEndLine : ProcessedSourceLine
    {
        public AssemblyEndLine(string line, int effectiveLength = 0, string label = null, string operand = null, Address endAddress = null) : base(line, effectiveLength, label, operand)
        {
            this.EndAddress = endAddress;
        }

        public Address EndAddress { get; }

        public override string ToString() => base.ToString() + EndAddress;
    }
}
