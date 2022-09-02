namespace Konamiman.Nestor80.Assembler.Output
{
    public abstract class ProcessedSourceLine
    {
        public ProcessedSourceLine(string line, int effectiveLength)
        {
            this.Line = line;
            this.EffectiveLineLength = effectiveLength;
        }

        public string Line { get; }

        public int EffectiveLineLength { get; }
    }
}
