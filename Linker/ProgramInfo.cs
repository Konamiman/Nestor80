using Konamiman.Nestor80.Assembler;

namespace Konamiman.Nestor80.Linker;

internal class ProgramInfo
{
    public string ProgramName { get; set; }

    public ushort CodeSegmentStart { get; set; }

    public ushort CodeSegmentEnd { get; set; }

    public ushort DataSegmentStart { get; set; }

    public ushort DataSegmentEnd { get; set; }

    public ushort CommonSegmentStart { get; set; }

    public ushort CommonSegmentEnd { get; set; }

    public ushort AbsoluteSegmentStart { get; set; }

    public ushort AbsoluteSegmentEnd { get; set; }

    public CommonBlock[] CommonBlocks { get; set; }

    public string[] PublicSymbols { get; set; }

    public AddressRange AbsoluteSegmentRange { get; set; }

    public AddressRange CodeSegmentRange { get; set; }

    public AddressRange DataSegmentRange { get; set; }

    public AddressRange CommonSegmentRange { get; set; }

    public AddressRange RangeOf(AddressType type) =>
        type switch {
            AddressType.ASEG => AbsoluteSegmentRange,
            AddressType.CSEG => CodeSegmentRange,
            AddressType.DSEG => DataSegmentRange,
            AddressType.COMMON => CommonSegmentRange,
            _ => throw new InvalidOperationException($"{nameof(ProgramInfo)}.{nameof(RangeOf)}: unexcpected type: {type}")
        };

    public bool HasCode { get; set; }

    public bool HasData { get; set; }

    public bool HasAbsolute { get; set; }

    public bool HasCommons { get; set; }

    public bool HasContent => HasCode || HasData || HasAbsolute || HasCommons;

    public bool Has(AddressType type) =>
        type switch {
            AddressType.ASEG => HasAbsolute,
            AddressType.CSEG => HasCode,
            AddressType.DSEG => HasData,
            AddressType.COMMON => HasCommons,
            _ => throw new InvalidOperationException($"{nameof(ProgramInfo)}.{nameof(Has)}: unexcpected type: {type}")
        };

    public void RebuildRanges()
    {
        CodeSegmentRange = HasCode ? new AddressRange(CodeSegmentStart, CodeSegmentEnd, Assembler.AddressType.CSEG) : null;
        DataSegmentRange = HasData ? new AddressRange(DataSegmentStart, DataSegmentEnd, Assembler.AddressType.DSEG) : null;
        CommonSegmentRange = HasCommons ? new AddressRange(CommonSegmentStart, CommonSegmentEnd, Assembler.AddressType.COMMON) : null;
        AbsoluteSegmentRange = HasAbsolute ? new AddressRange(AbsoluteSegmentStart, AbsoluteSegmentEnd, Assembler.AddressType.ASEG) : null;
    }

    // The "end" address of an empty segment is set to the segment start address,
    // which points one byte past the actual end of the program
    // (it's the address at which the segment would have started);
    // thus for empty segments the previous address needs to be considered instead.
    public ushort MaxSegmentEnd
    {
        get
        {
            var maxEnd = Math.Max(
                HasCode ? CodeSegmentEnd : CodeSegmentStart - 1,
                HasData ? DataSegmentEnd : DataSegmentStart - 1);
            if(HasAbsolute) {
                maxEnd = Math.Max(maxEnd, AbsoluteSegmentEnd);
            }
            return (ushort)Math.Max(maxEnd, 0);
        }
    }

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
