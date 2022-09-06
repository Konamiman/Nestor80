namespace Konamiman.Nestor80.Assembler.Output
{
    internal class PublicDeclarationLine : ProcessedSourceLine
    {
        public PublicDeclarationLine(string line, int effectiveLength = 0, string label = null, string operand = null, string symbolName = null) : base(line, effectiveLength, label, operand)
        {
            this.SymbolName = symbolName;
        }

        public string SymbolName { get; }
    }
}
