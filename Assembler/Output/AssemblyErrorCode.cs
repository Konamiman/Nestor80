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
        UnterminatedConditional,
        UserWarning,

        FirstError = 64,
        InvalidExpression,
        InvalidArgument,
        MissingValue,
        InvalidLabel,
        DuplicatedSymbol,
        UnknownStringEncoding,
        InvalidCpuInstruction,
        InvalidForAbsoluteOutput,
        ConditionalOutOfScope,
        UnknownSymbol,
        InvalidForRelocatable,
        UserError,

        FirstFatal = 128,
        UnexpectedError,
        SourceLineTooLong,
        UnsupportedCpu,
        CantInclude,
        TooManyNestedIncludes,
        UserFatal
    }
}
