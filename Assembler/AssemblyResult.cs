using Konamiman.Nestor80.Assembler.Errors;

namespace Konamiman.Nestor80.Assembler
{
    /// <summary>
    /// Represents the result of a source code assembly processing.
    /// This is returned by <see cref="AssemblySourceProcessor.Assemble(Stream, System.Text.Encoding, AssemblyConfiguration)"/>.
    /// </summary>
    public class AssemblyResult
    {
        public string ProgramName { get; set; }

        public int ProgramAreaSize { get; set; }

        public int DataAreaSize { get; set; }

        public Dictionary<string, int> CommonAreaSizes { get; set; }

        public AssemblyError[] Errors { get; set; }

        public ProcessedSourceLine[] ProcessedLines { get; set; }

        public Symbol[] Symbols { get; set; }

        /// <summary>
        /// The area of the end address if specified in an END instruction, otherwise zero.
        /// </summary>
        public AddressType EndAddressArea { get; set; }

        /// <summary>
        /// The end address if specified in an END instruction, otherwise zero.
        /// </summary>
        public ushort EndAddress { get; set; }

        public BuildType BuildType { get; set; }

        /// <summary>
        /// The maximum length allowed for relocatable symbols, as limited by
        /// the target relocatable file format.
        /// </summary>
        public int MaxRelocatableSymbolLength { get; set; }

        public bool HasWarnings => Errors.Any(e => e.IsWarning);

        public bool HasErrors => Errors.Any(e => !e.IsWarning);

        public bool HasNonFatalErrors => Errors.Any(e => !e.IsWarning && !e.IsFatal);

        public bool HasFatalErrors => Errors.Any(e => e.IsFatal);

        public string EffectiveRelocatableSymbolLength(string symbol) =>
            symbol.Length > MaxRelocatableSymbolLength ? symbol[..MaxRelocatableSymbolLength] : symbol;

        public string[] MacroNames { get; set; }
    }
}
