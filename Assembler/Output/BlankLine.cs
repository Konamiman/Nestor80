namespace Konamiman.Nestor80.Assembler.Output
{
    public class BlankLine : IProcessedSourceLine
    {
        private BlankLine()
        {
        }

        public static BlankLine Instance => new();

        public string Line => "";

        public int EffectiveLineLength => 0;
    }
}
