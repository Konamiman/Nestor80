using Konamiman.Nestor80.Assembler.Output;

namespace Konamiman.Nestor80.Assembler
{
    internal class NamedMacroExpansionState : MacroExpansionState
    {
        public NamedMacroExpansionState(string macroName, LinesContainerLine expansionProcessedLine, string[] templateLines, int argumentsCount, string[] parameters, int sourceLineNumber)
            :base(expansionProcessedLine, templateLines, sourceLineNumber)
        {
            MacroType = MacroType.Named;
            RelativeLineNumber = -1;

            if(parameters.Length < argumentsCount) {
                parameters = parameters.Concat(Enumerable.Repeat<string>(null, argumentsCount - parameters.Length)).ToArray();
            }

            this.MacroName = macroName;
            this.parameters = parameters;
            remainingLinesCount = templateLines.Length;
        }

        private readonly string[] parameters;

        private int remainingLinesCount;

        public override bool HasMore => remainingLinesCount > 0;

        public string MacroName { get; }

        public override string GetNextSourceLine()
        {
            if(!HasMore) {
                throw new InvalidOperationException($"{nameof(NamedMacroExpansionState)}.{nameof(GetNextSourceLine)} is not supposed to be called whtn {nameof(HasMore)} returns false");
            }

            RelativeLineNumber++;
            var line = string.Format(TemplateLines[RelativeLineNumber], parameters);
            remainingLinesCount--;
            return line;
        }

        public override void Exit(bool forceEnd)
        {
            remainingLinesCount = 0;
        }
    }
}
