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
                throw new InvalidExpressionException($"OR: At least one of the operands must be absolute (attempted {value1.EffectiveType} OR {value2.EffectiveType})");
            }

            var type = value1.IsAbsolute ? value2.Type : value1.Type;
            var commonName = value1.IsAbsolute ? value2.CommonBlockName : value1.CommonBlockName;

            return new Address(type, (ushort)(value1.Value | value2.Value), commonName);
        }
    }
}
