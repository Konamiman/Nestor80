using Konamiman.Nestor80.Assembler.Expressions;

namespace Konamiman.Nestor80.Assembler.Output
{
    public class DefbLine : IProcessedSourceLine, IProducesOutput, IChangesLocationCounter
    {
        public string Line { get; init; }
        public int EffectiveLineLength { get; init; }
        public byte[] OutputBytes { get; init; }
        public Tuple<int, IExpressionPart[]>[] Expressions { get; set; }
        public Address NewLocationCounter { get; init; }
    }
}
