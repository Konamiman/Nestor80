using Konamiman.Nestor80.Assembler.Relocatable;

namespace Konamiman.Nestor80.Assembler.Expressions.ExpressionParts.ArithmeticOperators
{
    internal class LessThanOperator : BinaryOperator
    {
        public static LessThanOperator Instance = new();

        public override int Precedence => 5;

        public override string Name => "LT";

        public override byte ExtendedLinkItemType => 20;

        protected override Address OperateCore(Address value1, Address value2)
        {
            // Both addresses must be in the same mode

            if (!value1.SameModeAs(value2))
            {
                throw new InvalidExpressionException($"LT: Both values must be in the same mode (attempted {value1.EffectiveType} LT {value2.EffectiveType})");
            }

            return value1.Value < value2.Value ? AbsoluteMinusOne : AbsoluteZero;
        }
    }
}
