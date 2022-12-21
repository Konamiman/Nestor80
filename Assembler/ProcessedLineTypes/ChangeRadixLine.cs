namespace Konamiman.Nestor80.Assembler.Output
{
    public class ChangeRadixLine : ProcessedSourceLine
    {
        public int NewRadix { get; set; }

        public override string ToString() => $"{base.ToString()}, {NewRadix}";
    }
}
