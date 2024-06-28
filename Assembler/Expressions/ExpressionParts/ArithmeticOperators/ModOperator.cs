using Konamiman.Nestor80.Assembler.Relocatable;

namespace Konamiman.Nestor80.Assembler.Expressions.ExpressionParts.ArithmeticOperators
{
    internal class ModOperator : BinaryOperator
    {
        public static ModOperator Instance = new();

        public override int Precedence => 2;

        public override string Name => "MOD";

        public override byte ExtendedLinkItemType => 11;

        protected override Address OperateCore(Address value1, Address value2)
        {
            // The second operator must be absolute
            // <mode> MOD Absolute = <mode>

            if (!value2.IsAbsolute)
            {
                throw new InvalidExpressionException($"MOD: The second operand must be absolute (attempted {value1.EffectiveType} MOD {value2.EffectiveType})");
            }

            unchecked
            {
                return new Address(value1.Type, (ushort)(value1.Value % value2.Value), value1.CommonBlockName);
            }
        }
    }
}
