namespace Konamiman.Nestor80.Assembler.Output
{
    /// <summary>
    /// Represents a line whose processing was skipped because it was found inside a false conditional.
    /// </summary>
    public class SkippedLine : ProcessedSourceLine
    {
        public override string ToString() => "Skipped, " + Line;
    }
}
