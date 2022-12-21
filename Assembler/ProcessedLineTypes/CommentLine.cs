namespace Konamiman.Nestor80.Assembler.Output
{
    internal class CommentLine : ProcessedSourceLine
    {
        public override string ToString() => base.ToString() + "(comment)";
    }
}
