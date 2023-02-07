namespace Konamiman.Nestor80.Linker
{
    internal class ProgramInfo
    {
        public string ProgramName { get; set; }

        public ushort CodeSegmentStart { get; set; }

        public ushort CodeSegmentEnd { get; set; }

        public ushort DataSegmentStart { get; set; }

        public ushort DataSegmentEnd { get; set; }

        public ushort AbsoluteSegmentStart { get; set; }

        public ushort AbsoluteSegmentEnd { get; set; }

        public Dictionary<string, Tuple<ushort, ushort>> CommonBlocks { get; set; }

        public string[] PublicSymbols { get; set; }

        public AddressRange AbsoluteSegmentRange { get; set; }

        public AddressRange CodeSegmentRange { get; set; }

        public AddressRange DataSegmentRange { get; set; }

        public bool HasCode { get; set; }

        public bool HasData { get; set; }

        public bool HasAbsolute { get; set; }

        public void RebuildRanges()
        {
            CodeSegmentRange = HasCode ? new AddressRange(CodeSegmentStart, CodeSegmentEnd, Assembler.AddressType.CSEG) : null;
            DataSegmentRange = HasData ? new AddressRange(DataSegmentStart, DataSegmentEnd, Assembler.AddressType.DSEG) : null;
            AbsoluteSegmentRange = HasAbsolute ? new AddressRange(AbsoluteSegmentStart, AbsoluteSegmentEnd, Assembler.AddressType.ASEG) : null;
        }

        public ushort MaxSegmentEnd => 
            HasAbsolute ? 
            Math.Max(Math.Max(CodeSegmentEnd, DataSegmentEnd), AbsoluteSegmentEnd) :
            Math.Max(CodeSegmentEnd, DataSegmentEnd);

        public override string ToString()
        {
            return $"{ProgramName} - CSEG: {CodeSegmentStart:X4}h - {CodeSegmentEnd:X4}h; DSEG: {DataSegmentStart:X4}h - {DataSegmentEnd:X4}h";
        }
    }
}
