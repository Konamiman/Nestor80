using Konamiman.Nestor80.Assembler.Relocatable;

namespace Konamiman.Nestor80.Assembler.Expressions.ExpressionParts.ArithmeticOperators
{
    internal class DivideOperator : BinaryOperator
    {
        public static DivideOperator Instance = new();

        public override int Precedence => 2;

        public override string Name => "/";

        public override byte ExtendedLinkItemType => 10;

        protected override Address OperateCore(Address value1, Address value2)
        {
            // The second operator must be absolute
            // <mode> / Absolute = <mode>

            if (!value2.IsAbsolute)
            {
                throw new InvalidExpressionException($"/: The second operand must be absolute (attempted {value1.Type} / {value2.Type})");
            }

            unchecked
            {
                return new Address(value1.Type, (ushort)(value1.Value / value2.Value), value1.CommonBlockName);
            }
        }
    }
}
