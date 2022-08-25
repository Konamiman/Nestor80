namespace Konamiman.Nestor80.Assembler.ArithmeticOperations
{
    internal class GreaterThanOrEqualOperator : BinaryOperator
    {
        public static GreaterThanOrEqualOperator Instance = new();

        public override int Precedence => 5;

        public override string Name => "GE";

        protected override Address OperateCore(Address value1, Address value2)
        {
            // Both addresses must be in the same mode

            if(!value1.SameModeAs(value2)) {
                throw new InvalidOperationException($"GE: Both addresses must be in the same mode (attempted {value1.Type} GE {value2.Type}");
            }

            return value1.Value >= value2.Value ? AbsoluteMinusOne : AbsoluteZero;
        }
    }
}
