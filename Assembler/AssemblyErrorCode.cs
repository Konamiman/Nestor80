namespace Konamiman.Nestor80.Assembler
{
    public enum AssemblyErrorCode : byte
    {
		FatalError = 1,
        UnexpectedError,
        SourceLineTooLong,

        FirstWarning = 128,

        /*
ArgumentError,
ConditionalNestingError,
DoubleDefinedSymbol,
ExternalError,
MultiplyDefinedSymbol,
NumberError,
ObjectionableSyntax,
PhaseError,
Questionable,
Relocation,
UndefinedSymbol,
ValueError
*/
    }
}
