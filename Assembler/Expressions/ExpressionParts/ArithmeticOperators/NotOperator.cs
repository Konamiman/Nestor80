using Konamiman.Nestor80.Assembler.Relocatable;

namespace Konamiman.Nestor80.Assembler.Expressions.ExpressionParts.ArithmeticOperators
{
    internal class NotOperator : UnaryOperator
    {
        public static NotOperator Instance = new();

        public override int Precedence => 6;

        public override string Name => "NOT";

        public override byte? ExtendedLinkItemType => 5;

        protected override Address OperateCore(Address value1, Address value2)
        {
            // The result is of the same type

            unchecked
            {
                return new Address(value1.Type, (ushort)~value1.Value);
            }
        }
    }
}
