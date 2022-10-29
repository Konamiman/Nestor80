namespace Konamiman.Nestor80.Assembler
{
    internal class MacroExpansionState
    {
        public static MacroExpansionState ForNamedMacro(string[] templateLines, string[] parameters, int sourceLineNumber)
        {
            return new MacroExpansionState() {
                MacroType = MacroType.Named,
                TemplateLines = templateLines,
                Parameters = parameters,
                SourceLineNumber = sourceLineNumber,
                ProcessedCount = 0,
                RemainingCount = templateLines.Length
            };
        }
        //WIP

        public MacroType MacroType { get; init; }

        public int SourceLineNumber { get; init; }

        public string[] TemplateLines { get; init; }

        public string[] Parameters { get; init; }

        public int ProcessedCount { get; private set; }

        public int RemainingCount { get; private set; }

        public void GoNext()
        {
            ProcessedCount++;
            RemainingCount--;
        }

        public bool HasMore => RemainingCount > 0;

        public string CurrentParam => RemainingCount == 0 ? null : Parameters[ProcessedCount];
    }
}
