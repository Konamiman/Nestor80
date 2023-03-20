namespace Konamiman.Nestor80.Linker;

/// <summary>
/// Contains information about a program processed during the linking process, 
/// including the resulting absolute addresses of its code and data segments.
/// </summary>
public class ProgramData
{
    public string ProgramName { get; set; }

    public ushort CodeSegmentStart { get; set; }

    public ushort CodeSegmentSize { get; set; }

    public ushort DataSegmentStart { get; set; }

    public ushort DataSegmentSize { get; set; }

    public ushort AbsoluteSegmentStart { get; set; }

    public ushort AbsoluteSegmentSize { get; set; }

    public Dictionary<string, ushort> PublicSymbols { get; set; }

    public bool HasContent => CodeSegmentSize > 0 || DataSegmentSize > 0 || AbsoluteSegmentSize > 0;
}
