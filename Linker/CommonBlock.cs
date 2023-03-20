namespace Konamiman.Nestor80.Linker;

/// <summary>
/// Represents the definition of a common block found during the linking process.
/// </summary>
public class CommonBlock
{
    public string Name { get; set; }

    /// <summary>
    /// The absolute Z80 memory address where the common block starts in the generated binary file.
    /// </summary>
    public ushort StartAddress { get; set; }

    public ushort Size { get; set; }

    /// <summary>
    /// The name of the program where this common block was first defined.
    /// </summary>
    public string DefinedInProgram { get; set; }
}
