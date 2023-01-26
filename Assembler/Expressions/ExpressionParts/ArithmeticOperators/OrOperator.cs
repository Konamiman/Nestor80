using Konamiman.Nestor80.Assembler.Relocatable;

namespace Konamiman.Nestor80.Assembler.Expressions.ExpressionParts.ArithmeticOperators
{
    internal class OrOperator : BinaryOperator
    {
        public static OrOperator Instance = new();

        public override int Precedence => 8;

        public override string Name => "OR";

        public override byte ExtendedLinkItemType => 25;

        protected override Address OperateCore(Address value1, Address value2)
        {
            // At least one of the operands must be Absolute
            // Absolute OR <mode> = <mode>

            if (!value1.IsAbsolute && !value2.IsAbsolute)
            {
                throw new InvalidExpressionException($"OR: At least one of the operands must be absolute (attempted {value1.Type} OR {value2.Type})");
            }

            var type = value1.IsAbsolute ? value2.Type : value1.Type;

            return new Address(type, (ushort)(value1.Value | value2.Value));
        }
    }
}
