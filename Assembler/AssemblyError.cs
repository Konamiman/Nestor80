namespace Konamiman.Nestor80.Assembler
{
    public class AssemblyError
    {
        public AssemblyError(AssemblyErrorCode code, string message)
        {
            Code = code;
            Message = message;
        }

        public int? LineNumber { get; init; } = null;

        public AssemblyErrorCode Code { get; init; }

        public string Message { get; init; }

        public bool IsWarning => Code >= AssemblyErrorCode.FirstWarning;

        public override string ToString()
        {
            var lineNumbePrefix = LineNumber == null ? "" : $"In line {LineNumber}: ";
            return $"{lineNumbePrefix}{Message}";
        }
    }
}
