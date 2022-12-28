using Konamiman.Nestor80.Assembler.Errors;
using Konamiman.Nestor80.Assembler.Expressions;
using Konamiman.Nestor80.Assembler.Expressions.ExpressionParts;
using Konamiman.Nestor80.Assembler.Infrastructure;
using Konamiman.Nestor80.Assembler.Output;
using Konamiman.Nestor80.Assembler.Relocatable;
using System.Text;
using System.Text.RegularExpressions;

namespace Konamiman.Nestor80.Assembler
{
    //This file contains the processing code for the assembler pseudo-operators (all the instructions that aren't CPU instructions).

    public partial class AssemblySourceProcessor
    {
        /// <summary>
        /// Dictionary of processing callbacks by instruction code.
        /// Aliases are handled by defining additional entries for the same callback.
        /// See <see cref="ProcessPrintOrUserErrorLine"/> for the allowed expression interpolation formats.
        /// </summary>
        readonly static Dictionary<string, Func<string, SourceLineWalker, ProcessedSourceLine>> PseudoOpProcessors = new(StringComparer.OrdinalIgnoreCase) {
            
            // $EJECT [<page size>]: Alias for PAGE
            { "$EJECT", ProcessSetListingNewPageLine },

            // $TITLE('title'): Alias for SUBTTL
            { "$TITLE", ProcessLegacySetListingSubtitleLine },

            // .8080: Unsupported, always throws error
            { ".8080", ProcessChangeCpuTo8080Line },

            // .COMMENT <delimiter>: Start multiline comment
            { ".COMMENT", ProcessDelimitedCommentStartLine },

            // .CPU <cpu>: Change current CPU
            { ".CPU", ProcessChangeCpuLine },

            // .CREF: Unsupported, does nothing
            { ".CREF", ProcessListingControlLine },

            // .DEPHASE: End a phased block
            { ".DEPHASE", ProcessDephaseLine },

            // .ERROR <text>: Produce an assembly normal error
            { ".ERROR", ProcessUserErrorLine },

            // .FATAL <text>: Produce an assembly fatal error
            { ".FATAL", ProcessUserFatalLine },

            // .LALL: List all lines in macro expansions in listing
            { ".LALL", ProcessListingControlLine },

            // .LFCOND: List all lines in false conditional blocks in listing
            { ".LFCOND", ProcessListingControlLine },

            // .LIST: Restore code output in listing
            { ".LIST", ProcessListingControlLine },

            // .PHASE <address>: Start phased block
            { ".PHASE", ProcessPhaseLine },

            // .PRINT <text>: Print message in both passes
            { ".PRINT", ProcessPrintLine },

            // .PRINT1 <text>: Print message in pass 1
            { ".PRINT1", ProcessPrint1Line },

            // .PRINT2 <text>: Print message in pass 2
            { ".PRINT2", ProcessPrint2Line },

            // .PRINTX <delimiter><text>[<delimiter>]: Legacy print message
            { ".PRINTX", ProcessPrintxLine },

            // .RADIX <radix>: Change default radix for numbers
            { ".RADIX", ProcessChangeRadixLine },

            // .RELAB: Enable relative labels
            { ".RELAB", ProcessRelabLine },

            // .REQUEST: Request symbols from an external file
            { ".REQUEST", ProcessRequestLinkFilesLine },

            // .SALL: Suppress output of macro expansion lines in listings
            { ".SALL", ProcessListingControlLine },

            // .SFCOND: Suppress lines in false conditional blocks in listing
            { ".SFCOND", ProcessListingControlLine },

            // .STRENC <encoding name or page>: Change the encoding used to convert strings to bytes
            { ".STRENC", ProcessSetEncodingLine },

            // .STRESC ON|OFF: Enable or disable escape sequences in strings
            { ".STRESC", ProcessChangeStringEscapingLine },

            // .TFCOND: Toggle the inclusion of false conditional blocks in listing
            //          (relative to the previous .TFCOND, unrelated to the state set by .LFCOND or .SFCOND)
            { ".TFCOND", ProcessListingControlLine },

            // .WARN <text>: Produce an assembly warning
            { ".WARN", ProcessUserWarningLine },

            // .XALL: Only list lines that produce output in macro expansions in listing
            { ".XALL", ProcessListingControlLine },

            // .XCREF: Unsupported, does nothing
            { ".XCREF", ProcessListingControlLine },

            // .XLIST: Suppress output of code lines in listing
            { ".XLIST", ProcessListingControlLine },

            // .XRELAB: Disable relative labels
            { ".XRELAB", ProcessXRelabLine },
            
            // .Z80: Select Z80 as the current CPU
            { ".Z80", ProcessChangeCpuToZ80Line },

            // ASEG: Select the absolute segment
            { "ASEG", ProcessAsegLine },

            // COMMON /<name>/: Select a common block
            { "COMMON", ProcessCommonLine },

            // COND: Alias for IF
            { "COND", ProcessIfTrueLine },

            // CONTM: Finish macro expansion but proceed with next repetiton
            { "CONTM", ProcessContmLine },

            // CSEG: Select the code segment
            { "CSEG", ProcessCsegLine },

            // DB: Alias for DEFB
            { "DB", ProcessDefbLine },

            // DC <string>: Define string with last cahacter having MSB set
            { "DC", ProcessDcLine },

            // DEFB <byte or string>[,<byte or string>[,...]]: Define sequence of bytes
            { "DEFB", ProcessDefbLine },

            // DEFM: Alias for DRFB
            { "DEFM", ProcessDefbLine },

            // DEFB <size>[,<value>]: Define space
            { "DEFS", ProcessDefsLine },

            // DEFW <word>[,<word>[,...]]: Define words
            { "DEFW", ProcessDefwLine },

            // DEFZ <byte or string>[,<byte or string>[,...]]: Define sequence of bytes
            //      with a zero character (converted according to current encoding) appended at the end
            { "DEFZ", ProcessDefzLine },

            // DS: Alias for DEFS
            { "DS", ProcessDefsLine },

            // DSEG: Select the data segment
            { "DSEG", ProcessDsegLine },

            // DW: Alias for DEFW
            { "DW", ProcessDefwLine },

            // DZ: Alias for DEFZ
            { "DZ", ProcessDefzLine },

            // ELSE: Start alternate conditional block 
            { "ELSE", ProcessElseLine },

            // END: End the assembly process
            { "END", ProcessEndLine },

            // ENDC: Alias for ENDIF
            { "ENDC", ProcessEndifLine },

            // ENDIF: Finish conditional block
            { "ENDIF", ProcessEndifLine },

            // ENDM: Finish named macro declaration or repeat macro expansion
            { "ENDM", ProcessEndmLine },

            // ENDMOD: Finish current module
            { "ENDMOD", ProcessEndModuleLine },

            // ENDOUT: Continue assembling, but suppress output from this point
            { "ENDOUT", ProcessEndoutLine },

            // ENTRY: Alias for PUBLIC
            { "ENTRY", ProcessPublicDeclarationLine },

            // EXITM: Finish macro expansion inclusing pending repetitions
            { "EXITM", ProcessExitmLine },

            // EXT: Alias for EXTRN
            { "EXT", ProcessExternalDeclarationLine },

            // EXTRN: Alias for EXTRN
            { "EXTERNAL", ProcessExternalDeclarationLine },

            // EXTERNAL <symbol>[,<symbol>[,...]]: Declare symbols as externals
            { "EXTRN", ProcessExternalDeclarationLine },

            // GLOBAL: Alias for PUBLIC
            { "GLOBAL", ProcessPublicDeclarationLine },

            // IF <condition>: Start "if condition is true" block
            { "IF", ProcessIfTrueLine },

            // IF1: Start "if in pass 1" block
            { "IF1", ProcessIf1Line },

            // IF1: Start "if in pass 2" block
            { "IF2", ProcessIf2Line },

            // IFABS: Start "if in absolute assembly mode" block
            { "IFABS", ProcessIfAbsLine },

            // IFB <text>: Start "if text is blank" block (< and > are literal)
            { "IFB", ProcessIfBlankLine },

            // IFCPU <cpu name>: Start "if current cpu is" block
            { "IFCPU", ProcessIfCpuLine },

            // IFDEF <symbol>: Start "if symbol is defined" blocl
            { "IFDEF", ProcessIfDefinedLine },

            // IFDIF <text1>,<text2>: Start "if text1 1 is different from text2" block
            //       (< and > are literal)
            { "IFDIF", ProcessIfDifferentLineCaseSensitive },

            // IFDIF <text1>,<text2>: Start "if text1 is different from text2 case-insensitive" block
            //       (< and > are literal)
            { "IFDIFI", ProcessIfDifferentLineCaseInsensitive },

            //IFE: Alias for IFF
            { "IFE", ProcessIfFalseLine },

            //IFF <expression>: Start "if false" block
            { "IFF", ProcessIfFalseLine },

            // IFIDN <text1>,<text2>: Start "if text1 is identical to text2" block
            //       (< and > are literal)
            { "IFIDN", ProcessIfIdenticalLineCaseSensitive },

            // IFIDNI <text1>,<text2>: Start "if text1 is identical to text2 case-insensitive" block
            //        (< and > are literal)
            { "IFIDNI", ProcessIfIdenticalLineCaseInsensitive },

            // IFB <text>: Start "if text is not blank" block (< and > are literal)
            { "IFNB", ProcessIfNotBlankLine },

            // IFNCPU <cpu name>: Start "if current cpu is not" block
            { "IFNCPU", ProcessIfNotCpuLine },

            // IFNDEF <symbol>: Start "if symbol is not defined" block
            { "IFNDEF", ProcessIfNotDefinedLine },

            // IFREL: Start "if in relocatable mode" block
            { "IFREL", ProcessIfRelLine },

            // IFT: Alias for IF
            { "IFT", ProcessIfTrueLine },

            // IRP <dummy>,<arg[,arg[,...]]>: Start indefinite repeat macro expansion
            //                                (< and > for args are literal)
            { "IRP", ProcessIrpLine },

            // IRP <dummy>,<string>: Start repeat for each char macro expansion (raw string)
            { "IRPC", ProcessIrpcLine },

            // IRP <dummy>,<string>: Start repeat for each char macro expansion
            //                       (with string syntax as DEFB)
            { "IRPS", ProcessIrpsLine },

            // LOCAL <symbol>[,<symbol>[,...]]: Define symbols as local to the macro
            { "LOCAL", ProcessLocalLine },

            // MAINPAGE: Change the listing main page (same as encountering a line feed char)
            { "MAINPAGE", ProcessChangeListingMainPageLine },

            // MODULE <name>: Start a module
            { "MODULE", ProcessModuleLine },

            // NAME <text>: Set program name
            { "NAME", ProcessSetProgramNameLine },

            // ORG <address>: Change the location counter in the current area
            { "ORG", ProcessOrgLine },

            // PAGE [<size>]: Change the current subpage, optionally setting the new page size
            { "PAGE", ProcessSetListingNewPageLine },

            // PUBLIC <symbol>[,<symbol>[,...]]: Declare symbols as public
            { "PUBLIC", ProcessPublicDeclarationLine },

            // REPT <count>: Start a repeat macro expansion
            { "REPT", ProcessReptLine },

            // ROOT <symbol>[,<symbol>[,...]]: Declare symbols as root (defined outside the current module)
            { "ROOT", ProcessRootLine },

            // SUBPAGE: Alias for PAGE
            { "SUBPAGE", ProcessSetListingNewPageLine },

            // SUBTTL <text>: Set the subtitle for listing
            { "SUBTTL", ProcessSetListingSubtitleLine },

            // TITLE <text>: Set the title for listing
            { "TITLE", ProcessSetListingTitleLine },
        };

        static ProcessedSourceLine ProcessDefbLine(string opcode, SourceLineWalker walker)
            => ProcessDefbOrDefwLine(opcode, walker, isByte: true);

        static ProcessedSourceLine ProcessDefwLine(string opcode, SourceLineWalker walker)
            => ProcessDefbOrDefwLine(opcode, walker, isByte: false);

        static ProcessedSourceLine ProcessDefzLine(string opcode, SourceLineWalker walker)
            => ProcessDefbOrDefwLine(opcode, walker, isByte: true, addZeroAtTheEnd: true);

        static ProcessedSourceLine ProcessDefbOrDefwLine(string opcode, SourceLineWalker walker, bool isByte, bool addZeroAtTheEnd = false)
        {
            var line = new DefbLine();
            var outputBytes = new List<byte>();
            var relocatables = new List<RelocatableOutputPart>();
            var index = -1;

            void AddZero()
            {
                outputBytes.Add(0);
                if(!isByte)
                    outputBytes.Add(0);
            }

            if(walker.AtEndOfLine) {
                AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} needs at least one {(isByte ? "byte" : "word")} value");
                AddZero();
            }

            while(!walker.AtEndOfLine) {
                index++;
                var expressionText = walker.ExtractExpression();
                if(expressionText == "") {
                    if(walker.AtEndOfLine) {
                        AddError(AssemblyErrorCode.UnexpectedContentAtEndOfLine, "Unexpected ',' found at the end of the line");
                        break;
                    }
                    else {
                        AddZero();
                        AddError(AssemblyErrorCode.InvalidExpression, "Empty expression found");
                        continue;
                    }
                }
                Expression expression = null;
                try {
                    expression = state.GetExpressionFor(expressionText, forDefb: isByte, isByte: isByte);

                    if(expression.IsRawBytesOutput) {
                        var part = (RawBytesOutput)expression.Parts[0];
                        index += part.Length-1;
                        outputBytes.AddRange(part);
                        continue;
                    }

                    var value = EvaluateIfNoSymbolsOrPass2(expression);
                    if(value is null) {
                        AddZero();
                        state.RegisterPendingExpression(line, expression, index, argumentType: isByte ? CpuInstrArgType.Byte : CpuInstrArgType.Word);
                        if(!isByte) index++;
                    }
                    else if(isByte && !value.IsValidByte) {
                        AddZero();
                        AddError(AssemblyErrorCode.InvalidExpression, $"Invalid expression for {opcode.ToUpper()}: {value:X4}h is not a valid byte value");
                    }
                    else if(value.IsAbsolute) {
                        outputBytes.Add(value.ValueAsByte);
                        if(!isByte) {
                            outputBytes.Add((byte)((value.Value & 0xFF00) >> 8));
                            index++;
                        }
                    }
                    else {
                        AddZero();
                        relocatables.Add(new RelocatableValue() { Index = index, IsByte = isByte, Type = value.Type, Value = value.Value });
                        if(!isByte) index++;
                    }
                }
                catch(ExpressionContainsExternalReferencesException) {
                    AddZero();
                    state.RegisterPendingExpression(line, expression, index, argumentType: isByte ? CpuInstrArgType.Byte : CpuInstrArgType.Word);
                    if(!isByte) index++;
                }
                catch(InvalidExpressionException ex) {
                    AddZero();
                    AddError(ex.ErrorCode, $"Invalid expression for {opcode.ToUpper()}: {ex.Message}");
                }
            }

            if(addZeroAtTheEnd) {
                outputBytes.AddRange(Expression.ZeroCharBytes);
            }

            state.IncreaseLocationPointer(outputBytes.Count);

            line.OutputBytes = outputBytes.ToArray();
            line.RelocatableParts = relocatables.ToArray();
            line.NewLocationArea = state.CurrentLocationArea;
            line.NewLocationCounter = state.CurrentLocationPointer;

            return line;
        }

        static ProcessedSourceLine ProcessDefsLine(string opcode, SourceLineWalker walker)
        {
            byte? value = null;
            ushort length = 0;
            var line = new DefineSpaceLine();

            if(walker.AtEndOfLine) {
                AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} needs at least the length argument");
            }
            else try {
                    var lengthExpressionText = walker.ExtractExpression();
                    var lengthExpression = state.GetExpressionFor(lengthExpressionText);
                    var lengthAddress = lengthExpression.Evaluate();
                    if(!lengthAddress.IsAbsolute) {
                        throw new InvalidExpressionException("the length argument must evaluate to an absolute value");
                    }
                    length = lengthAddress.Value;

                    if(!walker.AtEndOfLine) {
                        var valueExpressionText = walker.ExtractExpression();
                        var valueExpression = state.GetExpressionFor(valueExpressionText, isByte: true);
                        var valueAddress = EvaluateIfNoSymbolsOrPass2(valueExpression);
                        if(valueAddress is null) {
                            state.RegisterPendingExpression(line, valueExpression, argumentType: CpuInstrArgType.Byte);
                        }
                        else if(!valueAddress.IsValidByte) {
                            throw new InvalidExpressionException("the value argument must evaluate to a valid byte");
                        }
                        else {
                            value = valueAddress.ValueAsByte;
                        }
                    }
                }
                catch(InvalidExpressionException ex) {
                    AddError(ex.ErrorCode, $"Invalid expression for {opcode.ToUpper()}: {ex.Message}");
                    walker.DiscardRemaining();
                }

            state.IncreaseLocationPointer(length);
            line.NewLocationArea = state.CurrentLocationArea;
            line.NewLocationCounter = state.CurrentLocationPointer;
            line.Size = length;
            line.Value = value;

            return line;
        }

        static ProcessedSourceLine ProcessDcLine(string opcode, SourceLineWalker walker)
        {
            var line = new DefbLine();
            byte[] outputBytes = null;

            if(walker.AtEndOfLine) {
                AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} needs one string as argument");
            }
            else try {
                    var expressionText = walker.ExtractExpression();
                    var expression = state.GetExpressionFor(expressionText, forDefb: true);

                    if(expression.IsRawBytesOutput) {
                        var bytes = (RawBytesOutput)expression.Parts[0];
                        if(bytes.Any(b => b >= 0x80)) {
                            AddError(AssemblyErrorCode.StringHasBytesWithHighBitSet, $"{opcode.ToUpper()}: the string already has bytes with the MSB set once encoded with {Expression.OutputStringEncoding.WebName}");
                        }

                        bytes[bytes.Length - 1] |= 0x80;
                        outputBytes = bytes.ToArray();
                    }
                    else {
                        AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} needs one single string as argument");
                    }
                }
                catch(InvalidExpressionException ex) {
                    AddError(ex.ErrorCode, $"Invalid expression: {ex.Message}");
                    walker.DiscardRemaining();
                }

            if(outputBytes is not null) {
                state.IncreaseLocationPointer(outputBytes.Length);
            }

            line.OutputBytes = outputBytes ?? Array.Empty<byte>();
            line.RelocatableParts = Array.Empty<RelocatableOutputPart>();
            line.NewLocationArea = state.CurrentLocationArea;
            line.NewLocationCounter = state.CurrentLocationPointer;

            return line;
        }

        static ProcessedSourceLine ProcessCsegLine(string opcode, SourceLineWalker walker) => ProcessChangeAreaLine(AddressType.CSEG);

        static ProcessedSourceLine ProcessDsegLine(string opcode, SourceLineWalker walker) => ProcessChangeAreaLine(AddressType.DSEG);

        static ProcessedSourceLine ProcessAsegLine(string opcode, SourceLineWalker walker) => ProcessChangeAreaLine(AddressType.ASEG);

        static ProcessedSourceLine ProcessCommonLine(string opcode, SourceLineWalker walker)
        {
            if(walker.AtEndOfLine) {
                AddError(AssemblyErrorCode.InvalidArgument, $"{opcode.ToUpper()} requires a common block name enclosed between two / characters");
                return new ChangeAreaLine();
            }

            var commonBlockNameExpression = walker.ExtractExpression();
            if(!commonBlockNameRegex.IsMatch(commonBlockNameExpression)) {
                AddError(AssemblyErrorCode.InvalidArgument, $"{opcode.ToUpper()} requires a common block name having valid label characters and enclosed between two / characters");
                return new ChangeAreaLine();
            }

            var commonBlockName = commonBlockNameExpression.Trim('/', ' ', '\t').ToUpper();
            return ProcessChangeAreaLine(AddressType.COMMON, commonBlockName);
        }

        static ProcessedSourceLine ProcessChangeAreaLine(AddressType area, string commonName = null)
        {
            if(!(area is AddressType.COMMON ^ commonName is null)) {
                throw new InvalidOperationException($"{nameof(ProcessChangeAreaLine)}: {nameof(area)} is {area} and {nameof(commonName)} is {commonName}, that's illegal!");
            }

            if(buildType == BuildType.Absolute && area != AddressType.ASEG) {
                AddError(AssemblyErrorCode.IgnoredForAbsoluteOutput, $"Changing area to {area} when the output type is absolute has no effect");
            }

            if(state.IsCurrentlyPhased) {
                AddError(AssemblyErrorCode.InvalidInPhased, "Changing the location area is not allowed inside a .PHASE block");
                return new ChangeAreaLine();
            }

            state.SwitchToArea(area, commonName);

            return new ChangeAreaLine() {
                NewLocationArea = state.CurrentLocationArea,
                NewLocationCounter = state.CurrentLocationPointer,
                CommonBlockName = commonName,
            };
        }

        static ProcessedSourceLine ProcessOrgLine(string opcode, SourceLineWalker walker)
        {
            if(state.IsCurrentlyPhased) {
                AddError(AssemblyErrorCode.InvalidInPhased, "Changing the location pointer is not allowed inside a .PHASE block");
                return new ChangeOriginLine();
            }

            if(walker.AtEndOfLine) {
                AddError(AssemblyErrorCode.LineHasNoEffect, "ORG without value will have no effect");
                return new ChangeOriginLine();
            }

            try {
                var valueExpressionString = walker.ExtractExpression();
                var valueExpression = state.GetExpressionFor(valueExpressionString);
                var value = EvaluateIfNoSymbolsOrPass2(valueExpression);
                if(value is null) {
                    var line = new ChangeOriginLine();
                    state.RegisterPendingExpression(line, valueExpression, argumentType: CpuInstrArgType.Word);
                    return line;
                }
                else {
                    state.SwitchToLocation(value.Value);
                    return new ChangeOriginLine() { NewLocationArea = state.CurrentLocationArea, NewLocationCounter = value.Value };
                }
            }
            catch(InvalidExpressionException ex) {
                AddError(ex.ErrorCode, $"Invalid expression for {opcode.ToUpper()}: {ex.Message}");
                walker.DiscardRemaining();
                return new ChangeOriginLine();
            }
        }

        static ProcessedSourceLine ProcessExternalDeclarationLine(string opcode, SourceLineWalker walker)
        {
            if(walker.AtEndOfLine) {
                AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} must be followed by a symbol name");
                return new ExternalDeclarationLine();
            }

            var symbolNames = new List<string>();
            while(!walker.AtEndOfLine) {
                var symbolName = walker.ExtractExpression();
                if(string.IsNullOrWhiteSpace(symbolName)) {
                    AddError(AssemblyErrorCode.InvalidArgument, $"{opcode.ToUpper()}: the symbol name can't be empty");
                    continue;
                }
                if(!externalSymbolRegex.IsMatch(symbolName)) {
                    AddError(AssemblyErrorCode.InvalidLabel, $"{symbolName} is not a valid external symbol name, it contains invalid characters");
                }

                var existingSymbol = state.GetSymbol(ref symbolName);
                if(existingSymbol is null) {
                    state.AddSymbol(symbolName, type: SymbolType.External);
                    symbolNames.Add(symbolName);
                }
                else if(existingSymbol.IsPublic || (existingSymbol.IsOfKnownType && !existingSymbol.IsExternal)) {
                    AddError(AssemblyErrorCode.DuplicatedSymbol, $"{symbolName} is already defined, can't be declared as an external symbol");
                }
                else {
                    //In case the symbol first appeared as part of an expression
                    //and was therefore of type "Unknown"
                    existingSymbol.Type = SymbolType.External;
                    symbolNames.Add(symbolName);
                }
            }

            symbolNames = symbolNames.DistinctBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();

            if(buildType == BuildType.Automatic) {
                SetBuildType(BuildType.Relocatable);
            }
            else if(buildType == BuildType.Absolute) {
                if(symbolNames.Count == 1) {
                    AddError(AssemblyErrorCode.IgnoredForAbsoluteOutput, $"Symbol {symbolNames[0].ToUpper()} is declared as external, but that has no effect when the output type is absolute");
                }
                else {
                    var symbolsUpper = symbolNames.Select(s => s.ToUpper());
                    AddError(AssemblyErrorCode.IgnoredForAbsoluteOutput, $"Symbols {string.Join(", ", symbolsUpper)} are declared as external, but that has no effect when the output type is absolute");
                }
            }

            return new ExternalDeclarationLine() { SymbolNames = symbolNames.ToArray() };
        }

        static ProcessedSourceLine ProcessPublicDeclarationLine(string opcode, SourceLineWalker walker)
        {
            if(walker.AtEndOfLine) {
                AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} must be followed by a label name");
                return new PublicDeclarationLine();
            }

            var symbolNames = new List<string>();
            while(!walker.AtEndOfLine) {
                var symbolName = walker.ExtractExpression();
                if(string.IsNullOrWhiteSpace(symbolName)) {
                    AddError(AssemblyErrorCode.InvalidArgument, $"{opcode.ToUpper()}: the symbol name can't be empty");
                    continue;
                }
                if(!externalSymbolRegex.IsMatch(symbolName)) {
                    AddError(AssemblyErrorCode.InvalidLabel, $"{symbolName} is not a valid public symbol name, it contains invalid characters");
                    continue;
                }

                var existingSymbol = state.GetSymbol(ref symbolName);
                if(existingSymbol is null) {
                    state.AddSymbol(symbolName, SymbolType.Unknown, isPublic: true);
                    symbolNames.Add(symbolName);
                }
                else if(existingSymbol.IsExternal) {
                    AddError(AssemblyErrorCode.DuplicatedSymbol, $"{symbolName} is already defined as an external symbol, can't be defined as public");
                }
                else {
                    existingSymbol.IsPublic = true;
                    symbolNames.Add(symbolName);
                }
            }

            symbolNames = symbolNames.DistinctBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();

            if(buildType == BuildType.Automatic) {
                SetBuildType(BuildType.Relocatable);
            }
            else if(buildType == BuildType.Absolute) {
                if(symbolNames.Count == 1) {
                    AddError(AssemblyErrorCode.IgnoredForAbsoluteOutput, $"Symbol {symbolNames[0].ToUpper()} is declared as public, but that has no effect when the output type is absolute");
                }
                else {
                    var symbolsUpper = symbolNames.Select(s => s.ToUpper());
                    AddError(AssemblyErrorCode.IgnoredForAbsoluteOutput, $"Symbols {string.Join(", ", symbolsUpper)} are declared as public, but that has no effect when the output type is absolute");
                }
            }

            return new PublicDeclarationLine() { SymbolNames = symbolNames.ToArray() };
        }

        static ProcessedSourceLine ProcessEndLine(string opcode, SourceLineWalker walker)
        {
            if(walker.AtEndOfLine) {
                state.RegisterEndInstruction(Address.AbsoluteZero);
                return new AssemblyEndLine() { Line = walker.SourceLine };
            }

            if(buildType == BuildType.Absolute) {
                AddError(AssemblyErrorCode.IgnoredForAbsoluteOutput, "The argument of the END statement is ignored when the output type is absolute");
                state.RegisterEndInstruction(Address.AbsoluteZero);
                return new AssemblyEndLine() { Line = walker.SourceLine, EffectiveLineLength = walker.DiscardRemaining() };
            }

            try {
                var endAddressText = walker.ExtractExpression();
                var endAddressExpression = state.GetExpressionFor(endAddressText);

                //Note that we are ending source code parsing here,
                //therefore, the end address expression must be evaluable at this point!
                var endAddress = endAddressExpression.Evaluate();

                state.RegisterEndInstruction(endAddress);
                return new AssemblyEndLine() { EndAddress = endAddress.Value, EndAddressArea = endAddress.Type };
            }
            catch(InvalidExpressionException ex) {
                AddError(ex.ErrorCode, $"Invalid expression for {opcode.ToUpper()}: {ex.Message}");
                walker.DiscardRemaining();
                state.RegisterEndInstruction(Address.AbsoluteZero);
                return new AssemblyEndLine();
            }
        }

        static ProcessedSourceLine ProcessDelimitedCommentStartLine(string opcode, SourceLineWalker walker)
        {
            if(walker.AtEndOfLine) {
                AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} needs one comment delimiter character");
                return new DelimitedCommentLine() { Delimiter = '\0', IsLastLine = true };
            }

            var delimiter = walker.ExtractSymbol()[0];
            state.MultiLineCommandDelimiter = delimiter;
            return new DelimitedCommentLine() { Delimiter = delimiter };
        }

        static ProcessedSourceLine ProcessConstantDefinition(string opcode, string name, SourceLineWalker walker = null, Expression expression = null)
        {
            name = state.Modularize(name);
            var isRedefinition = !opcode.Equals("EQU", StringComparison.OrdinalIgnoreCase);
            var line = new ConstantDefinitionLine() { Name = name, IsRedefinible = isRedefinition };

            if(walker is not null && walker.AtEndOfLine) {
                AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} must be followed by a value");
                return line;
            }

            Address value;

            try {
                if(expression is null) {
                    var valueExpressionString = walker.ExtractExpression();
                    var valueExpression = state.GetExpressionFor(valueExpressionString);
                    value = state.InPass1 ? valueExpression.TryEvaluate() : valueExpression.Evaluate();
                    if(value is null) {
                        state.RegisterPendingExpression(line, valueExpression);
                        return line;
                    }
                }
                else {
                    value = expression.Evaluate();
                }
            }
            catch(InvalidExpressionException ex) {
                AddError(ex.ErrorCode, $"Invalid expression for {opcode.ToUpper()}: {ex.Message}");
                walker.DiscardRemaining();
                return line;
            }

            line.ValueArea = value.Type;
            line.Value = value.Value;

            var symbol = state.GetSymbol(ref name);

            if(symbol is null) {
                if(z80RegisterNames.Contains(name, StringComparer.OrdinalIgnoreCase)) {
                    AddError(AssemblyErrorCode.SymbolWithCpuRegisterName, $"{name.ToUpper()} is a {currentCpu} register or flag name, so it won't be possible to use it as a symbol in {currentCpu} instructions");
                }
                state.AddSymbol(name, isRedefinition ? SymbolType.Defl : SymbolType.Equ, value);
            }
            else if(!symbol.IsOfKnownType) {
                symbol.Type = isRedefinition ? SymbolType.Defl : SymbolType.Equ;
                symbol.Value = value;
            }
            else if(isRedefinition && symbol.IsRedefinible) {
                symbol.Value = value;
            }
            else if(value != symbol.Value) {
                AddError(AssemblyErrorCode.DuplicatedSymbol, $"Symbol '{name.ToUpper()}' already exists (defined as {symbol.Type.ToString().ToLower()}) and can't be redefined with {opcode.ToUpper()}");
            }

            return line;
        }

        static ProcessedSourceLine ProcessSetEncodingLine(string opcode, SourceLineWalker walker)
        {
            if(walker.AtEndOfLine) {
                AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} needs a encoding name or code page number");
                return new ChangeStringEncodingLine() { IsSuccessful = false };
            }

            var encodingNameOrCodePage = walker.ExtractSymbol();
            if(string.Equals("default", encodingNameOrCodePage, StringComparison.OrdinalIgnoreCase) ||
               string.Equals("def", encodingNameOrCodePage, StringComparison.OrdinalIgnoreCase)) {
                Expression.OutputStringEncoding = state.DefaultOutputStringEncoding;
                return new ChangeStringEncodingLine() { IsSuccessful = true, EncodingNameOrCodePage = encodingNameOrCodePage, IsDefault = true };
            }

            var successful = SetStringEncoding(encodingNameOrCodePage);
            return new ChangeStringEncodingLine() { IsSuccessful = successful, EncodingNameOrCodePage = encodingNameOrCodePage };
        }

        static ProcessedSourceLine ProcessChangeStringEscapingLine(string opcode, SourceLineWalker walker)
        {
            string argument = null;
            bool? enable = null;

            if(!walker.AtEndOfLine) {
                argument = walker.ExtractSymbol();
                if(string.Equals(argument, "ON", StringComparison.OrdinalIgnoreCase))
                    enable = true;
                else if(string.Equals(argument, "OFF", StringComparison.OrdinalIgnoreCase))
                    enable = false;
            }

            if(enable is null) {
                AddError(AssemblyErrorCode.InvalidArgument, $"{opcode.ToUpper()} requires an argument that must be either ON or OFF");
            }
            else {
                Expression.AllowEscapesInStrings = enable.Value;
            }

            return new ChangeStringEscapingLine() { Argument = argument, IsOn = enable.GetValueOrDefault() };
        }

        static ProcessedSourceLine ProcessChangeRadixLine(string opcode, SourceLineWalker walker)
        {
            if(walker.AtEndOfLine) {
                AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} needs the new radix argument (a number between 2 and 16)");
                return new ChangeRadixLine();
            }

            var backupRadix = Expression.DefaultRadix;

            try {
                Expression.DefaultRadix = 10;
                var valueExpressionString = walker.ExtractExpression();
                var valueExpression = state.GetExpressionFor(valueExpressionString);
                var value = valueExpression.Evaluate();
                if(!value.IsAbsolute || value.Value < 2 || value.Value > 16) {
                    Expression.DefaultRadix = backupRadix;
                    AddError(AssemblyErrorCode.InvalidArgument, $"{opcode.ToUpper()}: argument must be an absolute value between 2 and 16)");
                    return new ChangeRadixLine();
                }

                Expression.DefaultRadix = value.Value;
                return new ChangeRadixLine() { NewRadix = value.Value };
            }
            catch(InvalidExpressionException ex) {
                Expression.DefaultRadix = backupRadix;
                AddError(ex.ErrorCode, $"Invalid expression for {opcode.ToUpper()}: {ex.Message}");
                walker.DiscardRemaining();
                return new ChangeRadixLine();
            }
        }

        static ProcessedSourceLine ProcessChangeCpuTo8080Line(string opcode, SourceLineWalker walker)
        {
            ThrowFatal(AssemblyErrorCode.UnsupportedCpu, "Unsupported CPU type: 8080");
            return null;
        }

        static ProcessedSourceLine ProcessChangeCpuToZ80Line(string opcode, SourceLineWalker walker)
        {
            currentCpu = CpuType.Z80;
            return new ChangeCpuLine() { Cpu = CpuType.Z80 };
        }

        static ProcessedSourceLine ProcessChangeCpuLine(string opcode, SourceLineWalker walker)
        {
            if(walker.AtEndOfLine) {
                AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} requires a CPU type as argument");
                return new ChangeCpuLine();
            }

            var symbol = walker.ExtractSymbol();
            var cpuType = SetCurrentCpu(symbol);
            return new ChangeCpuLine() { Cpu = cpuType };
        }

        private static CpuType SetCurrentCpu(string cpuName)
        {
            var cpuType = CpuType.Unknown;
            try {
                cpuType = (CpuType)Enum.Parse(typeof(CpuType), cpuName, ignoreCase: true);
            }
            catch(ArgumentException) {
                //Invalid CPU type
            }

            if(cpuType == CpuType.Unknown) {
                ThrowFatal(AssemblyErrorCode.UnsupportedCpu, $"Unknown CPU type: {cpuName}");
            }

            currentCpu = cpuType;

            return cpuType;
        }

        static ProcessedSourceLine ProcessSetProgramNameLine(string opcode, SourceLineWalker walker)
        {
            if(walker.AtEndOfLine) {
                AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} requires a program name as argument");
                return new ProgramNameLine();
            }

            var programName = walker.GetRemaining();
            return ProcessSetProgramName(opcode, walker, programName);
        }

        static ProcessedSourceLine ProcessSetProgramName(string opcode, SourceLineWalker walker, string rawName)
        {
            var effectiveLineLength = walker.SourceLine.IndexOf(';');
            if(effectiveLineLength < 0) {
                effectiveLineLength = walker.SourceLine.Length;
            }

            var match = ProgramNameRegex.Match(rawName);
            if(!match.Success) {
                AddError(AssemblyErrorCode.InvalidArgument, $"{opcode.ToUpper()}: the program name must be in the format ('NAME')");
                return new ProgramNameLine() { EffectiveLineLength = effectiveLineLength };
            }

            var name = match.Groups["name"].Value;

            programName = (name.Length > MaxEffectiveExternalNameLength ? name[..MaxEffectiveExternalNameLength] : name).ToUpper();

            return new ProgramNameLine() { Name = rawName, EffectiveLineLength = effectiveLineLength };
        }

        static ProcessedSourceLine ProcessSetListingTitleLine(string opcode, SourceLineWalker walker)
        {
            var title = walker.GetRemaining();

            if(programName is null) {
                var firstWord = title.Split(' ', '\t')[0];
                programName = firstWord.Length > MaxEffectiveExternalNameLength ? firstWord[..MaxEffectiveExternalNameLength] : firstWord;
            }

            return new SetListingTitleLine() { Title = title, EffectiveLineLength = walker.SourceLine.Length };
        }

        static ProcessedSourceLine ProcessSetListingSubtitleLine(string opcode, SourceLineWalker walker)
        {
            var subtitle = walker.GetRemaining();
            return new SetListingSubtitleLine() { Subtitle = subtitle, EffectiveLineLength = walker.SourceLine.Length };
        }

        static ProcessedSourceLine ProcessLegacySetListingSubtitleLine(string opcode, SourceLineWalker walker)
        {
            if(walker.AtEndOfLine) {
                AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} requires a listing subtitle as argument");
                return new SetListingSubtitleLine();
            }

            var subtitle = walker.GetRemaining();
            return ProcessLegacySetListingSubtitle(opcode, walker, subtitle);
        }

        static ProcessedSourceLine ProcessLegacySetListingSubtitle(string opcode, SourceLineWalker walker, string subtitle)
        {
            var effectiveLineLength = walker.SourceLine.IndexOf(';');
            if(effectiveLineLength < 0) {
                effectiveLineLength = walker.SourceLine.Length;
            }

            var match = LegacySubtitleRegex.Match(subtitle);
            if(!match.Success) {
                AddError(AssemblyErrorCode.InvalidArgument, $"{opcode.ToUpper()}: the subtitle must be in the format ('text')");
                return new SetListingSubtitleLine() { EffectiveLineLength = effectiveLineLength };
            }

            var effectiveSubtitle = match.Groups["name"].Value;
            return new SetListingSubtitleLine() { Subtitle = effectiveSubtitle, EffectiveLineLength = effectiveLineLength };
        }

        static ProcessedSourceLine ProcessSetListingNewPageLine(string opcode, SourceLineWalker walker)
        {
            if(walker.AtEndOfLine) {
                return new ChangeListingPageLine() { NewPageSize = 0 };
            }

            try {
                var pageSizeText = walker.ExtractExpression();
                var pageSizeExpression = state.GetExpressionFor(pageSizeText);
                var pageSize = pageSizeExpression.Evaluate();
                if(!pageSize.IsAbsolute) {
                    AddError(AssemblyErrorCode.InvalidArgument, $"{opcode.ToUpper()}: the page size must be an absolute value");
                }
                if(pageSize.Value < 10) {
                    AddError(AssemblyErrorCode.InvalidListingPageSize, $"{opcode.ToUpper()}: the minimum listing page size is 10");
                }
                return new ChangeListingPageLine() { NewPageSize = pageSize.Value };
            }
            catch(InvalidExpressionException ex) {
                AddError(ex.ErrorCode, $"Invalid expression for {opcode.ToUpper()}: {ex.Message}");
                walker.DiscardRemaining();
                return new ChangeListingPageLine();
            }
        }

        static ProcessedSourceLine ProcessChangeListingMainPageLine(string opcode, SourceLineWalker walker)
        {
            return new ChangeListingPageLine() { IsMainPageChange = true };
        }

        static ProcessedSourceLine ProcessPrintxLine(string opcode, SourceLineWalker walker)
        {
            if(walker.AtEndOfLine) {
                AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} requires the text to print as argument");
                return new PrintLine();
            }

            var text = walker.GetRemaining();
            var delimiter = text[0];
            var endDelimiterPosition = text.IndexOf(delimiter, 1);
            if(endDelimiterPosition == -1) {
                return new PrintLine() { PrintedText = text, EffectiveLineLength = walker.SourceLine.Length };
            }

            var effectiveText = text[..(endDelimiterPosition + 1)];
            return new PrintLine() { PrintedText = effectiveText, EffectiveLineLength = walker.SourceLine.Length - (text.Length - endDelimiterPosition) + 1 };
        }

        static void TriggerPrintEvent(PrintLine line)
        {
            if(PrintMessage is not null && line.PrintedText is not null && ((state.InPass1 && line.PrintInPass1) || (state.InPass2 && line.PrintInPass2)))
                PrintMessage(null, line.PrintedText);
        }

        static ProcessedSourceLine ProcessRequestLinkFilesLine(string opcode, SourceLineWalker walker)
        {
            if(walker.AtEndOfLine) {
                AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} must be followed by at least one file name");
                return new LinkerFileReadRequestLine();
            }

            if(buildType == BuildType.Absolute) {
                AddError(AssemblyErrorCode.IgnoredForAbsoluteOutput, $"{opcode.ToUpper()} is ignored when the output type is absolute");
            }

            var filenames = new List<string>();

            while(!walker.AtEndOfLine) {
                var filename = walker.ExtractExpression();
                if(string.IsNullOrWhiteSpace(filename)) {
                    AddError(AssemblyErrorCode.InvalidArgument, $"{opcode.ToUpper()}: the file name can't be empty");
                }
                else if(!externalSymbolRegex.IsMatch(filename)) {
                    AddError(AssemblyErrorCode.InvalidArgument, $"{opcode.ToUpper()}: {filename} is not a valid linker request symbol name, it contains invalid characters");
                }
                else if(filename.Length > MaxEffectiveExternalNameLength) {
                    var truncated = filename[..MaxEffectiveExternalNameLength].ToUpper();
                    AddError(AssemblyErrorCode.TruncatedRequestFilename, $"{opcode.ToUpper()}: {filename} is too long, it will be truncated to {truncated}");
                    filenames.Add(truncated);
                }
                else {
                    filenames.Add(filename);
                }
            }

            return new LinkerFileReadRequestLine() { Filenames = filenames.ToArray() };
        }

        static ProcessedSourceLine ProcessListingControlLine(string opcode, SourceLineWalker walker)
        {
            var type = (ListingControlInstructionType)Enum.Parse(typeof(ListingControlInstructionType), opcode[1..], ignoreCase: true);
            return new ListingControlLine() { Type = type };
        }

        static ProcessedSourceLine ProcessPrintLine(string opcode, SourceLineWalker walker)
            => ProcessPrintOrUserErrorLine(opcode, walker, null);

        static ProcessedSourceLine ProcessPrint1Line(string opcode, SourceLineWalker walker)
            => ProcessPrintOrUserErrorLine(opcode, walker, 1);

        static ProcessedSourceLine ProcessPrint2Line(string opcode, SourceLineWalker walker)
            => ProcessPrintOrUserErrorLine(opcode, walker, 2);

        static ProcessedSourceLine ProcessUserWarningLine(string opcode, SourceLineWalker walker)
            => ProcessPrintOrUserErrorLine(opcode, walker, errorSeverity: AssemblyErrorSeverity.Warning);

        static ProcessedSourceLine ProcessUserErrorLine(string opcode, SourceLineWalker walker)
            => ProcessPrintOrUserErrorLine(opcode, walker, errorSeverity: AssemblyErrorSeverity.Error);

        static ProcessedSourceLine ProcessUserFatalLine(string opcode, SourceLineWalker walker)
            => ProcessPrintOrUserErrorLine(opcode, walker, errorSeverity: AssemblyErrorSeverity.Fatal);

        /// <summary>
        /// Handler for .PRINT(1/2), .WARN, .ERR and .FATAL instructions.
        /// These admit interpolating expressions with the following format:
        /// 
        /// {expression[:radix[size]]}
        /// 
        /// where radix is D,d (decimal), H,h,X,x (hexadecimal) or B,b (Binary)
        /// and size is the min number of digits to print (zeros are added to the left as needed), e.g.:
        /// 
        /// .PRINT2 Value of FOO is: {FOO:H4}h
        /// 
        /// </summary>
        /// <param name="opcode"></param>
        /// <param name="walker"></param>
        /// <param name="printInPass"></param>
        /// <param name="errorSeverity"></param>
        /// <returns></returns>
        static ProcessedSourceLine ProcessPrintOrUserErrorLine(string opcode, SourceLineWalker walker, int? printInPass = null, AssemblyErrorSeverity errorSeverity = AssemblyErrorSeverity.None)
        {
            var rawText = walker.GetRemaining();
            var sb = new StringBuilder();
            var lastIndex = 0;
            var expressionMatches = printStringExpressionRegex.Matches(rawText);
            if(expressionMatches.Count == 0 || (printInPass.HasValue && state.CurrentPass != printInPass )) {
                return GetLineForPrintOrUserError(rawText, walker, printInPass, errorSeverity);
            }

            Match match = null;
            foreach(var m in expressionMatches) {
                Address expressionValue = null;
                string partToPrint = null;
                string formatSpecifier = null;

                match = (Match)m;
                var expressionText = match.Value;
                var originalExpressionText = expressionText;
                var colonIndex = expressionText.LastIndexOf(':');
                if(colonIndex != -1 && colonIndex != expressionText.Length - 1 && colonIndex != 0) {
                    formatSpecifier = expressionText[(colonIndex + 1)..].Replace('h', 'x').Replace('H', 'X');
                    expressionText = expressionText[..colonIndex];
                }

                try {
                    var expression = state.GetExpressionFor(expressionText);
                    expressionValue = expression.Evaluate();
                }
                catch(InvalidExpressionException ex) {
                    AddError(ex.ErrorCode, $"Invalid expression for {opcode.ToUpper()}: {ex.Message}");
                    partToPrint = $"{{{originalExpressionText}}}";
                }

                if(expressionValue is null) {
                    AddError(AssemblyErrorCode.InvalidExpression, $"Can't evaluate expression for {opcode.ToUpper()}, does it reference undefined or external symbols?");
                    partToPrint = $"{{{originalExpressionText}}}";
                }
                else {
                    if(formatSpecifier is null) {
                        partToPrint = expressionValue.Value.ToString();
                    }
                    else if(formatSpecifier[0] is 'b' or 'B') {
                        if(formatSpecifier.Length == 1) {
                            partToPrint = Convert.ToString(expressionValue.Value, 2);
                        }
                        else if(int.TryParse(formatSpecifier[1..], out int length) && length >= 0) {
                            partToPrint = Convert.ToString(expressionValue.Value, 2).PadLeft(length, '0');
                        }
                    }
                    else if(formatSpecifier[0] is 'd' or 'D' or 'x' or 'X') {
                        try {
                            partToPrint = string.Format($"{{0:{formatSpecifier}}}", expressionValue.Value);
                        }
                        catch {
                            // partToPrint will remain null and that will be handled later
                        }
                    }
                }

                if(partToPrint is null) {
                    AddError(AssemblyErrorCode.InvalidArgument, $"Invalid format specifier for expression in {opcode.ToUpper()}: {formatSpecifier}");
                    partToPrint = $"{{{originalExpressionText}}}";
                }

                sb.Append(rawText.AsSpan(lastIndex, match.Index - lastIndex - 1));
                sb.Append(partToPrint);
                lastIndex = match.Index + match.Length + 1;
            }

            sb.Append(rawText.AsSpan(match.Index + match.Length + 1));

            var finalText = sb.ToString();
            return GetLineForPrintOrUserError(finalText, walker, printInPass, errorSeverity);
        }

        static ProcessedSourceLine GetLineForPrintOrUserError(string text, SourceLineWalker walker, int? pass, AssemblyErrorSeverity severity)
        {
            if(severity is AssemblyErrorSeverity.None) {
                return new PrintLine() { PrintedText = text, EffectiveLineLength = walker.SourceLine.Length, PrintInPass = pass };
            }
            else if(severity is AssemblyErrorSeverity.Fatal) {
                ThrowFatal(AssemblyErrorCode.UserFatal, text);
                return null;
            }
            else {
                var errorCode = severity is AssemblyErrorSeverity.Warning ? AssemblyErrorCode.UserWarning : AssemblyErrorCode.UserError;
                AddError(errorCode, text);
                return new UserErrorLine() { Severity = severity, Message = text };
            }
        }

        static ProcessedSourceLine ProcessIfTrueLine(string opcode, SourceLineWalker walker)
            => ProcessIfExpressionLine(opcode, walker, true);

        static ProcessedSourceLine ProcessIfFalseLine(string opcode, SourceLineWalker walker)
            => ProcessIfExpressionLine(opcode, walker, false);

        static ProcessedSourceLine ProcessIfExpressionLine(string opcode, SourceLineWalker walker, bool mustEvaluateToTrue)
        {
            bool? evaluator()
            {
                if(walker.AtEndOfLine) {
                    AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} requires an argument");
                    return null;
                }

                var expressionText = walker.ExtractExpression();
                Address expressionValue;
                try {
                    var expression = state.GetExpressionFor(expressionText);
                    expressionValue = expression.Evaluate();
                }
                catch(InvalidExpressionException ex) {
                    AddError(ex.ErrorCode, $"Invalid expression for {opcode.ToUpper()}: {ex.Message}");
                    walker.DiscardRemaining();
                    return null;
                }

                return mustEvaluateToTrue ? expressionValue.Value != 0 : expressionValue.Value == 0;
            };

            return ProcessIfLine(opcode, evaluator);
        }

        static ProcessedSourceLine ProcessIfDefinedLine(string opcode, SourceLineWalker walker)
        {
            bool? evaluator()
            {
                if(walker.AtEndOfLine) {
                    AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} requires an argument");
                    return null;
                }

                var symbol = walker.ExtractSymbol();
                return state.SymbolIsOfKnownType(symbol);
            }

            return ProcessIfLine(opcode, evaluator);
        }

        static ProcessedSourceLine ProcessIfNotDefinedLine(string opcode, SourceLineWalker walker)
        {
            bool? evaluator()
            {
                if(walker.AtEndOfLine) {
                    AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} requires an argument");
                    return null;
                }

                var symbol = walker.ExtractSymbol();
                return !state.SymbolIsOfKnownType(symbol);
            }

            return ProcessIfLine(opcode, evaluator);
        }

        static ProcessedSourceLine ProcessIf1Line(string opcode, SourceLineWalker walker)
        {
            static bool? evaluator()
            {
                return state.InPass1;
            }

            return ProcessIfLine(opcode, evaluator);
        }

        static ProcessedSourceLine ProcessIf2Line(string opcode, SourceLineWalker walker)
        {
            static bool? evaluator()
            {
                return state.InPass2;
            }

            return ProcessIfLine(opcode, evaluator);
        }

        static ProcessedSourceLine ProcessIfBlankLine(string opcode, SourceLineWalker walker)
        {
            bool? evaluator()
            {
                var text = walker.ExtractAngleBracketed();
                if(text is null) {
                    AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} requires an argument enclosed in < and >");
                    walker.DiscardRemaining();
                    return null;
                }

                return text == "";
            }

            return ProcessIfLine(opcode, evaluator);
        }

        static ProcessedSourceLine ProcessIfNotBlankLine(string opcode, SourceLineWalker walker)
        {
            bool? evaluator()
            {
                var text = walker.ExtractAngleBracketed();
                if(text is null) {
                    AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} requires an argument enclosed in < and >");
                    walker.DiscardRemaining();
                    return null;
                }

                return text != "";
            }

            return ProcessIfLine(opcode, evaluator);
        }

        static ProcessedSourceLine ProcessIfIdenticalLineCaseSensitive(string opcode, SourceLineWalker walker) =>
            ProcessIfIdenticalLine(opcode, walker, true);

        static ProcessedSourceLine ProcessIfIdenticalLineCaseInsensitive(string opcode, SourceLineWalker walker) =>
            ProcessIfIdenticalLine(opcode, walker, false);

        static ProcessedSourceLine ProcessIfIdenticalLine(string opcode, SourceLineWalker walker, bool caseSensitive)
        {
            bool? evaluator()
            {
                string text1 = null, text2 = null;
                text1 = walker.ExtractAngleBracketed();
                if(text1 is not null && walker.SkipComma()) {
                    text2 = walker.ExtractAngleBracketed();
                }

                if(text1 is null || text2 is null) {
                    AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} requires two arguments, each enclosed in < and >");
                    walker.DiscardRemaining();
                    return null;
                }

                return caseSensitive ? text1 == text2 : text1.Equals(text2, StringComparison.OrdinalIgnoreCase);
            }

            return ProcessIfLine(opcode, evaluator);
        }

        static ProcessedSourceLine ProcessIfDifferentLineCaseSensitive(string opcode, SourceLineWalker walker) =>
            ProcessIfDifferentLine(opcode, walker, true);

        static ProcessedSourceLine ProcessIfDifferentLineCaseInsensitive(string opcode, SourceLineWalker walker) =>
            ProcessIfDifferentLine(opcode, walker, false);

        static ProcessedSourceLine ProcessIfDifferentLine(string opcode, SourceLineWalker walker, bool caseSensitive)
        {
            bool? evaluator()
            {
                string text1 = null, text2 = null;
                text1 = walker.ExtractAngleBracketed();
                if(text1 is not null && walker.SkipComma()) {
                    text2 = walker.ExtractAngleBracketed();
                }

                if(text1 is null || text2 is null) {
                    AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} requires two arguments, each enclosed in < and >");
                    walker.DiscardRemaining();
                    return null;
                }

                return caseSensitive ? text1 != text2 : !text1.Equals(text2, StringComparison.OrdinalIgnoreCase);
            }

            return ProcessIfLine(opcode, evaluator);
        }

        static ProcessedSourceLine ProcessIfAbsLine(string opcode, SourceLineWalker walker)
        {
            static bool? evaluator()
            {
                return buildType == BuildType.Absolute;
            }

            return ProcessIfLine(opcode, evaluator);
        }

        static ProcessedSourceLine ProcessIfRelLine(string opcode, SourceLineWalker walker)
        {
            static bool? evaluator()
            {
                return buildType == BuildType.Relocatable;
            }

            return ProcessIfLine(opcode, evaluator);
        }

        static ProcessedSourceLine ProcessIfCpuLine(string opcode, SourceLineWalker walker)
        {
            bool? evaluator()
            {
                var text = walker.ExtractSymbol();
                if(text is null) {
                    AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} requires a CPU name as argument");
                    return null;
                }

                return string.Equals(currentCpu.ToString(), text, StringComparison.OrdinalIgnoreCase);
            }

            return ProcessIfLine(opcode, evaluator);
        }

        static ProcessedSourceLine ProcessIfNotCpuLine(string opcode, SourceLineWalker walker)
        {
            bool? evaluator()
            {
                var text = walker.ExtractSymbol();
                if(text is null) {
                    AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} requires a CPU name as argument");
                    return null;
                }

                return !string.Equals(currentCpu.ToString(), text, StringComparison.OrdinalIgnoreCase);
            }

            return ProcessIfLine(opcode, evaluator);
        }

        static ProcessedSourceLine ProcessIfLine(string opcode, Func<bool?> evaluator)
        {
            // From inside a false conditional we can't evaluate an expression that isn't intended
            // to be evaluated at that point as it could lead to weird errors. Example:
            // ifdef FOO
            // if FOO eq 34 --> This would throw an error if FOO is not defined
            var mustEvaluateTo = state.InFalseConditional ? false : evaluator();
            if(mustEvaluateTo is not null) {
                state.PushAndSetConditionalBlock(mustEvaluateTo.Value ? ConditionalBlockType.TrueIf : ConditionalBlockType.FalseIf);
            }

            return new IfLine() { EvaluatesToTrue = mustEvaluateTo };
        }

        static ProcessedSourceLine ProcessElseLine(string opcode, SourceLineWalker walker)
        {
            bool enteringTrueBlock = false;
            if(state.InElseBlock) {
                AddError(AssemblyErrorCode.ConditionalOutOfScope, $"{opcode.ToUpper()} found inside an ELSE block");
            }
            else if(!state.InMainConditionalBlock) {
                AddError(AssemblyErrorCode.ConditionalOutOfScope, $"{opcode.ToUpper()} found outside an IF block");
            }
            else {
                enteringTrueBlock = !state.InTrueConditional;
                state.SetConditionalBlock(enteringTrueBlock ? ConditionalBlockType.TrueElse : ConditionalBlockType.FalseElse);
            }

            return new ElseLine() { EvaluatesToTrue = enteringTrueBlock };
        }

        static ProcessedSourceLine ProcessEndifLine(string opcode, SourceLineWalker walker)
        {
            if(state.InConditionalBlock) {
                state.PopConditionalBlock();
            }
            else {
                AddError(AssemblyErrorCode.ConditionalOutOfScope, $"{opcode.ToUpper()} found outside a conditional (IF or ELSE) block");
            }

            return EndifLine.Instance;
        }

        static (Stream, ProcessedSourceLine) ProcessIncludeLine(string opcode, SourceLineWalker walker)
        {
            if(walker.AtEndOfLine) {
                AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} requires a file path as argument");
                return (null, new IncludeLine());
            }

            var path = walker.ExtractFileName();

            Stream stream = null;
            Exception exception = null;
            if(state.Configuration.GetStreamForInclude is not null) {
                try {
                    stream = state.Configuration.GetStreamForInclude(path);
                }
                catch(Exception ex) {
                    exception = ex;
                }
            }

            if(exception is not null) {
                ThrowFatal(AssemblyErrorCode.CantInclude, $"Error trying to include file {path}: ({exception.GetType().Name}) {exception.Message}");
            }

            if(stream is null) {
                ThrowFatal(AssemblyErrorCode.CantInclude, $"Can't include file {path}: File not found");
            }

            string fileName = path;
            try {
                fileName = Path.GetFileName(fileName);
            }
            catch(ArgumentException) {
            }

            return (stream, new IncludeLine() { FileName = fileName, FullPath = path });
        }

        /**
         * Additional restrictions compared to Macro80:
         * 
         * - Address must be known when the instruction is reached
         * - Address must be absolute
         * - Area changes and ORGs are not allowed inside a .PHASE block
         */
        static ProcessedSourceLine ProcessPhaseLine(string opcode, SourceLineWalker walker)
        {
            if(state.IsCurrentlyPhased) {
                AddError(AssemblyErrorCode.InvalidNestedPhase, $"Nested {opcode.ToUpper()} instructions are not allowed");
                walker.DiscardRemaining();
                return new PhaseLine();
            }

            Address phaseAddress;
            if(walker.AtEndOfLine) {
                AddError(AssemblyErrorCode.PhaseWithoutArgument, $"{opcode.ToUpper()} instruction without argument, address 0 will be used");
                phaseAddress = Address.AbsoluteZero;
            }
            else {
                try {
                    var phaseAddressText = walker.ExtractExpression();
                    var phaseAddressExpression = state.GetExpressionFor(phaseAddressText);
                    phaseAddress = phaseAddressExpression.Evaluate();
                }
                catch(InvalidExpressionException ex) {
                    AddError(ex.ErrorCode, $"Invalid expression for {opcode.ToUpper()}: {ex.Message}");
                    walker.DiscardRemaining();
                    return new PhaseLine();
                }
            }

            state.EnterPhase(phaseAddress);
            return new PhaseLine() { NewLocationArea = state.CurrentLocationArea, NewLocationCounter = state.CurrentLocationPointer };
        }

        static ProcessedSourceLine ProcessDephaseLine(string opcode, SourceLineWalker walker)
        {
            if(state.IsCurrentlyPhased) {
                state.ExitPhase();
            }
            else {
                walker.DiscardRemaining();
                AddError(AssemblyErrorCode.DephaseWithoutPhase, $"{opcode.ToUpper()} found without a corresponding .PHASE");
            }

            return new DephaseLine() { NewLocationArea = state.CurrentLocationArea, NewLocationCounter = state.CurrentLocationPointer };
        }

        static ProcessedSourceLine ProcessEndoutLine(string opcode, SourceLineWalker walker)
        {
            return new EndOutputLine();
        }

        static ProcessedSourceLine ProcessModuleLine(string opcode, SourceLineWalker walker)
        {
            string name = null;
            if(walker.AtEndOfLine) {
                AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} needs a module name as argument");
            }
            else {
                name = walker.ExtractSymbol();
                if(!IsValidSymbolName(name)) {
                    AddError(AssemblyErrorCode.InvalidLabel, $"'{name}' is not a valid module name");
                    name = null;
                }
            }

            if(name is not null) {
                state.EnterModule(name);
            }
            return new ModuleStartLine() { Name = name };
        }

        static ProcessedSourceLine ProcessEndModuleLine(string opcode, SourceLineWalker walker)
        {
            if(state.CurrentModule is null) {
                AddError(AssemblyErrorCode.EndModuleOutOfScope, $"{opcode.ToUpper()} found while not in a module");
            }
            else {
                state.ExitModule();
            }

            return new ModuleEndLine();
        }

        static ProcessedSourceLine ProcessRootLine(string opcode, SourceLineWalker walker)
        {
            if(state.CurrentModule is null) {
                AddError(AssemblyErrorCode.RootWithoutModule, $"{opcode.ToUpper()} is ignored while not in a module");
            }
            else if(walker.AtEndOfLine) {
                AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} requires one or more symbol names as argument");
                return new RootLine();
            }

            var symbols = new List<string>();
            while(!walker.AtEndOfLine) {
                var symbol = walker.ExtractExpression();
                if(!IsValidSymbolName(symbol)) {
                    AddError(AssemblyErrorCode.InvalidLabel, $"'{symbol}' is not a valid symbol name");
                }
                else if(symbol != "") {
                    symbols.Add(symbol);
                }
            }

            if(state.CurrentModule is not null) {
                state.RegisterRootSymbols(symbols);
            }

            return new RootLine() { RootSymbols = symbols.ToArray() };
        }

        static ProcessedSourceLine ProcessReptLine(string opcode, SourceLineWalker walker)
        {
            if(walker.AtEndOfLine) {
                AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} requires a repetitions count as argument");
                return new MacroExpansionLine();
            }

            Address repetitionsCount;
            try {
                var repetitionsExpressionString = walker.ExtractExpression();
                var repetitionsExpression = state.GetExpressionFor(repetitionsExpressionString);
                repetitionsCount = repetitionsExpression.Evaluate();
            }
            catch(InvalidExpressionException ex) {
                AddError(ex.ErrorCode, $"Invalid expression for {opcode.ToUpper()}: {ex.Message}");
                walker.DiscardRemaining();
                return new MacroExpansionLine();
            }

            if(!repetitionsCount.IsAbsolute) {
                AddError(AssemblyErrorCode.InvalidExpression, $"{opcode.ToUpper()}: the repetitions count can't be a relocatable value");
                walker.DiscardRemaining();
                return new MacroExpansionLine();
            }

            var line = new MacroExpansionLine() { MacroType = MacroType.ReptWithCount, Name = opcode.ToUpper(), RepetitionsCount = repetitionsCount.Value };
            state.RegisterMacroExpansionStart(line);
            return line;
        }

        static ProcessedSourceLine ProcessEndmLine(string opcode, SourceLineWalker walker)
        {
            var success = state.RegisterMacroEnd();
            if(!success) {
                AddError(AssemblyErrorCode.EndMacroOutOfScope, $"ENDM found outside of a macro definition or macro expansion");
            }
            return new EndMacroLine();
        }

        static ProcessedSourceLine ProcessIrpLine(string opcode, SourceLineWalker walker) => ProcessIrpOrIrpcLine(opcode, walker, false);

        static ProcessedSourceLine ProcessIrpcLine(string opcode, SourceLineWalker walker) => ProcessIrpOrIrpcLine(opcode, walker, true);

        static ProcessedSourceLine ProcessIrpOrIrpcLine(string opcode, SourceLineWalker walker, bool isIrpc)
        {
            if(walker.AtEndOfLine) {
                AddError(AssemblyErrorCode.MissingValue,
                    isIrpc ?
                    $"{opcode.ToUpper()} requires two arguments: parameter placeholder and a string of parameter characters" :
                    $"{opcode.ToUpper()} requires two arguments: parameter placeholder and a list of parameters enclosed in < >");
                return new MacroExpansionLine();
            }

            var placeholder = walker.ExtractExpression();
            if(!labelRegex.IsMatch(placeholder)) {
                AddError(AssemblyErrorCode.InvalidArgument, $"Invalid placeholder argument for {opcode.ToUpper()}");
                walker.DiscardRemaining();
                return new MacroExpansionLine();
            }

            if(!walker.SkipComma()) {
                AddError(AssemblyErrorCode.MissingValue,
                    isIrpc ?
                    $"{opcode.ToUpper()} requires two arguments: parameter placeholder and a string of parameter characters" :
                    $"{opcode.ToUpper()} requires two arguments: parameter placeholder and a list of parameters enclosed in < >");
                return new MacroExpansionLine();
            }

            var (argsList, missingDelimiterCounter) = isIrpc ? walker.ExtractArgsListForIrpc() : walker.ExtractArgsListForIrp();
            if(argsList is null) {
                AddError(AssemblyErrorCode.InvalidArgument, $"Invalid parameters argument for {opcode.ToUpper()}, it must be a list of parameters enclosed in < >");
                walker.DiscardRemaining();
                return new MacroExpansionLine();
            }
            if(missingDelimiterCounter == 1) {
                AddError(AssemblyErrorCode.MissingDelimiterInMacroArgsList,
                    isIrpc ?
                    $"Missing '>' delimiter at the end of parameter characters for {opcode.ToUpper()}" :
                    $"Missing '>' delimiter at the end of parameters list for {opcode.ToUpper()}");
            }
            else if(missingDelimiterCounter > 1) {
                AddError(AssemblyErrorCode.MissingDelimiterInMacroArgsList,
                    isIrpc ?
                    $"Missing '>' delimiters ({missingDelimiterCounter} levels) at the end of parameter characters for {opcode.ToUpper()}" :
                    $"Missing '>' delimiters ({missingDelimiterCounter} levels) at the end of parameters list for {opcode.ToUpper()}");
            }

            if(!isIrpc) {
                if(!EvaluateExpressionsInMacroExpansionParameters(opcode, argsList)) {
                    return new MacroExpansionLine();
                }
            }

            var line = new MacroExpansionLine() { MacroType = MacroType.ReptWithArgs, Placeholder = placeholder, Name = opcode.ToUpper(), Parameters = argsList };
            state.RegisterMacroExpansionStart(line);
            return line;
        }

        private static bool EvaluateExpressionsInMacroExpansionParameters(string opcode, string[] macroExpansionParameters)
        {
            for(int i = 0; i < macroExpansionParameters.Length; i++) {
                if(macroExpansionParameters[i] == "" || macroExpansionParameters[i][0] != '\u0001') {
                    continue;
                }

                var arg = macroExpansionParameters[i][1..].Trim();

                try {
                    var argExpression = state.GetExpressionFor(arg);
                    argExpression.ValidateAndPostifixize();
                    var argValue = argExpression.Evaluate();
                    if(!argValue.IsAbsolute) {
                        AddError(AssemblyErrorCode.InvalidForRelocatable, $"Expressions used as parameters for {opcode.ToUpper()} must be absolute, '{arg}' evaluates to {argValue}");
                        return false;
                    }
                    macroExpansionParameters[i] = Expression.NumberToStringInCurrentRadix(argValue.Value);
                }
                catch(InvalidExpressionException ex) {
                    AddError(ex.ErrorCode, $"Invalid expression '{arg}' for {opcode.ToUpper()}: {ex.Message}");
                    return false;
                }
            }

            return true;
        }

        static ProcessedSourceLine ProcessIrpsLine(string opcode, SourceLineWalker walker)
        {
            if(walker.AtEndOfLine) {
                AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} requires two arguments: parameter placeholder and a \" or ' delimited string");
                return new MacroExpansionLine();
            }

            var placeholder = walker.ExtractExpression();
            if(!labelRegex.IsMatch(placeholder)) {
                AddError(AssemblyErrorCode.InvalidArgument, $"Invalid placeholder argument for {opcode.ToUpper()}");
                walker.DiscardRemaining();
                return new MacroExpansionLine();
            }

            if(!walker.SkipComma()) {
                AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} requires two arguments: parameter placeholder and a \" or ' delimited string");
                return new MacroExpansionLine();
            }

            var stringText = walker.ExtractExpression();
            try {
                var expression = state.GetExpressionFor(stringText, true);
                if(!expression.IsRawBytesOutput) {
                    AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} requires two arguments: parameter placeholder and a \" or ' delimited string");
                    return new MacroExpansionLine();
                }
                stringText = ((RawBytesOutput)expression.Parts[0]).OriginalString;
            }
            catch(InvalidExpressionException ex) {
                AddError(ex.ErrorCode, $"Invalid expression '{stringText}' for {opcode.ToUpper()}: {ex.Message}");
                return new MacroExpansionLine();
            }

            var argsList = stringText.ToCharArray().Select(ch => ch.ToString()).ToArray();

            var line = new MacroExpansionLine() { MacroType = MacroType.ReptWithArgs, Placeholder = placeholder, Name = opcode.ToUpper(), Parameters = argsList };
            state.RegisterMacroExpansionStart(line);
            return line;
        }

        static ProcessedSourceLine ProcessNamedMacroDefinitionLine(string name, SourceLineWalker walker)
        {
            if(state.NamedMacroExists(name)) {
                if(state.InPass1) {
                    AddError(AssemblyErrorCode.DuplicatedMacro, $"A macro named {name.ToUpper()} already exists, new definition will replace the old one");
                }
                state.RemoveNamedMacroDefinition(name);
            }

            if(MacroDefinitionState.DefiningNamedMacro) {
                AddError(AssemblyErrorCode.NestedMacro, $"Nested named macro definitions are not allowed");
                walker.DiscardRemaining();
                return new NamedMacroDefinitionLine();
            }

            if(MacroDefinitionState.DefiningMacro) {
                AddError(AssemblyErrorCode.NestedMacro, $"Named macro definitions inside REPT macros are not allowed");
                walker.DiscardRemaining();
                return new NamedMacroDefinitionLine();
            }

            var args = new List<string>();
            while(!walker.AtEndOfLine) {
                var arg = walker.ExtractExpression();
                if(!labelRegex.IsMatch(arg)) {
                    AddError(AssemblyErrorCode.InvalidArgument, $"'{arg}' is not a valid macro argument");
                    return new NamedMacroDefinitionLine();
                }
                args.Add(arg);
            }

            var line = new NamedMacroDefinitionLine() { Name = name, Arguments = args.ToArray() };
            state.RegisterNamedMacroDefinitionStart(line);
            return line;
        }

        static ProcessedSourceLine ProcessNamedMacroExpansion(string opcode, string macroName, SourceLineWalker walker)
        {
            var (args, missingDelimiterCounter) = walker.ExtractArgsListForIrp(false);
            if(args is null) {
                args = Array.Empty<string>();
            }
            else if(missingDelimiterCounter == 1) {
                AddError(AssemblyErrorCode.MissingDelimiterInMacroArgsList, $"Missing '>' delimiter at the end of parameters list for {macroName.ToUpper()}");
            }
            else if(missingDelimiterCounter > 1) {
                AddError(AssemblyErrorCode.MissingDelimiterInMacroArgsList, $"Missing '>' delimiters ({missingDelimiterCounter} levels) at the end of parameters list for {macroName.ToUpper()}");
            }

            if(!EvaluateExpressionsInMacroExpansionParameters("MACRO", args)) {
                return new MacroExpansionLine();
            }

            var line = new MacroExpansionLine() { MacroType = MacroType.Named, Name = macroName, Parameters = args.ToArray() };
            state.RegisterMacroExpansionStart(line);
            return line;
        }

        static ProcessedSourceLine ProcessExitmLine(string opcode, SourceLineWalker walker)
        {
            if(state.CurrentMacroMode != MacroMode.Expansion) {
                AddError(AssemblyErrorCode.ExitmOutOfScope, $"{opcode.ToUpper()} outside of any macro definition");
            }
            else {
                state.ExitMacro(true);
            }

            return new ExitMacroLine();
        }

        static ProcessedSourceLine ProcessContmLine(string opcode, SourceLineWalker walker)
        {
            if(state.CurrentMacroMode != MacroMode.Expansion) {
                AddError(AssemblyErrorCode.ExitmOutOfScope, $"{opcode.ToUpper()} outside of any macro definition");
            }
            else {
                state.ExitMacro(false);
            }

            return new ContinueMacroLine();
        }

        static ProcessedSourceLine ProcessLocalLine(string opcode, SourceLineWalker walker)
        {
            if(!state.ExpandingNamedMacro) {
                AddError(AssemblyErrorCode.LocalOutOfMacro, $"{opcode.ToUpper()} can't be used outside of a named macro");
                return new LocalLine();
            }
            else if(walker.AtEndOfLine) {
                AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} requires one or more symbol names as argument");
                return new RootLine();
            }

            var symbols = new List<string>();
            while(!walker.AtEndOfLine) {
                var symbol = walker.ExtractExpression();
                if(!IsValidSymbolName(symbol)) {
                    AddError(AssemblyErrorCode.InvalidLabel, $"'{symbol}' is not a valid symbol name");
                }
                else if(symbol != "") {
                    symbols.Add(symbol);
                }
            }

            var symbolsArray = symbols.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var macroExpansionLine = (NamedMacroExpansionState)state.CurrentMacroExpansionState;
            if(macroExpansionLine.LocalSymbols is not null) {
                symbolsArray = symbolsArray.Concat(macroExpansionLine.LocalSymbols).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            }

            macroExpansionLine.LocalSymbols = symbolsArray;

            return new LocalLine() { SymbolNames = symbolsArray };
        }

        static ProcessedSourceLine ProcessRelabLine(string opcode, SourceLineWalker walker)
        {
            state.RelativeLabelsEnabled = true;
            return new RelabLine() { Enable = true };
        }

        static ProcessedSourceLine ProcessXRelabLine(string opcode, SourceLineWalker walker)
        {
            state.RelativeLabelsEnabled = false;
            return new RelabLine() { Enable = false };
        }
    }
}
