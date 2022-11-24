
namespace Konamiman.Nestor80.Assembler.ArithmeticOperations
{
    internal class XorOperator : BinaryOperator
    {
        public static XorOperator Instance = new();

        public override int Precedence => 8;

        public override string Name => "XOR";

        protected override Address OperateCore(Address value1, Address value2)
        {
            // At least one of the operands must be Absolute
            // Absolute XOR <mode> = <mode>

            if(!value1.IsAbsolute && !value2.IsAbsolute) {
                throw new InvalidExpressionException($"XOR: At least one of the operands must be absolute (attempted {value1.Type} XOR {value2.Type})");
            }

            var type = value1.IsAbsolute ? value2.Type : value1.Type;

            return new Address(type, (ushort)(value1.Value ^ value2.Value));
        }
    }
}
