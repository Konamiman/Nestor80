namespace Konamiman.Nestor80.Assembler.Output
{
    public class BlankLineWithoutLabel : BlankLine
    {
        private BlankLineWithoutLabel(): base()
        {
            Line = null;
            EffectiveLineLength = 0;
        }

        public static BlankLineWithoutLabel Instance => new();

        public override string ToString() => "(blank)";
    }
}
