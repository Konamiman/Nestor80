namespace Konamiman.Nestor80.Assembler.Output
{
    public class PrintLine : ProcessedSourceLine
    {
        public string PrintedText { get; set; }

        public int? PrintInPass { get; set; }

        public bool PrintInPass1 => PrintInPass is null or 1;

        public bool PrintInPass2 => PrintInPass is null or 2;

        public override string ToString() => $"{base.ToString()}: {PrintedText}, {PrintInPass}";
    }
}
