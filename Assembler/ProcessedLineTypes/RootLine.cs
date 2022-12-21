namespace Konamiman.Nestor80.Assembler.Output
{
    internal class RootLine : ProcessedSourceLine
    {
        public string[] RootSymbols { get; set; }

        public override string ToString() => $"{base.ToString()} {string.Join(",", RootSymbols)}";
    }
}
