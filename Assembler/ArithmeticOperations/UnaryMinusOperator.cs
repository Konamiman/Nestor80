namespace Konamiman.Nestor80.Assembler.ArithmeticOperations
{
    internal class UnaryMinusOperator : ArithmeticOperator
    {
        public override int Precedence => 3;

        public override string Name => "u-";

        public override bool IsUnary => true;

        public override byte? ExtendedLinkItemType => 6;

        protected override Address OperateCore(Address value1, Address value2)
        {
            unchecked {
                return new Address(value1.Type, (ushort)(-value1.Value));
            }
        }
    }
}
