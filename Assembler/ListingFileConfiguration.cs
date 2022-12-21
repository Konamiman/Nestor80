namespace Konamiman.Nestor80.Assembler
{
    /// <summary>
    /// Listing generation configuration object passed to
    /// <see cref="ListingFileGenerator.GenerateListingFile(AssemblyResult, StreamWriter, ListingFileConfiguration)"/>
    /// </summary>
    public class ListingFileConfiguration
    {
        /// <summary>
        /// How many symbols to include in each line of the symbols listing.
        /// </summary>
        public int SymbolsPerRow { get; set; }

        /// <summary>
        /// How many characters to print at most for each symbol in the symbols listing,
        /// longer symbols get their names truncated in the listing.
        /// </summary>
        public int MaxSymbolLength { get; set; }

        /// <summary>
        /// Whether the listing should include the source code or not.
        /// </summary>
        public bool ListCode { get; set; }

        /// <summary>
        /// Whether the listing should include the symbols and macros list or not.
        /// </summary>
        public bool ListSymbols { get; set; }

        /// <summary>
        /// Whether false conditional blocks should be included in the listing or not
        /// (this can also be set in code with the .TFCOND, .LFCOND and .SFCOND instructions).
        /// </summary>
        public bool ListFalseConditionals { get; set; }

        /// <summary>
        /// How many bytes to put in one single listing line for instructions that can generate
        /// an arbitraty number of output bytes such as DEFB and DEFW; as many lines as needed
        /// are generated if the bytes of the instruction don't fit in one.
        /// </summary>
        public int BytesPerRow { get; set; }

        /// <summary>
        /// Whether the symbol names in the symbols list should be kept as they are in the source
        /// or should be uppercased as Macro80 does when generating symbol listins.
        /// </summary>
        public bool UppercaseSymbolNames { get; set; }

        /// <summary>
        /// Text to put between the listing title set with .TITLE and the page number
        /// at the beginning of each listing page.
        /// </summary>
        public string TitleSignature { get; set; }
    }
}
