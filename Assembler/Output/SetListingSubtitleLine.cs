namespace Konamiman.Nestor80.Assembler.Output
{
    public class SetListingSubtitleLine : ProcessedSourceLine
    {
        public string Subtitle { get; set; }

        public override string ToString() => $"{base.ToString()}, {Subtitle}";
    }
}
