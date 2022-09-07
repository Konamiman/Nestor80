namespace Konamiman.Nestor80.Assembler.ArithmeticOperations
{
    internal class AndOperator : BinaryOperator
    {
        public static AndOperator Instance = new();

        public override int Precedence => 7;

        public override string Name => "AND";

        protected override Address OperateCore(Address value1, Address value2)
        {
            // Both addresses must be in absolute mode

            if(!value1.IsAbsolute || !value2.IsAbsolute) {
                throw new InvalidExpressionException($"AND: Both operands must be in absolute mode (attempted {value1.Type} AND {value2.Type}");
            }

            return new Address(AddressType.ASEG, (ushort)(value1.Value & value2.Value));
        }
    }
}
