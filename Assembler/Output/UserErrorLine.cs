namespace Konamiman.Nestor80.Assembler.Output
{
    public class UserErrorLine : ProcessedSourceLine
    {
        public AssemblyErrorSeverity Severity { get; set; }

        public string Message { get; set; }

        public override string ToString() => $"{base.ToString()} ({Severity}), {Message}";
    }
}
