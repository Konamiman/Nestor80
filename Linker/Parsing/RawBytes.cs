namespace Konamiman.Nestor80.Linker.Parsing;

/// <summary>
/// Represents a sequence of raw (uninterpreted) bytes in a relocatable file.
/// </summary>
public class RawBytes : IRelocatableFilePart
{
    public byte[] Bytes { get; set; }

    public override string ToString()
    {
        return string.Join(' ',Bytes.Select(b => b.ToString("X2")).ToArray());
    }
}
