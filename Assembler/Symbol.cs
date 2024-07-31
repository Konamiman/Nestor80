using Konamiman.Nestor80.Assembler.Infrastructure;

namespace Konamiman.Nestor80.Assembler
{
    /// <summary>
    /// This class contains information about a symbol defined or referenced
    /// while processing the source code.
    /// </summary>
    /// <remarks>
    /// This is a public class and is used to return the symbols list in <see cref="AssemblyResult"/>.
    /// <see cref="SymbolInfo"/>, on the other hand, is an internal class used during the assembly process.
    /// </remarks>
    public class Symbol
    {
        public string Name { get; set; }

        /// <summary>
        /// If the symbol is declared as public its effective name (the name that will be used to refer to the symbol
        /// in the resulting relocatable file) is the original name truncated to 6 characters.
        /// This is a limitation of the LINK-80 relocatable file format.
        /// </summary>
        public string EffectiveName { get; set; }

        public SymbolType Type { get; set; }

        public bool IsPublic { get; set; }

        public AddressType ValueArea { get; set; }

        public ushort Value { get; set; }

        /// <summary>
        /// Name of the COMMON block the symbol belongs to, if the symbol
        /// belongs to a COMMON block.
        /// </summary>
        public string CommonName { get; set; }

        public string SdccAreaName { get; set; }

        public override string ToString() => $"{Name} = {ValueArea} {Value:X4}, {Type}, {CommonName}{(SdccAreaName is null ? "" : ", area: " + SdccAreaName)}";
    }
}
