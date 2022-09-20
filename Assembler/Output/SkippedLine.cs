namespace Konamiman.Nestor80.Assembler.Output
{
    public class SkippedLine : ProcessedSourceLine
    {
        public override string ToString() => "Skipped, " + Line;
    }
}
