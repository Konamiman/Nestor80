namespace Konamiman.Nestor80.Assembler.Output
{
    public class AssemblyResult
    {
        public string ProgramName { get; init; }

        public int ProgramAreaSize { get; init; }

        public int DataAreaSize { get; init; }

        public Dictionary<string, int> CommonAreaSizes { get; init; }

        public AssemblyError[] Errors { get; init; }

        public ProcessedSourceLine[] ProcessedLines { get; init; }

        public Symbol[] Symbols { get; init; }

        public AddressType EndAddressArea { get; init; }

        public ushort EndAddress { get; init; }
    }
}
