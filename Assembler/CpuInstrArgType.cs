namespace Konamiman.Nestor80.Assembler
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
        IyOffset
    }
}
