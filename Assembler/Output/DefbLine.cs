using Konamiman.Nestor80.Assembler.Expressions;

namespace Konamiman.Nestor80.Assembler.Output
{
    public class DefbLine : ProcessedSourceLine, IProducesOutput, IChangesLocationCounter
    {
        public DefbLine(string line, byte[] outputBytes, Tuple<int, IExpressionPart[]>[] expressions, Address newLocationCounter, string operand) 
            : base(line, operand: "DB")
        {
            this.OutputBytes = outputBytes;
            this.Expressions = expressions;
            this.NewLocationCounter = newLocationCounter;
            this.Operand = operand;
        }

        public byte[] OutputBytes { get; init; }
        public Tuple<int, IExpressionPart[]>[] Expressions { get; set; }
        public Address NewLocationCounter { get; init; }

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
