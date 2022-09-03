namespace Konamiman.Nestor80.Assembler.Output
{
    public class BlankLineWithoutLabel : BlankLine
    {
        public BlankLineWithoutLabel(): base(null)
        {
        }

        public static BlankLineWithoutLabel Instance => new();
    }
}
