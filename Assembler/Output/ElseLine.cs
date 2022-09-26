namespace Konamiman.Nestor80.Assembler.Output
{
    public class ElseLine : ProcessedSourceLine
    {
        public bool EvaluatesToTrue { get; set; }

        public override string ToString() => $"{base.ToString()}, {EvaluatesToTrue}";
    }
}
