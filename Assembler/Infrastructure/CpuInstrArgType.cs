namespace Konamiman.Nestor80.Assembler.Infrastructure
{
    /// <summary>
    /// Type of *defined* argument for a CPU instruction.
    /// </summary>
    internal enum CpuInstrArgType
    {
        None,
        Byte,
        Word,
        ByteInParenthesis,
        WordInParenthesis,
        OffsetFromCurrentLocation,
        IxOffset,
        IyOffset,
        PcOffset,
        SpOffset,
        HlOffset,
        IxOffsetLong,
        IyOffsetLong,
        OffsetFromCurrentLocationMinusOne
    }
}
