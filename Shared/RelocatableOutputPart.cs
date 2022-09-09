namespace Konamiman.Nestor80
{ 
    public abstract class RelocatableOutputPart
    {
        public int Index { get; set; }

        public bool IsByte { get; set; }

        public override string ToString() => $"@{Index}, {(IsByte ? "byte" : "word")}";
    }
}
