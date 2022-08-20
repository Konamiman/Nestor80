namespace Konamiman.Nestor80.Assembler
{
    internal class ClosingParenthesis
    {
        private ClosingParenthesis()
        {
        }

        public static ClosingParenthesis Value => new();

        public override string ToString() => ")";
    }
}
