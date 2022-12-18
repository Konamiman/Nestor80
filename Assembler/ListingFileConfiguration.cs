namespace Konamiman.Nestor80.Assembler
{
    public class ListingFileConfiguration
    {
        public int SymbolsPerRow { get; set; }

        public int MaxSymbolLength { get; set; }

        public bool ListCode { get; set; }

        public bool ListSymbols { get; set; }

        public bool ListFalseConditionals { get; set; }

        public int BytesPerRow { get; set; }

        public bool UppercaseSymbolNames { get; set; }
    }
}
