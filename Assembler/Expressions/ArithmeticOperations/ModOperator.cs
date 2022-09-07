namespace Konamiman.Nestor80.Assembler.ArithmeticOperations
{
    internal class ModOperator : BinaryOperator
    {
        public static ModOperator Instance = new();

        public override int Precedence => 2;

        public override string Name => "MOD";

        public override byte? ExtendedLinkItemType => 11;

        protected override Address OperateCore(Address value1, Address value2)
        {
            // One of the operands must be absolute
            // <mode> MOD Absolute = <mode>
            // Absolute MOD <mode> = <mode>

            if(!value1.IsAbsolute && !value2.IsAbsolute) {
                throw new InvalidExpressionException($"MOD: One of the operands must be absolute (attempted {value1.Type} MOD {value2.Type}");
            }

            var type = value1.IsAbsolute ? value2.Type : value1.Type;

            unchecked {
                return new Address(type, (ushort)(value1.Value % value2.Value));
            }
        }
    }
}
