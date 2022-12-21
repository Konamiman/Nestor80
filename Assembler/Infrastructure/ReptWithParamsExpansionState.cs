using Konamiman.Nestor80.Assembler.Output;

namespace Konamiman.Nestor80.Assembler.Infrastructure
{
    /// <summary>
    /// Expansion state class for repeat with arguments macros (those that are defined with the IRP, IRPC and IRPS instructions).
    /// </summary
    internal class ReptWithParamsExpansionState : MacroExpansionState
    {
        private static readonly string[] singleNullArray = new string[] { null };

        public ReptWithParamsExpansionState(LinesContainerLine expansionProcessedLine, string[] templateLines, string[] parameters, int sourceLineNumber)
            : base(expansionProcessedLine, templateLines, sourceLineNumber)
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
            if (!HasMore)
            {
                throw new InvalidOperationException($"{nameof(NamedMacroExpansionState)}.{nameof(GetNextSourceLine)} is not supposed to be called whtn {nameof(HasMore)} returns false");
            }

            var line = string.Format(TemplateLines[currentLineIndex], parameters[currentParameterIndex]);
            RelativeLineNumber = currentLineIndex;
            currentLineIndex++;
            remainingLinesCount--;
            if (remainingLinesCount == 0)
            {
                StartOver();
            }
            return line;
        }

        private void StartOver()
        {
            currentLineIndex = 0;
            remainingLinesCount = TemplateLines.Length;
            remainingParametersCount--;
            currentParameterIndex++;
        }

        public override void Exit(bool forceEnd)
        {
            if (forceEnd)
            {
                remainingParametersCount = 0;
            }
            else
            {
                StartOver();
            }
        }
    }
}
