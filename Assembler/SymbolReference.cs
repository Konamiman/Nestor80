namespace Konamiman.Nestor80.Assembler
{
    internal class SymbolReference : IAssemblyOutputPart, IExpressionPart
    {
        public string SymbolName { get; set; }

        public bool IsExternal { get; set; }
    }
}
