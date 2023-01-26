using Konamiman.Nestor80.Assembler.Relocatable;

namespace Konamiman.Nestor80.Assembler.Expressions.ExpressionParts.ArithmeticOperators
{
    internal class UnaryPlusOperator : UnaryOperator
    {
        public static UnaryPlusOperator Instance = new();

        public override int Precedence => 3;

        public override string Name => "u+";

        //Not actually used
        public override byte ExtendedLinkItemType => 255;

        protected override Address OperateCore(Address value1, Address value2)
        {
            return value1;
        }
    }
}
