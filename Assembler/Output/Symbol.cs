namespace Konamiman.Nestor80.Assembler.Output
{
    public class Symbol
    {
        public string Name { get; set; }

        public SymbolType Type { get; set; }

        public AddressType ValueArea { get; set; }

        public ushort Value { get; set; }

        public string CommonName { get; set; }

        public override string ToString() => $"{Name} = {ValueArea} {Value}, {Type}, {CommonName}";
    }
}
