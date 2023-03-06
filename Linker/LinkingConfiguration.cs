namespace Konamiman.Nestor80.Linker;

/// <summary>
/// Contains the definition and configuration of a linking process.
/// </summary>
public class LinkingConfiguration
{
    public const int DEFAULT_MAX_ERRORS = 34;

    /// <summary>
    /// The minimum Z80 memory address used by the generated binary file.
    /// Ignored if any part of the processed relocatable files is lower.
    /// </summary>
    public ushort StartAddress { get; set; } = 0xFFFF;

    /// <summary>
    /// The maximum Z80 memory address used by the generated binary file.
    /// Ignored if any part of the processed relocatable files is higher.
    /// </summary>
    public ushort EndAddress { get; set; } = 0;

    /// <summary>
    /// The byte to use to fill gaps between the linked file segments,
    /// between <see cref="StartAddress"/> and the actual start of the linked contents,
    /// and between <see cref="EndAddress"/> and the actual end of the linked contents, as applicable.
    /// </summary>
    public byte FillingByte { get; set; } = 0;

    /// <summary>
    /// A callback to open a file by nname, must return null on file not found.
    /// </summary>
    public Func<string, Stream> OpenFile = _ => null;

    /// <summary>
    /// A callback to get the full name of a library file referenced by a .REQUEST instruction.
    /// </summary>
    public Func<string, string> GetFullNameOfRequestedLibraryFile = _ => _;

    /// <summary>
    /// The sequence of items that define the linking progress to perform.
    /// </summary>
    public ILinkingSequenceItem[] LinkingSequenceItems { get; set; }

    /// <summary>
    /// Maximum number of errors to produce, after which the linking process is aborted.
    /// </summary>
    public int MaxErrors { get; set; } = DEFAULT_MAX_ERRORS;

    /// <summary>
    /// True to generate an Intel HEX file, false to generate a binary file.
    /// </summary>
    public bool OutputHexFormat { get; set; }
}
