using System.IO;

namespace Konamiman.Nestor80.Assembler
{
    internal class OpeningParenthesis : IExpressionPart
    {
        private OpeningParenthesis()
        {
        }

        public static OpeningParenthesis Value => new();

        public override string ToString() => "(";

        public static bool operator ==(OpeningParenthesis parent1, OpeningParenthesis parent2)
        {
            if(parent1 is null)
                return parent2 is null;

            return parent2.Equals(parent2);
        }

        public static bool operator !=(OpeningParenthesis parent1, OpeningParenthesis parent2)
        {
            return !(parent1 == parent2);
        }

        public override bool Equals(object obj)
        {
            return obj != null && obj is OpeningParenthesis;
        }

        public override int GetHashCode() => Value.GetHashCode();
    }
}
