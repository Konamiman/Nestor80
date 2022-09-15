namespace Konamiman.Nestor80.Assembler.Output
{
    public enum AssemblyErrorCode : byte
    {
        None = 0,
        NoEndStatement,
        UnexpectedContentAtEndOfLine,
        DollarAsLabel,
        LineHasNoEffect,
        UnterminatedComment,
        StringHasBytesWithHighBitSet,
        InvalidListingPageSize,
        SymbolWithCpuRegisterName,
        ConfusingOffset,

        FirstError = 64,
        InvalidExpression,
        InvalidArgument,
        MissingValue,
        InvalidLabel,
        DuplicatedSymbol,
        UnknownStringEncoding,
        InvalidCpuInstruction,

        FirstFatal = 128,
        UnexpectedError,
        SourceLineTooLong,
        UnsupportedCpu
    }
}
