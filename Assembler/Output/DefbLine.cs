using Konamiman.Nestor80.Assembler.Expressions;

namespace Konamiman.Nestor80.Assembler.Output
{
    public class DefbLine : ProcessedSourceLine, IProducesOutput, IChangesLocationCounter
    {
        public DefbLine(string line, int effectiveLength, byte[] outputBytes, Tuple<int, IExpressionPart[]>[] expressions, Address newLocationCounter) : base(line, effectiveLength)
        {
            this.OutputBytes = outputBytes;
            this.Expressions = expressions;
            this.NewLocationCounter = newLocationCounter;
        }

        public byte[] OutputBytes { get; init; }
        public Tuple<int, IExpressionPart[]>[] Expressions { get; set; }
        public Address NewLocationCounter { get; init; }
    }
}
