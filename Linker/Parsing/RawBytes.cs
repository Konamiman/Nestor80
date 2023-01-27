namespace Konamiman.Nestor80.Linker.Parsing;

public class RawBytes : IRelocatableFilePart
{
    public byte[] Bytes { get; set; }

    public override string ToString()
    {
        return string.Join(' ',Bytes.Select(b => b.ToString("X2")).ToArray());
    }
}
