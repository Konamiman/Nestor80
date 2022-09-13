namespace Konamiman.Nestor80.Assembler.Output
{
    public class PrintxLine : ProcessedSourceLine
    {
        public string PrintedText { get; set; }

        public override string ToString() => $"{base.ToString()}: {PrintedText}";
    }
}
