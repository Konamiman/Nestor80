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
        IgnoredForAbsoluteOutput,

        FirstError = 64,
        InvalidExpression,
        InvalidArgument,
        MissingValue,
        InvalidLabel,
        DuplicatedSymbol,
        UnknownStringEncoding,
        InvalidCpuInstruction,
        InvalidForAbsoluteOutput,

        FirstFatal = 128,
        UnexpectedError,
        SourceLineTooLong,
        UnsupportedCpu
    }
}
