namespace Konamiman.Nestor80.Linker;

/// <summary>
/// Represents a "set next code segment address" instruction
/// to process during the linking process.
/// </summary>
public class SetCodeSegmentAddress : ILinkingSequenceItem
{
    public ushort Address { get; set; }
}
