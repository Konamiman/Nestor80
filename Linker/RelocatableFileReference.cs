namespace Konamiman.Nestor80.Linker;

/// <summary>
/// Represents one of the relocatable files to process during the linking process.
/// </summary>
public class RelocatableFileReference : ILinkingSequenceItem
{
    public string FullName { get; set; }

    public string DisplayName { get; set; }
}
