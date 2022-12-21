namespace Konamiman.Nestor80.Assembler.Output
{
    public class ModuleStartLine : ProcessedSourceLine
    {
        public string Name { get; set; }

        public override string ToString() => $"{base.ToString()} {Name}";
    }
}
