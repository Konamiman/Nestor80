
namespace Konamiman.Nestor80.Assembler.ArithmeticOperations
{
    internal class XorOperator : BinaryOperator
    {
        public static XorOperator Instance = new();

        public override int Precedence => 8;

        public override string Name => "XOR";

        protected override Address OperateCore(Address value1, Address value2)
        {
            // Both addresses must be in absolute mode

            if(!value1.IsAbsolute || !value2.IsAbsolute) {
                throw new InvalidExpressionException($"XOR: Both operands must be in absolute mode (attempted {value1.Type} XOR {value2.Type}");
            }

            return new Address(AddressType.ASEG, (ushort)(value1.Value ^ value2.Value));
        }
    }
}
