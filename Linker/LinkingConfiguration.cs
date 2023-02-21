namespace Konamiman.Nestor80.Linker;

public class LinkingConfiguration
{
    public const int DEFAULT_MAX_ERRORS = 34;

    public ushort StartAddress { get; set; } = 0xFFFF;

    public ushort EndAddress { get; set; } = 0;

    public byte FillingByte { get; set; } = 0;

    public Func<string, Stream> OpenFile = _ => null;

    public Func<string, string> GetFullNameOfRequestedLibraryFile = _ => _;

    public ILinkingSequenceItem[] LinkingSequenceItems { get; set; }

    public int MaxErrors { get; set; } = DEFAULT_MAX_ERRORS;

    public bool OutputHexFormat { get; set; }
}
