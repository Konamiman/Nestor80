using Konamiman.Nestor80.Assembler.Infrastructure;

namespace Konamiman.Nestor80.Assembler.Expressions
{
    /// <summary>
    /// Represents an expression whose evaluation needs to be deferred to pass 2,
    /// and contains all the information needed to evaluate it and integrate the result
    /// in the generated output file.
    /// </summary>
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
