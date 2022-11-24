namespace Konamiman.Nestor80.Assembler.Expressions
{
    internal class ExpressionPendingEvaluation
    {
        public Expression Expression { get; set; }

        public int LocationInOutput { get; set; }

        public bool IsByte => ArgumentType is not CpuInstrArgType.Word && ArgumentType is not CpuInstrArgType.WordInParenthesis;

        public bool IsNegativeIxy { get; set; }

        public CpuInstrArgType ArgumentType { get; set; }

        public override string ToString() => $"@{LocationInOutput}, {Expression}, {ArgumentType} {(IsNegativeIxy ? "-" : "+")}";
    }
}
