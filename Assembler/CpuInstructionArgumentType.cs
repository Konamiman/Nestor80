namespace Konamiman.Nestor80.Assembler
{
    internal enum CpuInstructionArgumentType
    {
        None,
        Byte,
        Word,
        ByteInParenthesis,
        WordInParenthesis,
        OffsetFromCurrentLocation,
        IxyOffset,
        IyOffset
    }
}
