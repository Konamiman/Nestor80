namespace Konamiman.Nestor80.Assembler.Output
{
    public class LinkerFileReadRequestLine : ProcessedSourceLine
    {
        public string[] Filenames { get; set; }

        public override string ToString() => $"{base.ToString()}, {string.Join(", ", Filenames)}";
    }
}
