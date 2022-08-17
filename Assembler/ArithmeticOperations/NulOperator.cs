namespace Konamiman.Nestor80.Assembler.ArithmeticOperations
{
    internal class NulOperator : ArithmeticOperator
    {
        public override int Precedence => 0;

        public override string Name => "NUL";

        public override bool IsUnary => true;

        protected override Address OperateCore(Address value1, Address value2)
        {
            return value1.Value == 0 ? AbsoluteMinusOne : AbsoluteZero;
        }
    }
}
