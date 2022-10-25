namespace Konamiman.Nestor80.Assembler.Expressions
{
    internal class ExpressionPendingEvaluation
    {
        public Expression Expression { get; set; }

        public int LocationInOutput { get; set; }

        public bool IsByte => ArgumentType is not CpuInstructionArgumentType.Word;

        public string IxRegisterSign { get; set; }

        public CpuInstructionArgumentType ArgumentType { get; set; }

        public override string ToString() => $"@{LocationInOutput}, {Expression}, {ArgumentType} {IxRegisterSign}";
    }
}
