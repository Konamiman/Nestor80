namespace Konamiman.Nestor80.Assembler.Output
{
    public class ProgramNameLine : ProcessedSourceLine
    {
        public string Name { get; set; }

        public override string ToString() => $"{base.ToString()} {Name}";
    }
}
