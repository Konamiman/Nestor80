using Konamiman.Nestor80.Assembler.Output;

namespace Konamiman.Nestor80.Assembler.Infrastructure
{
    /// <summary>
    /// Expansion state class for repeat with count macros (those that are defined with the REPT instruction).
    /// </summary>
    internal class ReptWithCountExpansionState : MacroExpansionState
    {
        public ReptWithCountExpansionState(LinesContainerLine expansionProcessedLine, string[] lines, int count, int sourceLineNumber)
            : base(expansionProcessedLine, lines, sourceLineNumber)
        {
            MacroType = MacroType.ReptWithCount;
            currentLineIndex = 0;
            RelativeLineNumber = -1;
            remainingRepetitionsCount = count;
            remainingLinesCount = count == 0 ? 0 : lines.Length;
        }

        private int remainingRepetitionsCount;
        private int remainingLinesCount;
        private int currentLineIndex;

        public override bool HasMore => remainingRepetitionsCount > 0 && remainingLinesCount > 0;

        public override string GetNextSourceLine()
        {
            if (!HasMore)
            {
                throw new InvalidOperationException($"{nameof(NamedMacroExpansionState)}.{nameof(GetNextSourceLine)} is not supposed to be called whtn {nameof(HasMore)} returns false");
            }

            var line = TemplateLines[currentLineIndex];
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
            remainingRepetitionsCount--;
        }

        public override void Exit(bool forceEnd)
        {
            if (forceEnd)
            {
                remainingRepetitionsCount = 0;
            }
            else
            {
                StartOver();
            }
        }
    }
}
