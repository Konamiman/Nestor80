﻿namespace Konamiman.Nestor80.Assembler.Output
{
    public enum AssemblyErrorCode : byte
    {
        None = 0,
        NoEndStatement,
        UnexpectedContentAtEndOfLine,
        DollarAsLabel,
        LineHasNoEffect,

        FirstError = 64,
        InvalidExpression,
        MissingValue,
        InvalidLabel,
        DuplicatedSymbol,

        FirstFatal = 128,
        UnexpectedError,
        SourceLineTooLong,
    }
}
