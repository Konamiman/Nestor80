namespace Konamiman.Nestor80.Assembler.Output
{
    public class BlankLine : ProcessedSourceLine
    {
        private BlankLine() : base("", 0)
        {
        }

        public static BlankLine Instance => new();
    }
}
