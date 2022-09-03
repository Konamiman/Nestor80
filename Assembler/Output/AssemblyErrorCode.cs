namespace Konamiman.Nestor80.Assembler.Output
{
    public enum AssemblyErrorCode : byte
    {
        None = 0,
        NoEndStatement,
        UnexpectedContentAtEndOfLine,
        MaskedDollar,

        FirstError = 64,
        InvalidExpression,
        MisssingOperand,
        InvalidLabel,
        DuplicateLabel,

        FirstFatal = 128,
        UnexpectedError,
        SourceLineTooLong,
    }
}
