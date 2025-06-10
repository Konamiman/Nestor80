namespace Konamiman.Nestor80.Linker;

/// <summary>
/// Represents an "align code segment address" instruction
/// to process during the linking process.
/// </summary>
public class AlignCodeSegmentAddress : ILinkingSequenceItem
{
    public ushort Value { get; set; }
}
