namespace Konamiman.Nestor80.Assembler.Output
{
    public class AssemblyError
    {
        public const int MAX_STORED_SOURCE_TEXT_LENGTH = 80;

        public AssemblyError(AssemblyErrorCode code, string message, int? lineNumber, string sourceLineText = null, string includeFileName = null, (string, int)[] macroNamesAndLines = null)
        {
            Code = code;
            Message = message;
            LineNumber = lineNumber;
            IncludeFileName = includeFileName;
            MacroNamesAndLines = macroNamesAndLines;
            Severity = code switch {
                < AssemblyErrorCode.FirstError => AssemblyErrorSeverity.Warning,
                < AssemblyErrorCode.FirstFatal => AssemblyErrorSeverity.Error,
                _ => AssemblyErrorSeverity.Fatal
            };

            if(sourceLineText is not null) {
                sourceLineText = sourceLineText.Trim();
                SourceLineText = sourceLineText.Length > MAX_STORED_SOURCE_TEXT_LENGTH ? sourceLineText[..(MAX_STORED_SOURCE_TEXT_LENGTH - 3)] + "..." : sourceLineText;
            }
        }

        public int? LineNumber { get; init; } = null;

        public string SourceLineText { get; init; } = null;

        public AssemblyErrorCode Code { get; init; }

        public string Message { get; init; }

        public (string, int)[] MacroNamesAndLines { get; set; }

        public string IncludeFileName { get; set; }

        public bool IsWarning => Severity is AssemblyErrorSeverity.Warning;

        public bool IsFatal => Severity is AssemblyErrorSeverity.Fatal;

        public bool IsMacroLine => MacroNamesAndLines is not null;

        public AssemblyErrorSeverity Severity { get; init; }

        public override string ToString()
        {
            var lineNumbePrefix = LineNumber == null ? "" : $"In line {LineNumber}: ";
            var fileNamePrefix = IncludeFileName == null ? "" : $"[{IncludeFileName}] ";
            var macroPrefix = IsMacroLine ? $"<{string.Join(" --> ",MacroNamesAndLines.Select(nl => $"{nl.Item1}:{nl.Item2}").ToArray())}> " : "";
            return $"{fileNamePrefix}{macroPrefix}{lineNumbePrefix}{Message}";
        }

        public static bool operator ==(AssemblyError error1, AssemblyError error2)
        {
            if(error1 is null || error2 is null) {
                return false;
            }

            return error1.Code == error2.Code &&
                error1.LineNumber == error2.LineNumber &&
                error1.IncludeFileName == error2.IncludeFileName &&
                (error1.MacroNamesAndLines ?? Array.Empty<(string,int)>()).SequenceEqual(error2.MacroNamesAndLines ?? Array.Empty<(string, int)>());
        }

        public static bool operator !=(AssemblyError error1, AssemblyError error2)
        {
            return !(error1 == error2);
        }

        public override bool Equals(object obj)
        {
            if(obj == null || GetType() != obj.GetType())
                return false;

            var error2 = (AssemblyError)obj;
            return this == error2;
        }

        public override int GetHashCode()
        {
            return Code.GetHashCode() ^
                LineNumber.GetHashCode() ^
                IncludeFileName?.GetHashCode() ?? 0 ^
                MacroNamesAndLines?.GetHashCode() ?? 0;
        }
    }
}
