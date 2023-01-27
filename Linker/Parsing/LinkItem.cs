using Konamiman.Nestor80.Assembler.Relocatable;

namespace Konamiman.Nestor80.Linker.Parsing;

public class LinkItem : IRelocatableFilePart
{
    public LinkItemType Type { get; set; }

    public RelocatableAddress Address { get; set; } = null;

    public ExtensionLinkItemType? ExtendedType { get; set; } = null;

    public byte[] SymbolBytes { get; set; } = null;

    public string Symbol { get; set; } = null;

    public override string ToString()
    {
        var s = Type.ToString();
        if(ExtendedType != null) {
            s += ", " + ExtendedType;
        }

        if(Address != null) {
            s += ", " + Address.ToString();
        }

        if(Symbol != null && !Symbol.Any(c => char.IsControl(c))) {
            s += ", " + Symbol;
        }

        if(SymbolBytes != null) {
            s += ", " + string.Join(' ', SymbolBytes.Select(b => b.ToString("X2")).ToArray());
        }

        return s;
    }
}
