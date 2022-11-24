namespace Konamiman.Nestor80.Assembler.ArithmeticOperations
{
    internal class LessThanOperator : BinaryOperator
    {
        public static LessThanOperator Instance = new();

        public override int Precedence => 5;

        public override string Name => "LT";

        protected override Address OperateCore(Address value1, Address value2)
        {
            // Both addresses must be in the same mode

            if(!value1.SameModeAs(value2)) {
                throw new InvalidExpressionException($"LT: Both values must be in the same mode (attempted {value1.Type} LT {value2.Type})");
            }

            return value1.Value < value2.Value ? AbsoluteMinusOne : AbsoluteZero;
        }
    }
}
