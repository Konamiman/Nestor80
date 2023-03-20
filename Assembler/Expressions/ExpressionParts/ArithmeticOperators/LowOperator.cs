using Konamiman.Nestor80.Assembler.Relocatable;

namespace Konamiman.Nestor80.Assembler.Expressions.ExpressionParts.ArithmeticOperators
{
    internal class LowOperator : UnaryOperator
    {
        public static LowOperator Instance = new();

        public override int Precedence => 1;

        public override string Name => "LOW";

        public override byte ExtendedLinkItemType => 4;

        protected override Address OperateCore(Address value1, Address value2)
        {
            return new Address(value1.Type, (ushort)(value1.Value & 0xFF), value1.CommonBlockName);
        }
    }
}
