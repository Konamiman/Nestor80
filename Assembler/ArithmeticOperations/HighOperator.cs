namespace Konamiman.Nestor80.Assembler.ArithmeticOperations
{
    internal class HighOperator : ArithmeticOperator
    {
        public override int Precedence => 1;

        public override string Name => "HIGH";

        public override bool IsUnary => true;

        public override byte? ExtendedLinkItemType => 3;

        protected override Address OperateCore(Address value1, Address value2)
        {
            return new Address(value1.Type, (ushort)(value1.Value >> 8));
        }
    }
}
