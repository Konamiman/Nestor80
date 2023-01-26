using Konamiman.Nestor80.Assembler.Relocatable;

namespace Konamiman.Nestor80.Assembler.Expressions.ExpressionParts.ArithmeticOperators
{
    internal class TypeOperator : UnaryOperator
    {
        public static TypeOperator Instance = new();

        public override int Precedence => 0;

        public override string Name => "TYPE";

        //Not actually used
        public override byte ExtendedLinkItemType => 255;

        protected override Address OperateCore(Address value1, Address value2)
            => Address.Absolute((ushort)((byte)value1.Type | 0x20));
    }
}
