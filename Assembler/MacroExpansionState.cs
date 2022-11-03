using Konamiman.Nestor80.Assembler.Output;

namespace Konamiman.Nestor80.Assembler
{
    internal abstract class MacroExpansionState
    {
        public MacroExpansionState(LinesContainerLine expansionProcessedLine, string[] templateLines, int sourceLineNumber)
        {
            TemplateLines = templateLines;
            StartLineNumber = sourceLineNumber;
            ExpansionProcessedLine = expansionProcessedLine;
        }

        public LinesContainerLine ExpansionProcessedLine { get; init; }

        public MacroType MacroType { get; init; }

        public int StartLineNumber { get; init; }

        public string[] TemplateLines { get; init; }

        public int RelativeLineNumber { get; protected set; }

        public int ActualLineNumber => StartLineNumber + RelativeLineNumber;

        public List<ProcessedSourceLine> ProcessedLines { get; } = new();

        public abstract bool HasMore { get; }

        public abstract string GetNextSourceLine();
    }
}
