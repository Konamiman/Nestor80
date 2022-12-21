using Konamiman.Nestor80.Assembler.Infrastructure;

namespace Konamiman.Nestor80.Assembler.Output
{
    public class ListingControlLine : ProcessedSourceLine
    {
        public ListingControlInstructionType Type { get; set; }

        public override string ToString() => $"{base.ToString()}, {Type}";
    }
}
