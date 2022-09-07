namespace Konamiman.Nestor80.Assembler.Output
{
    public class AssemblyEndLine : ProcessedSourceLine
    {
        public Address EndAddress { get; set; }

        public override string ToString() => base.ToString() + EndAddress;
    }
}
