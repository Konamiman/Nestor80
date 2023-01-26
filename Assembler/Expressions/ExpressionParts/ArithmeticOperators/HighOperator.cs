using Konamiman.Nestor80.Assembler.Relocatable;

namespace Konamiman.Nestor80.Assembler.Expressions.ExpressionParts.ArithmeticOperators
{
    internal class HighOperator : UnaryOperator
    {
        public static HighOperator Instance = new();

        public override int Precedence => 1;

        public override string Name => "HIGH";

        public override byte ExtendedLinkItemType => 3;

        protected override Address OperateCore(Address value1, Address value2)
        {
            return new Address(value1.Type, (ushort)(value1.Value >> 8));
        }
    }
}
