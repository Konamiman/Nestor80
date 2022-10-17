namespace Konamiman.Nestor80.Assembler.Output
{
    public class AssemblyResult
    {
        public string ProgramName { get; set; }

        public int ProgramAreaSize { get; set; }

        public int DataAreaSize { get; set; }

        public Dictionary<string, int> CommonAreaSizes { get; set; }

        public AssemblyError[] Errors { get; set; }

        public ProcessedSourceLine[] ProcessedLines { get; set; }

        public Symbol[] Symbols { get; set; }

        public AddressType EndAddressArea { get; set; }

        public ushort EndAddress { get; set; }

        public BuildType BuildType { get; set; }

        public bool HasErrors => Errors.Any(e => !e.IsWarning && !e.IsFatal);

        public bool HasFatals => Errors.Any(e => e.IsFatal);
    }
}
