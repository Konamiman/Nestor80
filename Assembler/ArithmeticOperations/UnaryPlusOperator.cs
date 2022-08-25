namespace Konamiman.Nestor80.Assembler.ArithmeticOperations
{
    internal class UnaryPlusOperator : UnaryOperator
    {
        public static UnaryPlusOperator Instance = new();

        public override int Precedence => 3;

        public override string Name => "u+";

        protected override Address OperateCore(Address value1, Address value2)
        {
            return value1;
        }
    }
}
