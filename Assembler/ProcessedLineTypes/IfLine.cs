namespace Konamiman.Nestor80.Assembler.Output
{
    public class IfLine : ProcessedSourceLine
    {
        public bool? EvaluatesToTrue { get; set; }

        public override string ToString() => $"{base.ToString()}, {EvaluatesToTrue}";
    }
}
