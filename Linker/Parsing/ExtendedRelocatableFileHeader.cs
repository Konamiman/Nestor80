namespace Konamiman.Nestor80.Linker.Parsing;

/// <summary>
/// Represents the extended relocatable file header.
/// </summary>
public class ExtendedRelocatableFileHeader : IRelocatableFilePart
{
	private ExtendedRelocatableFileHeader()
	{
	}

    public static readonly byte[] Bytes = { 0x85, 0xD3, 0x13, 0x92, 0xD4, 0xD5, 0x13, 0xD4, 0xA5, 0x00, 0x00, 0x13, 0x8F, 0xFF, 0xF0, 0x9E };

    public static readonly ExtendedRelocatableFileHeader Instance = new();

    public override string ToString() => "Extended relocatable file header";
}
