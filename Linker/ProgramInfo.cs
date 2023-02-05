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

        public AddressRange CodeSegmentRange { get; set; }

        public AddressRange DataSegmentRange { get; set; }

        public void RebuildRanges()
        {
            CodeSegmentRange = new AddressRange(CodeSegmentStart, CodeSegmentEnd, Assembler.AddressType.CSEG);
            DataSegmentRange = new AddressRange(DataSegmentStart, DataSegmentEnd, Assembler.AddressType.DSEG);
        }

        public ushort MaxSegmentEnd => Math.Max(CodeSegmentEnd, DataSegmentEnd);

        public override string ToString()
        {
            return $"{ProgramName} - CSEG: {CodeSegmentStart:X4}h - {CodeSegmentEnd:X4}h; DSEG: {DataSegmentStart:X4}h - {DataSegmentEnd:X4}h";
        }
    }
}
