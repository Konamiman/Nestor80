using Konamiman.Nestor80.Assembler.Relocatable;

namespace Konamiman.Nestor80.Assembler.Output
{
    /// <summary>
    /// Interface for the lines that produce output bytes except DEFS (so CPU instructions and DEFB).
    /// </summary>
    public interface IProducesOutput : IChangesLocationCounter
    {
        byte[] OutputBytes { get; set; }

        RelocatableOutputPart[] RelocatableParts { get; set; }
    }
}
