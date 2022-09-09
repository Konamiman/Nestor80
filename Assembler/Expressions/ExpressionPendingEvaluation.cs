namespace Konamiman.Nestor80.Assembler.Expressions
{
    internal class ExpressionPendingEvaluation
    {
        public Expression Expression { get; set; }

        public int LocationInOutput { get; set; }

        public int OutputSize { get; set; }

        public override string ToString() => $"@{LocationInOutput}, {OutputSize} bytes: {Expression}";
    }
}
