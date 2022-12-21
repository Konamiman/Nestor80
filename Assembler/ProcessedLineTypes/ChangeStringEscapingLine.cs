namespace Konamiman.Nestor80.Assembler.Output
{
    public class ChangeStringEscapingLine : ProcessedSourceLine
    {
        public bool IsOn { get; set; }

        public string Argument { get; set; }

        public override string ToString() => $"{base.ToString()}, {Argument}, {IsOn}";
    }
}
