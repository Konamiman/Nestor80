namespace Konamiman.Nestor80.Assembler
{
    internal class ClosingParenthesis : IExpressionPart
    {
        private ClosingParenthesis()
        {
        }

        public static ClosingParenthesis Value => new();

        public override string ToString() => ")";

        public static bool operator ==(ClosingParenthesis parent1, ClosingParenthesis parent2)
        {
            if(parent1 is null)
                return parent2 is null;

            return parent2.Equals(parent2);
        }

        public static bool operator !=(ClosingParenthesis parent1, ClosingParenthesis parent2)
        {
            return !(parent1 == parent2);
        }

        public override bool Equals(object obj)
        {
            return obj != null && obj is ClosingParenthesis;
        }

        public override int GetHashCode() => Value.GetHashCode();
    }
}
