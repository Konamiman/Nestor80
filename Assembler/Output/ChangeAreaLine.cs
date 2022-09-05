namespace Konamiman.Nestor80.Assembler.Output
{
    public class ChangeAreaLine : ProcessedSourceLine, IChangesLocationCounter
    {
        public ChangeAreaLine(string line, Address newLocationCounter, string operand, int effectiveLineLength = 0) : base(line, effectiveLineLength, operand: operand)
        {
            NewLocationCounter = newLocationCounter;
        }

        public Address NewLocationCounter { get; init; }

        public override string ToString() => base.ToString() + (NewLocationCounter is null ? "(location unknown)" : NewLocationCounter);
    }
}
