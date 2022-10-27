using Konamiman.Nestor80.Assembler.Expressions;
using Konamiman.Nestor80.Assembler.Output;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Konamiman.Nestor80.Assembler
{
    public partial class AssemblySourceProcessor
    {
        readonly static Dictionary<string, Func<string, SourceLineWalker, ProcessedSourceLine>> PseudoOpProcessors = new(StringComparer.OrdinalIgnoreCase) {
            { "DB", ProcessDefbLine },
            { "DEFB", ProcessDefbLine },
            { "DEFM", ProcessDefbLine },
            { "DW", ProcessDefwLine },
            { "DEFW", ProcessDefwLine },
            { "DS", ProcessDefsLine },
            { "DEFS", ProcessDefsLine },
            { "DC", ProcessDcLine },
            { "CSEG", ProcessCsegLine },
            { "DSEG", ProcessDsegLine },
            { "ASEG", ProcessAsegLine },
            { "ORG", ProcessOrgLine },
            { "EXT", ProcessExternalDeclarationLine },
            { "EXTRN", ProcessExternalDeclarationLine },
            { "EXTERNAL", ProcessExternalDeclarationLine },
            { "PUBLIC", ProcessPublicDeclarationLine },
            { "GLOBAL", ProcessPublicDeclarationLine },
            { "ENTRY", ProcessPublicDeclarationLine },
            { "END", ProcessEndLine },
            { ".COMMENT", ProcessDelimitedCommentStartLine },
            { ".STRENC", ProcessSetEncodingLine },
            { ".STRESC", ProcessChangeStringEscapingLine },
            { ".RADIX", ProcessChangeRadixLine },
            { ".8080", ProcessChangeCpuTo8080Line },
            { ".Z80", ProcessChangeCpuToZ80Line },
            { ".CPU", ProcessChangeCpuLine },
            { "NAME", ProcessSetProgramNameLine },
            { "TITLE", ProcessSetListingTitleLine },
            { "SUBTTL", ProcessSetListingSubtitleLine },
            { "$TITLE", ProcessLegacySetListingSubtitleLine },
            { "PAGE", ProcessSetListingNewPageLine },
            { "$EJECT", ProcessSetListingNewPageLine },
            { ".PRINTX", ProcessPrintxLine },
            { "DEFZ", ProcessDefineZeroTerminatedStringLine },
            { "DZ", ProcessDefineZeroTerminatedStringLine },
            { ".REQUEST", ProcessRequestLinkFilesLine },
            { ".LIST", ProcessListingControlLine },
            { ".XLIST", ProcessListingControlLine },
            { ".TFCOND", ProcessListingControlLine },
            { ".SFCOND", ProcessListingControlLine },
            { ".LFCOND", ProcessListingControlLine },
            { ".CREF", ProcessListingControlLine },
            { ".XCREF", ProcessListingControlLine },
            { ".LALL", ProcessListingControlLine },
            { ".SALL", ProcessListingControlLine },
            { ".XALL", ProcessListingControlLine },
            { ".PRINT", ProcessPrintLine },
            { ".PRINT1", ProcessPrint1Line },
            { ".PRINT2", ProcessPrint2Line },
            { "IF", ProcessIfTrueLine },
            { "COND", ProcessIfTrueLine },
            { "IFT", ProcessIfTrueLine },
            { "IFE", ProcessIfFalseLine },
            { "IFF", ProcessIfFalseLine },
            { "IFDEF", ProcessIfDefinedLine },
            { "IFNDEF", ProcessIfNotDefinedLine },
            { "IF1", ProcessIf1Line },
            { "IF2", ProcessIf2Line },
            { "ELSE", ProcessElseLine },
            { "ENDIF", ProcessEndifLine },
            { "ENDC", ProcessEndifLine },
            { "IFB", ProcessIfBlankLine },
            { "IFNB", ProcessIfNotBlankLine },
            { "IFIDN", ProcessIfIdenticalLine },
            { "IFDIF", ProcessIfDifferentLine },
            { ".WARN", ProcessUserWarningLine },
            { ".ERROR", ProcessUserErrorLine },
            { ".FATAL", ProcessUserFatalLine },
            { ".PHASE", ProcessPhaseLine },
            { ".DEPHASE", ProcessDephaseLine },
            { "ENDOUT", ProcessEndoutLine },
            { "MODULE", ProcessModuleLine },
            { "ENDMOD", ProcessEndModuleLine },
            { "ROOT", ProcessRootLine },
            { "IFABS", ProcessIfAbsLine },
            { "IFREL", ProcessIfRelLine }
        };

        static ProcessedSourceLine ProcessDefbLine(string opcode, SourceLineWalker walker)
            => ProcessDefbOrDefwLine(opcode, walker, true);

        static ProcessedSourceLine ProcessDefwLine(string opcode, SourceLineWalker walker)
            => ProcessDefbOrDefwLine(opcode, walker, false);

        static ProcessedSourceLine ProcessDefbOrDefwLine(string opcode, SourceLineWalker walker, bool isByte)
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
                try {
                    var expression = Expression.Parse(expressionText, forDefb: isByte);
                    expression.ValidateAndPostifixize();

                    if(expression.IsRawBytesOutput) {
                        outputBytes.AddRange((RawBytesOutput)expression.Parts[0]);
                        continue;
                    }

                    var value = expression.EvaluateIfNoSymbols();
                    if(value is null) {
                        AddZero();
                        state.RegisterPendingExpression(line, expression, index, argumentType: isByte ? CpuInstrArgType.Byte : CpuInstrArgType.Word);
                        if(!isByte) index++;
                    }
                    else if(isByte && !value.IsValidByte) {
                        AddZero();
                        AddError(AssemblyErrorCode.InvalidExpression, $"Invalid expression for {opcode.ToUpper()}: value {value:X4}h can't be stored as a byte");
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
                        relocatables.Add(new RelocatableAddress() { Index = index, IsByte = isByte, Type = value.Type, Value = value.Value });
                    }
                }
                catch(InvalidExpressionException ex) {
                    AddZero();
                    AddError(AssemblyErrorCode.InvalidExpression, $"Invalid expression for {opcode.ToUpper()}: {ex.Message}");
                }
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
                    var lengthExpression = Expression.Parse(lengthExpressionText);
                    lengthExpression.ValidateAndPostifixize();
                    var lengthAddress = lengthExpression.Evaluate();
                    if(!lengthAddress.IsAbsolute) {
                        throw new InvalidExpressionException("the length argument must evaluate to an absolute value");
                    }
                    length = lengthAddress.Value;

                    if(!walker.AtEndOfLine) {
                        var valueExpressionText = walker.ExtractExpression();
                        var valueExpression = Expression.Parse(valueExpressionText);
                        valueExpression.ValidateAndPostifixize();
                        var valueAddress = valueExpression.EvaluateIfNoSymbols();
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
                    AddError(AssemblyErrorCode.InvalidExpression, $"Invalid expression for {opcode.ToUpper()}: {ex.Message}");
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
                    var expression = Expression.Parse(expressionText, forDefb: true);

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
                    AddError(AssemblyErrorCode.InvalidExpression, $"Invalid expression: {ex.Message}");
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

        static ProcessedSourceLine ProcessChangeAreaLine(AddressType area)
        {
            if(buildType == BuildType.Absolute && area != AddressType.ASEG) {
                AddError(AssemblyErrorCode.IgnoredForAbsoluteOutput, $"Changing area to {area} when the output type is absolute has no effect");
            }

            if(state.IsCurrentlyPhased) {
                state.AddError(AssemblyErrorCode.InvalidInPhased, "Changing the location area is not allowed inside a .PHASE block");
                return new ChangeAreaLine();
            }

            state.SwitchToArea(area);

            return new ChangeAreaLine() {
                NewLocationArea = state.CurrentLocationArea,
                NewLocationCounter = state.CurrentLocationPointer
            };
        }

        static ProcessedSourceLine ProcessOrgLine(string opcode, SourceLineWalker walker)
        {
            if(state.IsCurrentlyPhased) {
                state.AddError(AssemblyErrorCode.InvalidInPhased, "Changing the location pointer is not allowed inside a .PHASE block");
                return new ChangeOriginLine();
            }

            if(walker.AtEndOfLine) {
                AddError(AssemblyErrorCode.LineHasNoEffect, "ORG without value will have no effect");
                return new ChangeOriginLine();
            }

            try {
                var valueExpressionString = walker.ExtractExpression();
                var valueExpression = Expression.Parse(valueExpressionString);
                valueExpression.ValidateAndPostifixize();
                var value = valueExpression.EvaluateIfNoSymbols();
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
                AddError(AssemblyErrorCode.InvalidExpression, $"Invalid expression for {opcode.ToUpper()}: {ex.Message}");
                return new ChangeOriginLine();
            }
        }

        static ProcessedSourceLine ProcessExternalDeclarationLine(string opcode, SourceLineWalker walker)
        {
            if(walker.AtEndOfLine) {
                AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} must be followed by a symbol name");
                return new ExternalDeclarationLine();
            }

            var symbolName = walker.ExtractSymbol();
            if(!externalSymbolRegex.IsMatch(symbolName)) {
                AddError(AssemblyErrorCode.InvalidLabel, $"{symbolName} is not a valid external symbol name, it contains invalid characters");
                return new ExternalDeclarationLine() { SymbolName = symbolName };
            }

            var existingSymbol = state.GetSymbol(symbolName);
            var success = true;
            if(existingSymbol is null) {
                state.AddSymbol(symbolName, type: SymbolType.External);
            }
            else if(existingSymbol.IsPublic || (existingSymbol.IsOfKnownType && !existingSymbol.IsExternal)) {
                AddError(AssemblyErrorCode.DuplicatedSymbol, $"{symbolName} is already defined, can't be declared as an external symbol");
                success = false;
            }
            else {
                //In case the symbol first appeared as part of an expression
                //and was therefore of type "Unknown"
                existingSymbol.Type = SymbolType.External;
            }

            if(success) {
                if(buildType == BuildType.Automatic) {
                    SetBuildType(BuildType.Relocatable);
                }
                else if(buildType == BuildType.Absolute) {
                    AddError(AssemblyErrorCode.InvalidForAbsoluteOutput, $"Symbol {symbolName.ToUpper()} is declared as external, but that's not allowed when the output type is absolute");
                }
            }

            return new ExternalDeclarationLine() { SymbolName = symbolName };
        }

        static ProcessedSourceLine ProcessPublicDeclarationLine(string opcode, SourceLineWalker walker)
        {
            if(walker.AtEndOfLine) {
                AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} must be followed by a label name");
                return new PublicDeclarationLine();
            }

            var symbolName = walker.ExtractSymbol();
            if(!externalSymbolRegex.IsMatch(symbolName)) {
                AddError(AssemblyErrorCode.InvalidLabel, $"{symbolName} is not a valid public symbol name, it contains invalid characters");
                return new ExternalDeclarationLine() { SymbolName = symbolName };
            }

            var existingSymbol = state.GetSymbol(symbolName);
            var success = true;
            if(existingSymbol is null) {
                state.AddSymbol(symbolName, SymbolType.Unknown, isPublic: true);
            }
            else if(existingSymbol.IsExternal) {
                AddError(AssemblyErrorCode.DuplicatedSymbol, $"{symbolName} is already defined as an external symbol, can't be defined as public");
                success = false;
            }
            else {
                existingSymbol.IsPublic = true;
            }

            if(success) {
                if(buildType == BuildType.Automatic) {
                    SetBuildType(BuildType.Relocatable);
                }
                else if(buildType == BuildType.Absolute) {
                    AddError(AssemblyErrorCode.IgnoredForAbsoluteOutput, $"Symbol {symbolName.ToUpper()} is declared as public, but that has no effect when the output type is absolute");
                }
            }

            return new PublicDeclarationLine() { SymbolName = symbolName };
        }

        static ProcessedSourceLine ProcessEndLine(string opcode, SourceLineWalker walker)
        {
            if(walker.AtEndOfLine) {
                state.End(Address.AbsoluteZero);
                return new AssemblyEndLine() { Line = walker.SourceLine };
            }

            if(buildType == BuildType.Absolute) {
                AddError(AssemblyErrorCode.IgnoredForAbsoluteOutput, "The argument of the END statement is ignored when tthe output type is absolute");
                state.End(Address.AbsoluteZero);
                return new AssemblyEndLine() { Line = walker.SourceLine, EffectiveLineLength = walker.DiscardRemaining() };
            }

            try {
                var endAddressText = walker.ExtractExpression();
                var endAddressExpression = Expression.Parse(endAddressText);
                endAddressExpression.ValidateAndPostifixize();

                //Note that we are ending source code parsing here,
                //therefore, the end address expression must be evaluable at this point!
                var endAddress = endAddressExpression.Evaluate();

                state.End(endAddress);
                return new AssemblyEndLine() { EndAddress = endAddress.Value, EndAddressArea = endAddress.Type };
            }
            catch(InvalidExpressionException ex) {
                AddError(AssemblyErrorCode.InvalidExpression, $"Invalid expression for {opcode.ToUpper()}: {ex.Message}");
                state.End(Address.AbsoluteZero);
                return new AssemblyEndLine();
            }
        }

        static ProcessedSourceLine ProcessDelimitedCommentStartLine(string opcode, SourceLineWalker walker)
        {
            if(walker.AtEndOfLine) {
                AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} needs one comment delimiter character");
                return new DelimitedCommandLine() { Delimiter = '\0', IsLastLine = true };
            }

            var delimiter = walker.ExtractSymbol()[0];
            state.MultiLineCommandDelimiter = delimiter;
            return new DelimitedCommandLine() { Delimiter = delimiter };
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
                    var valueExpression = Expression.Parse(valueExpressionString);
                    valueExpression.ValidateAndPostifixize();
                    value = valueExpression.TryEvaluate();
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
                AddError(AssemblyErrorCode.InvalidExpression, $"Invalid expression for {opcode.ToUpper()}: {ex.Message}");
                return line;
            }

            line.ValueArea = value.Type;
            line.Value = value.Value;

            var symbol = state.GetSymbol(name);

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
                var valueExpression = Expression.Parse(valueExpressionString);
                valueExpression.ValidateAndPostifixize();
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
                AddError(AssemblyErrorCode.InvalidExpression, $"Invalid expression for {opcode.ToUpper()}: {ex.Message}");
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
                return new ChangeListingPageLine();
            }

            try {
                var pageSizeText = walker.ExtractExpression();
                if(string.Equals("BREAK", pageSizeText, StringComparison.OrdinalIgnoreCase)) {
                    return new ChangeListingPageLine() { IsMainPageChange = true };
                }

                var pageSizeExpression = Expression.Parse(pageSizeText);
                pageSizeExpression.ValidateAndPostifixize();
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
                AddError(AssemblyErrorCode.InvalidExpression, $"Invalid expression for {opcode.ToUpper()}: {ex.Message}");
                return new ChangeListingPageLine();
            }
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

        static ProcessedSourceLine ProcessDefineZeroTerminatedStringLine(string opcode, SourceLineWalker walker)
        {
            var line = new DefbLine();
            byte[] outputBytes = null;

            if(walker.AtEndOfLine) {
                AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} needs one string as argument");
            }
            else try {
                    var expressionText = walker.ExtractExpression();
                    var expression = Expression.Parse(expressionText, forDefb: true);

                    if(expression.IsRawBytesOutput) {
                        var bytes = (RawBytesOutput)expression.Parts[0];
                        outputBytes = bytes.Concat(Expression.ZeroCharBytes).ToArray();
                    }
                    else {
                        AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} needs one single string as argument");
                    }
                }
                catch(InvalidExpressionException ex) {
                    AddError(AssemblyErrorCode.InvalidExpression, $"Invalid expression: {ex.Message}");
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
            var type = (ListingControlType)Enum.Parse(typeof(ListingControlType), opcode[1..], ignoreCase: true);
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

        // {expression}
        // {expression:d}
        // {expression:d5}
        // {expression:D5}
        // {expression:b}
        // {expression:b5}
        // {expression:B5}
        // {expression:h}
        // {expression:h5}
        // {expression:H5}
        static ProcessedSourceLine ProcessPrintOrUserErrorLine(string opcode, SourceLineWalker walker, int? printInPass = null, AssemblyErrorSeverity errorSeverity = AssemblyErrorSeverity.None)
        {
            var rawText = walker.GetRemainingRaw();
            var sb = new StringBuilder();
            var lastIndex = 0;
            var expressionMatches = printStringExpressionRegex.Matches(rawText);
            if(expressionMatches.Count == 0) {
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
                    var expression = Expression.Parse(expressionText);
                    expression.ValidateAndPostifixize();
                    expressionValue = expression.Evaluate();
                }
                catch(InvalidExpressionException ex) {
                    AddError(AssemblyErrorCode.InvalidExpression, $"Invalid expression for {opcode.ToUpper()}: {ex.Message}");
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
                    var expression = Expression.Parse(expressionText);
                    expression.ValidateAndPostifixize();
                    expressionValue = expression.Evaluate();
                }
                catch(InvalidExpressionException ex) {
                    AddError(AssemblyErrorCode.InvalidExpression, $"Invalid expression for {opcode.ToUpper()}: {ex.Message}");
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
                    return null;
                }

                return text != "";
            }

            return ProcessIfLine(opcode, evaluator);
        }

        static ProcessedSourceLine ProcessIfIdenticalLine(string opcode, SourceLineWalker walker)
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
                    return null;
                }

                return text1 == text2;
            }

            return ProcessIfLine(opcode, evaluator);
        }

        static ProcessedSourceLine ProcessIfDifferentLine(string opcode, SourceLineWalker walker)
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
                    return null;
                }

                return text1 != text2;
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

        static ProcessedSourceLine ProcessIfLine(string opcode, Func<bool?> evaluator)
        {
            var mustEvaluateTo = evaluator();
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
                    var phaseAddressExpression = Expression.Parse(phaseAddressText);
                    phaseAddressExpression.ValidateAndPostifixize();
                    phaseAddress = phaseAddressExpression.Evaluate();
                }
                catch(InvalidExpressionException ex) {
                    AddError(AssemblyErrorCode.InvalidExpression, $"Invalid expression for {opcode.ToUpper()}: {ex.Message}");
                    return new PhaseLine();
                }
            }

            if(!phaseAddress.IsAbsolute) {
                AddError(AssemblyErrorCode.InvalidArgument, $"Invalid expression for {opcode.ToUpper()}: the value must be absolute");
                walker.DiscardRemaining();
                return new PhaseLine();
            }

            state.EnterPhase(phaseAddress.Value);
            return new PhaseLine() { NewLocationArea = AddressType.ASEG, NewLocationCounter = state.CurrentLocationPointer };
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
                    return new RootLine();
                }
                if(symbol != "") {
                    symbols.Add(symbol);
                }
            }

            if(state.CurrentModule is not null) {
                state.RegisterRootSymbols(symbols);
            }

            return new RootLine() { RootSymbols = symbols.ToArray() };
        }

    }
}
