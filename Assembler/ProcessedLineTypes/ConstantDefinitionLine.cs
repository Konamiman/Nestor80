namespace Konamiman.Nestor80.Assembler.Output
{
    public class ConstantDefinitionLine : ProcessedSourceLine
    {
        public AddressType ValueArea { get; set; }

        public ushort Value { get; set; }

        public string Name { get; set; }

        public bool IsRedefinible { get; set; }

        public override string ToString() => $"{base.ToString()}, {Name} = {Value}";
    }
}
