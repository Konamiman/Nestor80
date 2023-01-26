using Konamiman.Nestor80.Assembler.Relocatable;

namespace Konamiman.Nestor80.Assembler.Expressions.ExpressionParts.ArithmeticOperators
{
    internal class ShiftRightOperator : BinaryOperator
    {
        public static ShiftRightOperator Instance = new();

        public override int Precedence => 2;

        public override string Name => "SHR";

        public override byte ExtendedLinkItemType => 16;

        protected override Address OperateCore(Address value1, Address value2)
        {
            // The second operator must be absolute
            // <mode> SHR Absolute = <mode>

            if (!value2.IsAbsolute)
            {
                throw new InvalidExpressionException($"SHR: The second operand must be absolute (attempted {value1.Type} SHR {value2.Type})");
            }

            unchecked
            {
                return new Address(value1.Type, (ushort)(value1.Value >> value2.Value));
            }
        }
    }
}
