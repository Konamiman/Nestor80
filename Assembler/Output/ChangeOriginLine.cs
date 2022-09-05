namespace Konamiman.Nestor80.Assembler.Output
{
    public class ChangeOriginLine : ProcessedSourceLine, IChangesLocationCounter
    {
        public ChangeOriginLine(string line, Address newLocationCounter, string operand, int effectiveLineLength = 0) : base(line, effectiveLineLength, operand: operand)
        {
            NewLocationCounter = newLocationCounter;
        }

        public Address NewLocationCounter { get; init; }

        internal Expression NewLocationCounterExpression { get; init; }

        public override string ToString()
        {
            if(NewLocationCounter is not null)
                return base.ToString() + NewLocationCounter;
            else if(NewLocationCounterExpression is not null)
                return base.ToString() + "unk: " + NewLocationCounterExpression;
            else
                return base.ToString();
        }
    }
}
