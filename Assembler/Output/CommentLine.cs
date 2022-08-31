namespace Konamiman.Nestor80.Assembler.Output
{
    internal class CommentLine : IProcessedSourceLine
    {
        public string Line { get; init; }

        public int EffectiveLineLength { get; init; }
    }
}
