namespace Konamiman.Nestor80.Assembler
{
    internal class ReptWithParamsExpansionState : MacroExpansionState
    {
        private static readonly string[] singleNullArray = new string[] { null };

        public ReptWithParamsExpansionState(string[] templateLines, string[] parameters, int sourceLineNumber)
            : base(templateLines, sourceLineNumber)
        {
            MacroType = MacroType.ReptWithArgs;
            RelativeLineNumber = -1;
            currentLineIndex = 0;
            currentParameterIndex = 0;

            this.parameters = parameters.Length == 0 ? singleNullArray : parameters;
            remainingParametersCount = templateLines.Length == 0 ? 0 : this.parameters.Length;
            remainingLinesCount = templateLines.Length;
        }

        private int remainingParametersCount;
        private int remainingLinesCount;
        private int currentLineIndex;
        private int currentParameterIndex;

        private readonly string[] parameters;

        public override bool HasMore => remainingParametersCount > 0 && remainingLinesCount > 0;

        public override string GetNextSourceLine()
        {
            if(!HasMore) {
                throw new InvalidOperationException($"{nameof(NamedMacroExpansionState)}.{nameof(GetNextSourceLine)} is not supposed to be called whtn {nameof(HasMore)} returns false");
            }

            var line = string.Format(TemplateLines[currentLineIndex], parameters[currentParameterIndex]);
            RelativeLineNumber = currentLineIndex;
            currentLineIndex++;
            remainingLinesCount--;
            if(remainingLinesCount == 0) {
                currentLineIndex = 0;
                remainingLinesCount = TemplateLines.Length;
                remainingParametersCount--;
                currentParameterIndex++;
            }
            return line;
        }
    }
}
