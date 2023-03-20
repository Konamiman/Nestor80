using Konamiman.Nestor80.Assembler;

namespace Konamiman.Nestor80.Linker.Parsing;

/// <summary>
/// Represents a relocatable address (relative to code, data or common segment) in a relocatable file.
/// </summary>
public class RelocatableAddress : IRelocatableFilePart
{
    public AddressType Type { get; set; }

    public ushort Value { get; set; }

    public override string ToString() => $"{Type} {Value:X4}";
}
