namespace Konamiman.Nestor80.Assembler.Output
{
    public enum AssemblyErrorCode : byte
    {
        None = 0,
        NoEndStatement,

        FirstError = 64,
        InvalidExpression,

        FirstFatal = 128,
        UnexpectedError,
        SourceLineTooLong,
    }
}
