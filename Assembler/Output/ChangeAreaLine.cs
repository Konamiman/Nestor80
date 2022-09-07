namespace Konamiman.Nestor80.Assembler.Output
{
    public class ChangeAreaLine : ProcessedSourceLine, IChangesLocationCounter
    {
        public Address NewLocationCounter { get; set; }

        public override string ToString() => base.ToString() + (NewLocationCounter is null ? "(location unknown)" : NewLocationCounter);
    }
}
