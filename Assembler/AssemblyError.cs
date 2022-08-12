namespace Konamiman.Nestor80.Assembler
{
    internal enum AssemblyError : byte
    {
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
	}
}
