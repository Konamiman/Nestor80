namespace Konamiman.Nestor80.Assembler.Output
{
    public class MacroDefinitionBodyLine : ProcessedSourceLine
    {
        public override string ToString() => $"MacroBody: {Line}";
    }
}
