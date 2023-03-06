namespace Konamiman.Nestor80.Linker;

internal class ExternalReference
{
    public string SymbolName { get; set; }

    public string ProgramName { get; set; }

    public ushort ChainStartAddress { get; set; }
}
