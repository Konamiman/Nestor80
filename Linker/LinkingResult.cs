namespace Konamiman.Nestor80.Linker;

/// <summary>
/// Contains information about the result of a linking process.
/// </summary>
public class LinkingResult
{
    public string[] Warnings { get; set; }

    public string[] Errors { get; set; }

    public ProgramData[] ProgramsData { get; set; }

    public ushort StartAddress { get; set; }

    public ushort EndAddress { get; set; }

    public bool MaxErrorsReached { get; set; }

    public CommonBlock[] CommonBlocks { get; set; }
}
