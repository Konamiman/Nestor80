namespace Konamiman.Nestor80.Assembler.ArithmeticOperations
{
    internal class DivideOperator : BinaryOperator
    {
        public static DivideOperator Instance = new();

        public override int Precedence => 2;

        public override string Name => "/";

        public override byte? ExtendedLinkItemType => 10;

        protected override Address OperateCore(Address value1, Address value2)
        {
            // One of the operands must be absolute
            // <mode> / Absolute = <mode>
            // Absolute / <mode> = <mode>

            if(!value1.IsAbsolute && !value2.IsAbsolute) {
                throw new InvalidExpressionException($"/: One of the operarnds must be absolute (attempted {value1.Type} / {value2.Type}");
            }

            var type = value1.IsAbsolute ? value2.Type : value1.Type;

            unchecked {
                return new Address(type, (ushort)(value1.Value / value2.Value));
            }
        }
    }
}
