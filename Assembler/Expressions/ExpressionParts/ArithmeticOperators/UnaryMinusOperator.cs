using Konamiman.Nestor80.Assembler.Relocatable;

namespace Konamiman.Nestor80.Assembler.Expressions.ExpressionParts.ArithmeticOperators
{
    internal class UnaryMinusOperator : UnaryOperator
    {
        public static UnaryMinusOperator Instance = new();

        public override int Precedence => 3;

        public override string Name => "u-";

        public override byte? ExtendedLinkItemType => 6;

        protected override Address OperateCore(Address value1, Address value2)
        {
            unchecked
            {
                return new Address(value1.Type, (ushort)-value1.Value);
            }
        }
    }
}
