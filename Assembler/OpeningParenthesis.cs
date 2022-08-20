namespace Konamiman.Nestor80.Assembler
{
    internal class OpeningParenthesis : IExpressionPart
    {
        private OpeningParenthesis()
        {
        }

        public static OpeningParenthesis Value => new();

        public override string ToString() => "(";
    }
}
