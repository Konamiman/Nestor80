using Konamiman.Nestor80.Assembler.Expressions;

namespace Konamiman.Nestor80.Assembler.Output
{
    public class DefbLine : ProcessedSourceLine, IProducesOutput, IChangesLocationCounter
    {
        public byte[] OutputBytes { get; set; }
        public RelocatableOutputPart[] RelocatableParts { get; set; }
        public Address NewLocationCounter { get; set; }

        public override string ToString()
        {
            var s = base.ToString() + string.Join(", ", OutputBytes.Select(x => $"{x:X2}"));
            if(RelocatableParts?.Length == 0) {
                return s;
            }

            var part = RelocatableParts.Select(e => e.ToString());
            s += " | " + string.Join(" | ", part);
            return s;
        }
    }
}
