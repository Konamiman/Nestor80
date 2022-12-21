namespace Konamiman.Nestor80.Assembler.Output
{
    public class IncludeLine : LinesContainerLine
    {
        public string FullPath { get; set; }

        public string FileName { get; set; }

        public override string ToString() => $"{base.ToString()} {FullPath}";
    }
}
