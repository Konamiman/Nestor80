namespace Konamiman.Nestor80.Assembler
{
    internal abstract class MacroExpansionState
    {
        public MacroExpansionState(string[] templateLines, int sourceLineNumber)
        {
            TemplateLines = templateLines;
            StartLineNumber = sourceLineNumber;
        }

        public MacroType MacroType { get; init; }

        public int StartLineNumber { get; init; }

        public string[] TemplateLines { get; init; }

        public int RelativeLineNumber { get; protected set; }

        public abstract bool HasMore { get; }

        public abstract string GetNextSourceLine();
    }
}
