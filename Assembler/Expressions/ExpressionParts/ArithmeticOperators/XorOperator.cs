using Konamiman.Nestor80.Assembler.Relocatable;

namespace Konamiman.Nestor80.Assembler.Expressions.ExpressionParts.ArithmeticOperators
{
    internal class XorOperator : BinaryOperator
    {
        public static XorOperator Instance = new();

        public override int Precedence => 8;

        public override string Name => "XOR";

        public override byte ExtendedLinkItemType => 26;

        protected override Address OperateCore(Address value1, Address value2)
        {
            // At least one of the operands must be Absolute
            // Absolute XOR <mode> = <mode>

            if (!value1.IsAbsolute && !value2.IsAbsolute)
            {
                throw new InvalidExpressionException($"XOR: At least one of the operands must be absolute (attempted {value1.EffectiveType} XOR {value2.EffectiveType})");
            }

            var type = value1.IsAbsolute ? value2.Type : value1.Type;
            var commonName = value1.IsAbsolute ? value2.CommonBlockName : value1.CommonBlockName;

            return new Address(type, (ushort)(value1.Value ^ value2.Value), commonName);
        }
    }
}
