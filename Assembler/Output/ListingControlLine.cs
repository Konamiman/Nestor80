namespace Konamiman.Nestor80.Assembler.Output
{
    public class ListingControlLine : ProcessedSourceLine
    {
        public ListingControlType Type { get; set; }

        public override string ToString() => $"{base.ToString()}, {Type}";
    }
}
