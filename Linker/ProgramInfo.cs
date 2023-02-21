using Konamiman.Nestor80.Assembler;

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

        public AddressRange RangeOf(AddressType type) =>
            type switch {
                AddressType.ASEG => AbsoluteSegmentRange,
                AddressType.CSEG => CodeSegmentRange,
                AddressType.DSEG => DataSegmentRange,
                _ => throw new InvalidOperationException($"{nameof(ProgramInfo)}.{nameof(RangeOf)}: unexcpected type: {type}")
            };

        public bool HasCode { get; set; }

        public bool HasData { get; set; }

        public bool HasAbsolute { get; set; }

        public bool HasContent => HasCode || HasData || HasAbsolute;

        public bool Has(AddressType type) =>
            type switch {
                AddressType.ASEG => HasAbsolute,
                AddressType.CSEG => HasCode,
                AddressType.DSEG => HasData,
                _ => throw new InvalidOperationException($"{nameof(ProgramInfo)}.{nameof(Has)}: unexcpected type: {type}")
            };

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

        public ProgramData ToProgramData(Dictionary<string, ushort> allKnownSymbols)
        {
            return new ProgramData() {
                CodeSegmentStart = CodeSegmentStart,
                CodeSegmentSize = (ushort)(HasCode ? CodeSegmentEnd - CodeSegmentStart + 1 : 0),
                DataSegmentStart = DataSegmentStart,
                DataSegmentSize = (ushort)(HasData ? DataSegmentEnd - DataSegmentStart + 1 : 0),
                AbsoluteSegmentStart = AbsoluteSegmentStart,
                AbsoluteSegmentSize = (ushort)(HasAbsolute ? AbsoluteSegmentEnd - AbsoluteSegmentStart + 1 : 0),
                PublicSymbols = new(allKnownSymbols.Where(s => PublicSymbols.Contains(s.Key))),
                ProgramName = ProgramName
            };
        }

        public override string ToString()
        {
            return $"{ProgramName} - CSEG: {CodeSegmentStart:X4}h - {CodeSegmentEnd:X4}h; DSEG: {DataSegmentStart:X4}h - {DataSegmentEnd:X4}h";
        }
    }
}
