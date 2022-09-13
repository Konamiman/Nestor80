namespace Konamiman.Nestor80.Assembler.Output
{
    public class SetListingTitleLine : ProcessedSourceLine
    {
        public string Title { get; set; }

        public override string ToString() => $"{base.ToString()}, {Title}";
    }
}
