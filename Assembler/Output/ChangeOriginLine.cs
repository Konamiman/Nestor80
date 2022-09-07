namespace Konamiman.Nestor80.Assembler.Output
{
    public class ChangeOriginLine : ProcessedSourceLine, IChangesLocationCounter
    {
        public Address NewLocationCounter { get; set; }

        internal Expression NewLocationCounterExpression { get; set; }

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
