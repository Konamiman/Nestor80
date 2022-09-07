using Konamiman.Nestor80.Assembler.Expressions;

namespace Konamiman.Nestor80.Assembler.Output
{
    public class DefbLine : ProcessedSourceLine, IProducesOutput, IChangesLocationCounter
    {
        public byte[] OutputBytes { get; set; }
        public Tuple<int, IExpressionPart[]>[] Expressions { get; set; }
        public Address NewLocationCounter { get; set; }

        public override string ToString()
        {
            var s = base.ToString() + string.Join(", ", OutputBytes.Select(x => $"{x:X2}"));
            if(Expressions?.Length == 0) {
                return s;
            }

            var exp = Expressions.Select(e => $"@{e.Item1} = {string.Join(", ", e.Item2.Select(x => x.ToString()))}");
            s += " | " + string.Join(" | ", exp);
            return s;
        }
    }
}
