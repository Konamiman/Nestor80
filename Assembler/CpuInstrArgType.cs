namespace Konamiman.Nestor80.Assembler
{
    internal enum CpuInstrArgType
    {
        None,
        Byte,
        Word,
        ByteInParenthesis,
        WordInParenthesis,
        OffsetFromCurrentLocation,
        IxOffset,
        IyOffset
    }
}
