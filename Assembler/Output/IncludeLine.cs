namespace Konamiman.Nestor80.Assembler.Output
{
    public class IncludeLine : ProcessedSourceLine
    {
        public string FullPath { get; set; }

        public string FileName { get; set; }

        public ProcessedSourceLine[] Lines { get; set; }

        public override string ToString() => $"{base.ToString()} {FullPath}";
    }
}
