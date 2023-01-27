using Konamiman.Nestor80.Assembler;

namespace Konamiman.Nestor80.Linker.Parsing;

public class RelocatableAddress : IRelocatableFilePart
{
    public AddressType Type { get; set; }

    public ushort Value { get; set; }

    public string CommonBlockName { get; set; }

    public override string ToString() => $"{Type} {Value:X4}";
}
