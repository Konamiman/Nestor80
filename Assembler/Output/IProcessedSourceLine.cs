namespace Konamiman.Nestor80.Assembler.Output
{
    public interface IProcessedSourceLine
    {
        public string Line { get; }

        public int EffectiveLineLength { get; }
    }
}
