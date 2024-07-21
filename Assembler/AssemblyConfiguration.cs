using System.Text;

namespace Konamiman.Nestor80.Assembler
{
    /// <summary>
    /// Assembly process configuration object. An instance of this object needs
    /// to be passed to <see cref="AssemblySourceProcessor.Assemble(Stream, Encoding, AssemblyConfiguration)"/>.
    /// </summary>
    public class AssemblyConfiguration
    {
        /// <summary>
        /// Name of the encoding to use to convert strings to bytes in DEFB instructions.
        /// This encoding can also be changed in code by using the .STRENC instruction.
        /// </summary>
        public string OutputStringEncoding { get; init; } = "ASCII";

        /// <summary>
        /// Build type: absolute, relocatable or automatic (select based on the code itself:
        /// will be absolute if ORG is found before a CPU instruction or any of these:
        /// CSEG, DSEG, COMMON, DB, DW, DS, DC, DM, DZ, PUBLIC, EXTRN, .REQUEST; otherwise
        /// it will be relocatable).
        /// </summary>
        public BuildType BuildType { get; init; } = BuildType.Automatic;

        /// <summary>
        /// Name of the target CPU. This can also be changed in code with the .CPU instruction.
        /// </summary>
        public string CpuName { get; init; } = "Z80";

        /// <summary>
        /// Allow escape sequences in strings or not (needs to be disabled for old
        /// code that contains literal "\" characters in strings).
        /// </summary>
        public bool AllowEscapesInStrings { get; init; } = true;

        /// <summary>
        /// Callback to use for INCLUDE instructions, the parameter is the name of
        /// the file requested and the return value is a stream to read source code from.
        /// </summary>
        public Func<string, Stream> GetStreamForInclude { get; init; } = _ => null;

        /// <summary>
        /// Callback to use for INCBIN instructions, the parameter is the name of
        /// the file requested and the return value is a stream to read source code from.
        /// </summary>
        public Func<string, Stream> GetStreamForIncbin { get; init; } = _ => null;

        /// <summary>
        /// List of predefined symbols as pairs of name-value, they will be registerd
        /// as if they were defined with DEFL.
        /// </summary>

        public (string, ushort)[] PredefinedSymbols = Array.Empty<(string, ushort)>();

        /// <summary>
        /// Maximum number of allowed assembly errors, assembly process will stop if reached;
        /// 0 means "infinite".
        /// </summary>
        public int MaxErrors { get; init; } = 0;

        /// <summary>
        /// Allow or not bare expressions in code, these are treated as DEFB statements;
        /// e.g. FOO: 1,2,3 qill be treated as FOO: DEFB 1,2,3
        /// </summary>
        public bool AllowBareExpressions { get; init; } = false;

        /// <summary>
        /// Allow or not relative labels (they start wiht a dot and are relative
        /// to the last non-relative label).
        /// </summary>
        public bool AllowRelativeLabels { get; init; } = false;

        /// <summary>
        /// Maximum amount of content that will be read from files included with the INCBIN instruction.
        /// </summary>
        public int MaxIncbinFileSize { get; init; } = 65536;

        /// <summary>
        /// True to produce relocatable files that are compatible with LINK-80
        /// (so public and external symbols are limited to 6 ASCII-only characters, and the set of
        /// arithmetic operators allowed for expressions with external references is limited).
        /// </summary>
        public bool Link80Compatibility { get; init; } = false;

        /// <summary>
        /// True if a hash character (#) present at the beginning of an expression needs to be discarded
        /// before evaluating the expression. This is useful for assembling sources intended for the
        /// SDAS assembler, which expects numeric constants to be prefixed with a hash character.
        /// </summary>
        public bool DiscardHashPrefix { get; init; } = false;

        /// <summary>
        /// True if instruction aliases with a dot prefix (e.g. ".DS" as an alias of "DS") are accepted.
        /// </summary>
        public bool AcceptDottedInstructionAliases { get; set; } = false;

        /// <summary>
        /// True to consider symbols that are unknown in pass 2 as external symbol references.
        /// </summary>
        public bool TreatUnknownSymbolsAsExternals { get; set; } = false;

        /// <summary>
        /// Character sequence to use as end of line markers for text files.
        /// </summary>
        public string EndOfLine { get; set; } = Environment.NewLine;
    }
}
