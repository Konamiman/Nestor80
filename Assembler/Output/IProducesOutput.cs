namespace Konamiman.Nestor80.Assembler.Output
{
    public interface IProducesOutput : IChangesLocationCounter
    {
        byte[] OutputBytes { get; set; }

        RelocatableOutputPart[] RelocatableParts { get; set; }
    }
}
