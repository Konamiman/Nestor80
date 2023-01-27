namespace Konamiman.Nestor80.Linker;

public class LinkingConfiguration
{
    public ushort? StartAddress { get; set; } = null;

    public ushort? EndAddress { get; set; } = null;

    public byte FillingByte { get; set; } = 0;

    public ILinkingSequenceItem[] LinkingSequenceItems { get; set; }
}
