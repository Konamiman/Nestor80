namespace Konamiman.Nestor80.Assembler.Infrastructure
{
    /// <summary>
    /// Type of *actual* argument found in a CPU instruction.
    /// </summary>
    internal enum CpuParsedArgType
    {
        None,
        Fixed,
        Number,
        NumberInParenthesis,
        IxPlusOffset,
        IyPlusOffset,
        PcPlusOffset,
        SpPlusOffset,
        HlPlusOffset
    }
}
