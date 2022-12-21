namespace Konamiman.Nestor80.Assembler.Output
{
    internal class RelabLine : ProcessedSourceLine
    {
        public bool Enable { get; set; }

        public override string ToString() => base.ToString() + (Enable ? " enable" : " disable");
    }
}
