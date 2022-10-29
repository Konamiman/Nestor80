namespace Konamiman.Nestor80.Assembler.Output
{
    public class NamedMacroDefinitionLine : ProcessedSourceLine
    {
        public string Name { get; set; }

        public string[] Arguments { get; set; }

        public string[] LineTemplates { get; set; }
    }
}
