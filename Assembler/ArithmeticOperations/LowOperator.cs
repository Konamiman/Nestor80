namespace Konamiman.Nestor80.Assembler.ArithmeticOperations
{
    internal class LowOperator : ArithmeticOperator
    {
        public override int Precedence => 1;

        public override string Name => "LOW";

        public override bool IsUnary => true;

        public override byte? ExtendedLinkItemType => 4;

        protected override Address OperateCore(Address value1, Address value2)
        {
            return new Address(value1.Type, (ushort)(value1.Value & 0xFF));
        }
    }
}
