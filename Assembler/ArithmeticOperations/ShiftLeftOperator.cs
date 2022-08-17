namespace Konamiman.Nestor80.Assembler.ArithmeticOperations
{
    internal class ShiftLeftOperator : ArithmeticOperator
    {
        public override int Precedence => 2;

        public override string Name => "SHL";

        protected override Address OperateCore(Address value1, Address value2)
        {
            // The second operator must be absolute

            if(!value1.IsAbsolute || !value2.IsAbsolute) {
                throw new InvalidOperationException($"SHL: The second operand must be absolute (attempted {value1.Type} SHL {value2.Type}");
            }

            unchecked {
                return new Address(value1.Type, (ushort)(value1.Value << value2.Value));
            }
        }
    }
}
