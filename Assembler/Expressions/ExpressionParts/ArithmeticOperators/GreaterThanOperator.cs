using Konamiman.Nestor80.Assembler.Relocatable;

namespace Konamiman.Nestor80.Assembler.Expressions.ExpressionParts.ArithmeticOperators
{
    internal class GreaterThanOperator : BinaryOperator
    {
        public static GreaterThanOperator Instance = new();

        public override int Precedence => 5;

        public override string Name => "GT";

        public override byte ExtendedLinkItemType => 22;

        protected override Address OperateCore(Address value1, Address value2)
        {
            // Both addresses must be in the same mode

            if (!value1.SameModeAs(value2))
            {
                throw new InvalidExpressionException($"GT: Both values must be in the same mode (attempted {value1.EffectiveType} GT {value2.EffectiveType})");
            }

            return value1.Value > value2.Value ? AbsoluteMinusOne : AbsoluteZero;
        }
    }
}
