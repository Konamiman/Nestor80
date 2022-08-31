namespace Konamiman.Nestor80.Assembler.ArithmeticOperations
{
    internal class LessThanOrEqualOperator : BinaryOperator
    {
        public static LessThanOrEqualOperator Instance = new();

        public override int Precedence => 5;

        public override string Name => "LE";

        protected override Address OperateCore(Address value1, Address value2)
        {
            // Both addresses must be in the same mode

            if(!value1.SameModeAs(value2)) {
                throw new InvalidOperationException($"LE: Both addresses must be in the same mode (attempted {value1.Type} LE {value2.Type}");
            }

            return value1.Value <= value2.Value ? AbsoluteMinusOne : AbsoluteZero;
        }
    }
}
