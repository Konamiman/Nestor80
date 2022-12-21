namespace Konamiman.Nestor80.Assembler.Errors
{
    public enum AssemblyErrorCode : byte
    {
        //Warnings

        None = 0,
        NoEndStatement = 1,
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
        PhaseWithoutArgument,
        DephaseWithoutPhase,
        UnterminatedPhase,
        SameEffectiveExternal,
        SameEffectiveCommon,
        UnterminatedModule,
        RootWithoutModule,
        TruncatedRequestFilename,
        MissingDelimiterInMacroArgsList,
        DuplicatedMacro,
        UserWarning,
        LastWarning = UserWarning,

        //Normal errors

        FirstError = 64,
        InvalidExpression = 64,
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
        InvalidNestedPhase,
        InvalidInPhased,
        DifferentPassValues,
        SameEffectivePublic,
        UnknownInstruction,
        EndModuleOutOfScope,
        EndMacroOutOfScope,
        UnterminatedMacro,
        NestedMacro,
        ExitmOutOfScope,
        LocalOutOfMacro,
        UserError,
        LasetError = UserError,

        //Fatal errors

        FirstFatal = 128,
        UnexpectedError = 128,
        SourceLineTooLong,
        UnsupportedCpu,
        CantInclude,
        TooManyNestedIncludes,
        IncludeInPass2Only,
        MaxErrorsReached,
        UserFatal,
        LastFatal = UserFatal,
    }
}
