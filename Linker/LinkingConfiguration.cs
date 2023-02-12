namespace Konamiman.Nestor80.Linker;

public class LinkingConfiguration
{
    public ushort StartAddress { get; set; } = 0xFFFF;

    public ushort EndAddress { get; set; } = 0;

    public byte FillingByte { get; set; } = 0;

    public Func<string, Stream> OpenFile = _ => null;

    public Func<string, string> GetFullNameOfRequestedLibraryFile = _ => _;

    public ILinkingSequenceItem[] LinkingSequenceItems { get; set; }
}
