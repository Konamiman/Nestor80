namespace Konamiman.Nestor80.Assembler.Output
{
    public class AssemblyError
    {
        public AssemblyError(AssemblyErrorCode code, string message, int? lineNumber, string includeFileName = null)
        {
            Code = code;
            Message = message;
            LineNumber = lineNumber;
            IncludeFileName = includeFileName;
            Severity = code switch
            {
                < AssemblyErrorCode.FirstError => AssemblyErrorSeverity.Warning,
                < AssemblyErrorCode.FirstFatal => AssemblyErrorSeverity.Error,
                _ => AssemblyErrorSeverity.Fatal
            };
        }

        public int? LineNumber { get; init; } = null;

        public AssemblyErrorCode Code { get; init; }

        public string Message { get; init; }

        public string IncludeFileName { get; set; }

        public bool IsWarning => Severity is AssemblyErrorSeverity.Warning;

        public bool IsFatal => Severity is AssemblyErrorSeverity.Fatal;

        public AssemblyErrorSeverity Severity { get; init; }

        public override string ToString()
        {
            var lineNumbePrefix = LineNumber == null ? "" : $"In line {LineNumber}: ";
            var fileNamePrefix = IncludeFileName == null ? "" : $"[{IncludeFileName}] ";
            return $"{fileNamePrefix}{lineNumbePrefix}{Message}";
        }
    }
}
