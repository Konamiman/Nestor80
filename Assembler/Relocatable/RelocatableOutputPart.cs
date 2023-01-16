namespace Konamiman.Nestor80.Assembler.Relocatable
{
    /// <summary>
    /// Base class for the relocatable items defined by the LINK-80 relocatable file format.
    /// </summary>
    public abstract class RelocatableOutputPart
    {
        public int Index { get; set; }

        public bool IsByte { get; set; }

        public override string ToString() => $"@{Index}, {(IsByte ? "byte" : "word")}";
    }
}
