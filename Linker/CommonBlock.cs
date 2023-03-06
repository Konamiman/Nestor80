namespace Konamiman.Nestor80.Linker;

public class CommonBlock
{
    public string Name { get; set; }

    public ushort StartAddress { get; set; }

    public ushort Size { get; set; }

    public string DefinedInProgram { get; set; }
}
