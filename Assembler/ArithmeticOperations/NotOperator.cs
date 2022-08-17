namespace Konamiman.Nestor80.Assembler.ArithmeticOperations
{
    internal class NotOperator : ArithmeticOperator
    {
        public override int Precedence => 6;

        public override string Name => "NOT";

        public override bool IsUnary => true;

        public override byte? ExtendedLinkItemType => 5;

        protected override Address OperateCore(Address value1, Address value2)
        {
            // NOT: The result is of the same type

            unchecked {
                return new Address(value1.Type, (ushort)~value1.Value);
            }
        }
    }
}
