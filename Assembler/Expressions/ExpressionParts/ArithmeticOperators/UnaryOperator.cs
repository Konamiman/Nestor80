using Konamiman.Nestor80.Assembler.Expressions.ExpressionParts;

namespace Konamiman.Nestor80.Assembler.Expressions.ExpressionParts.ArithmeticOperators
{
    internal abstract class UnaryOperator : ArithmeticOperator
    {
        public override bool IsUnary => true;
    }
}
