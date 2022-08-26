namespace Konamiman.Nestor80.Assembler.ArithmeticOperations
{
    internal abstract class UnaryOperator : ArithmeticOperator
    {
        public override bool IsUnary => true;

        public override bool IsRightAssociative => true;
    }
}
