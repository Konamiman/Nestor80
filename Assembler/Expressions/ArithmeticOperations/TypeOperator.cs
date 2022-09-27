using Konamiman.Nestor80.Assembler.ArithmeticOperations;

namespace Konamiman.Nestor80.Assembler.Expressions.ArithmeticOperations
{
    internal class TypeOperator : UnaryOperator
    {
        public static TypeOperator Instance = new();

        public override int Precedence => 0;

        public override string Name => "TYPE";

        protected override Address OperateCore(Address value1, Address value2)
            => Address.Absolute((ushort)((byte)value1.Type | 0x20));
    }
}
