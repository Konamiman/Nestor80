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

        public string[] LocalSymbols { get; set; }

        private readonly string[] parameters;

        private int remainingLinesCount;

        public override bool HasMore => remainingLinesCount > 0;

        public string MacroName { get; }

        private Dictionary<string, string> processedLocalSymbols = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Given a symbol name, check if it's a local symbol inside a named macro expansion
        /// and then replace it with the corresponding "..number".
        /// </summary>
        /// <param name="symbolName">Symbol name to check.</param>
        /// <param name="newLocalSymbolNumber">Number to use if a replacement is needed, it gets increased in that case.</param>
        /// <returns>Converted (maybe) symbol name.</returns>
        public bool MaybeConvertLocalSymbolName(ref string symbolName, ref ushort newLocalSymbolNumber)
        {
            if(LocalSymbols is null) {
                return false;
            }

            if(!LocalSymbols.Contains(symbolName, StringComparer.OrdinalIgnoreCase)) {
                return false;
            }

            if(processedLocalSymbols.ContainsKey(symbolName)) {
                symbolName = processedLocalSymbols[symbolName];
                return true;
            }

            processedLocalSymbols[symbolName] = $"..{newLocalSymbolNumber:X4}";
            symbolName = processedLocalSymbols[symbolName];
            newLocalSymbolNumber++;
            return true;
        }

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
