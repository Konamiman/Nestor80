using Konamiman.Nestor80.Assembler.Relocatable;

namespace Konamiman.Nestor80.Assembler.Expressions.ExpressionParts.ArithmeticOperators
{
    internal class ShiftLeftOperator : BinaryOperator
    {
        public static ShiftLeftOperator Instance = new();

        public override int Precedence => 2;

        public override string Name => "SHL";

        public override byte ExtendedLinkItemType => 17;

        protected override Address OperateCore(Address value1, Address value2)
        {
            // The second operator must be absolute
            // <mode> SHL Absolute = <mode>

            if (!value1.IsAbsolute || !value2.IsAbsolute)
            {
                throw new InvalidExpressionException($"SHL: The second operand must be absolute (attempted {value1.Type} SHL {value2.Type})");
            }

            unchecked
            {
                return new Address(value1.Type, (ushort)(value1.Value << value2.Value), value1.CommonBlockName);
            }
        }
    }
}
