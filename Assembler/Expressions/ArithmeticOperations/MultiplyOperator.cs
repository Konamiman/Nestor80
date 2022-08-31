namespace Konamiman.Nestor80.Assembler.ArithmeticOperations
{
    internal class MultiplyOperator : BinaryOperator
    {
        public static MultiplyOperator Instance = new();

        public override int Precedence => 2;

        public override string Name => "*";

        public override byte? ExtendedLinkItemType => 9;

        protected override Address OperateCore(Address value1, Address value2)
        {
            // One of the operands must be absolute
            // <mode1> * <mode2> = <mode2>

            if(!value1.IsAbsolute || !value2.IsAbsolute) {
                throw new InvalidOperationException($"*: One of the operands must be is absolute (attempted {value1.Type} * {value2.Type}");
            }

            unchecked {
                return new Address(value2.Type, (ushort)(value1.Value * value2.Value));
            }
        }
    }
}
