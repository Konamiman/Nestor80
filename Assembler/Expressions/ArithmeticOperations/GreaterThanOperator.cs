namespace Konamiman.Nestor80.Assembler.ArithmeticOperations
{
    internal class GreaterThanOperator : BinaryOperator
    {
        public static GreaterThanOperator Instance = new();

        public override int Precedence => 5;

        public override string Name => "GT";

        protected override Address OperateCore(Address value1, Address value2)
        {
            // Both addresses must be in the same mode

            if(!value1.SameModeAs(value2)) {
                throw new InvalidOperationException($"GT: Both addresses must be in the same mode (attempted {value1.Type} GT {value2.Type}");
            }

            return value1.Value > value2.Value ? AbsoluteMinusOne : AbsoluteZero;
        }
    }
}
