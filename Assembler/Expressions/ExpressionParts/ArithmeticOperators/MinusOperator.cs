using Konamiman.Nestor80.Assembler.Relocatable;

namespace Konamiman.Nestor80.Assembler.Expressions.ExpressionParts.ArithmeticOperators
{
    internal class MinusOperator : BinaryOperator
    {
        public static MinusOperator Instance = new();

        public override int Precedence => 4;

        public override string Name => "-";

        public override byte? ExtendedLinkItemType => 7;

        protected override Address OperateCore(Address value1, Address value2)
        {
            // <mode> - Absolute = <mode>
            // <mode> - <mode> = Absolute, where the two <mode>s are the same

            if (!value2.IsAbsolute && value1.Type != value2.Type)
            {
                throw new InvalidExpressionException($"-: Both operand modes must be the same or the second operand must be absolute (attempted {value1.Type} - {value2.Type})");
            }

            var type = value2.IsAbsolute ? value1.Type : AddressType.ASEG;

            unchecked
            {
                return new Address(type, (ushort)(value1.Value - value2.Value));
            }
        }
    }
}
