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

        FirstError = 64,
        InvalidExpression,
        InvalidArgument,
        MissingValue,
        InvalidLabel,
        DuplicatedSymbol,
        UnknownStringEncoding,

        FirstFatal = 128,
        UnexpectedError,
        SourceLineTooLong,
    }
}
