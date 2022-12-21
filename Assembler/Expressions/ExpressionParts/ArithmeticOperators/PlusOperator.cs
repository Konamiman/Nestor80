using Konamiman.Nestor80.Assembler.Relocatable;

namespace Konamiman.Nestor80.Assembler.Expressions.ExpressionParts.ArithmeticOperators
{
    internal class PlusOperator : BinaryOperator
    {
        public static PlusOperator Instance = new();

        public override int Precedence => 4;

        public override string Name => "+";

        public override byte? ExtendedLinkItemType => 8;

        protected override Address OperateCore(Address value1, Address value2)
        {
            // At least one of the operands must be Absolute
            // Absolute + <mode> = <mode>

            if (!value1.IsAbsolute && !value2.IsAbsolute)
            {
                throw new InvalidExpressionException($"+: At least one of the operands must be absolute (attempted {value1.Type} + {value2.Type})");
            }

            var type = value1.IsAbsolute ? value2.Type : value1.Type;

            unchecked
            {
                return new Address(type, (ushort)(value1.Value + value2.Value));
            }
        }
    }
}
