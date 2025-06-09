namespace Konamiman.Nestor80.Linker;

/// <summary>
/// Represents an "align data segment address" instruction
/// to process during the linking process.
/// </summary>
public class AlignDataSegmentAddress : ILinkingSequenceItem
{
    public ushort Value { get; set; }
}
