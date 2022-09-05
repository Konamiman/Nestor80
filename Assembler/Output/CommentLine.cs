namespace Konamiman.Nestor80.Assembler.Output
{
    internal class CommentLine : ProcessedSourceLine
    {
        public CommentLine(string line, int effectiveSize, string label = null): base(line, effectiveSize, label)
        {
        }

        public override string ToString() => base.ToString() + "(comment)";
    }
}
