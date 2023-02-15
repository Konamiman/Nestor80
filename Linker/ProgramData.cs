namespace Konamiman.Nestor80.Linker
{
    public class ProgramData
    {
        public string ProgramName { get; set; }

        public ushort CodeSegmentStart { get; set; }

        public ushort CodeSegmentSize { get; set; }

        public ushort DataSegmentStart { get; set; }

        public ushort DataSegmentSize { get; set; }

        public ushort AbsoluteSegmentStart { get; set; }

        public ushort AbsoluteSegmentSize { get; set; }

        public Dictionary<string, ushort> PublicSymbols { get; set; }
    }
}
