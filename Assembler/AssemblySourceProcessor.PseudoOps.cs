using Konamiman.Nestor80.Assembler.Expressions;
using Konamiman.Nestor80.Assembler.Output;
using System.Text;
using System.Text.RegularExpressions;

namespace Konamiman.Nestor80.Assembler
{
    public partial class AssemblySourceProcessor
    {
        readonly Dictionary<string, Func<string, SourceLineWalker, ProcessedSourceLine>> PseudoOpProcessors = new(StringComparer.OrdinalIgnoreCase) {
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
            { ".PRINT", ProcessPrintLine },
            { ".PRINT1", ProcessPrint1Line },
            { ".PRINT2", ProcessPrint2Line },
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

                    var value = expression.TryEvaluate();
                    if(value is null) {
                        AddZero();
                        state.RegisterPendingExpression(line, expression, index, 1);
                    }
                    else if(isByte && !value.IsValidByte) {
                        AddZero();
                        AddError(AssemblyErrorCode.InvalidExpression, $"Invalid expression for {opcode.ToUpper()}: value {value:X4} can't be stored as a byte");
                    }
                    else if(value.IsAbsolute) {
                        outputBytes.Add(value.ValueAsByte);
                        if(!isByte)
                            outputBytes.Add((byte)((value.Value & 0xFF00) >> 8));
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
            else try{
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
                    var valueAddress = valueExpression.TryEvaluate();
                    if(valueAddress is null) {
                        state.RegisterPendingExpression(line, valueExpression, size: 1);
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
            state.SwitchToArea(area);

            return new ChangeAreaLine() {
                NewLocationArea = state.CurrentLocationArea,
                NewLocationCounter = state.CurrentLocationPointer
            };
        }

        static ProcessedSourceLine ProcessOrgLine(string opcode, SourceLineWalker walker)
        {
            if(walker.AtEndOfLine) {
                AddError(AssemblyErrorCode.LineHasNoEffect, "ORG without value will have no effect");
                return new ChangeOriginLine();
            }

            try {
                var valueExpressionString = walker.ExtractExpression();
                var valueExpression = Expression.Parse(valueExpressionString);
                valueExpression.ValidateAndPostifixize();
                var value = valueExpression.TryEvaluate();
                if(value is null) {
                    var line = new ChangeOriginLine();
                    state.RegisterPendingExpression(line, valueExpression);
                    return line;
                }
                else {
                    state.SwitchToLocation(value.Value);
                    return new ChangeOriginLine() { NewLocationArea = value.Type, NewLocationCounter = value.Value };
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
            if(existingSymbol is null) {
                state.AddSymbol(symbolName, type: SymbolType.External);
            }
            else if(existingSymbol.IsPublic || (existingSymbol.IsOfKnownType && !existingSymbol.IsExternal)) {
                AddError(AssemblyErrorCode.DuplicatedSymbol, $"{symbolName} is already defined, can't be declared as an external symbol");
            }
            else {
                //In case the symbol first appeared as part of an expression
                //and was therefore of type "Unknown"
                existingSymbol.Type = SymbolType.External;
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
            if(existingSymbol is null) {
                state.AddSymbol(symbolName, SymbolType.Unknown, isPublic: true);
            }
            else if(existingSymbol.IsExternal) {
                AddError(AssemblyErrorCode.DuplicatedSymbol, $"{symbolName} is already defined as an external symbol, can't be defined as public");
            }
            else {
                existingSymbol.IsPublic = true;
            }

            return new PublicDeclarationLine() { SymbolName = symbolName };
        }

        static ProcessedSourceLine ProcessEndLine(string opcode, SourceLineWalker walker)
        {
            if(walker.AtEndOfLine) {
                state.End(Address.AbsoluteZero);
                return new AssemblyEndLine() { Line = walker.SourceLine, Opcode = opcode };
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

        static ProcessedSourceLine ProcessConstantDefinition(string opcode, string name, SourceLineWalker walker)
        {
            var isRedefinition = !opcode.Equals("EQU", StringComparison.OrdinalIgnoreCase);
            var line = new ConstantDefinitionLine() { Name = name, IsRedefinible = isRedefinition };

            if(walker.AtEndOfLine) {
                AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} must be followed by a value");
                return line;
            }

            Address value;

            try {
                var valueExpressionString = walker.ExtractExpression();
                var valueExpression = Expression.Parse(valueExpressionString);
                valueExpression.ValidateAndPostifixize();
                value = valueExpression.TryEvaluate();
                if(value is null) {
                    state.RegisterPendingExpression(line, valueExpression);
                    return line;
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
                    AddError(AssemblyErrorCode.SymbolWithCpuRegisterName, $"{name.ToUpper()} is a Z80 register or flag name, defining it as a constant will prevent using it as a register or flag in Z80 instructions");
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
            throw new FatalErrorException(new AssemblyError(AssemblyErrorCode.UnsupportedCpu, "Unsupported CPU type: 8080", state.CurrentLineNumber));
        }

        static ProcessedSourceLine ProcessChangeCpuToZ80Line(string opcode, SourceLineWalker walker)
        {
            return new ChangeCpuLine() { Cpu = CpuType.Z80 };
        }

        static ProcessedSourceLine ProcessChangeCpuLine(string opcode, SourceLineWalker walker)
        {
            if(walker.AtEndOfLine) {
                AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} requires a CPU type as argument");
                return new ChangeCpuLine();
            }

            var symbol = walker.ExtractSymbol();
            var cpuType = CpuType.Unknown;
            try {
                cpuType = (CpuType)Enum.Parse(typeof(CpuType), symbol, true);
            } catch(ArgumentException) {
                //Invalid CPU type
            }
            
            if(cpuType == CpuType.Unknown) {
                throw new FatalErrorException(new AssemblyError(AssemblyErrorCode.UnsupportedCpu, $"{opcode.ToUpper()}: Unknown CPU type: {symbol}", state.CurrentLineNumber));
            }

            return new ChangeCpuLine() { Cpu = cpuType };
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

        static ProcessedSourceLine ProcessSetProgramName(string opcode, SourceLineWalker walker, string programName)
        {
            var effectiveLineLength = walker.SourceLine.IndexOf(';');
            if(effectiveLineLength < 0) {
                effectiveLineLength = walker.SourceLine.Length;
            }

            var match = ProgramNameRegex.Match(programName);
            if(!match.Success) {
                AddError(AssemblyErrorCode.InvalidArgument, $"{opcode.ToUpper()}: the program name must be in the format ('NAME')");
                return new ProgramNameLine() { EffectiveLineLength = effectiveLineLength };
            }

            var name = match.Groups["name"].Value;
            return new ProgramNameLine() { Name = name, EffectiveLineLength = effectiveLineLength };
        }

        static ProcessedSourceLine ProcessSetListingTitleLine(string opcode, SourceLineWalker walker)
        {
            var title = walker.GetRemaining();
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
                TriggerPrintEvent(text);
                return new PrintLine() { PrintedText = text, EffectiveLineLength = walker.SourceLine.Length };
            }

            var effectiveText = text[..(endDelimiterPosition + 1)];
            TriggerPrintEvent(effectiveText);
            return new PrintLine() { PrintedText = effectiveText, EffectiveLineLength = walker.SourceLine.Length - (text.Length - endDelimiterPosition) + 1 };
        }

        static void TriggerPrintEvent(string message, int? ifPass = null)
        {
            if(PrintMessage is not null && ((ifPass is null) || (state.InPass1 && ifPass == 1) || (state.InPass2 && ifPass == 2)))
                PrintMessage(null, message);
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
                return new ExternalDeclarationLine();
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
            => ProcessPrintLineCore(opcode, walker, null);

        static ProcessedSourceLine ProcessPrint1Line(string opcode, SourceLineWalker walker)
            => ProcessPrintLineCore(opcode, walker, 1);

        static ProcessedSourceLine ProcessPrint2Line(string opcode, SourceLineWalker walker)
            => ProcessPrintLineCore(opcode, walker, 2);

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
        static ProcessedSourceLine ProcessPrintLineCore(string opcode, SourceLineWalker walker, int? printInPass)
        {
            if(walker.AtEndOfLine) {
                AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} requires the text to print as argument");
                return new PrintLine();
            }

            var rawText = walker.GetRemaining();
            var sb = new StringBuilder();
            var lastIndex = 0;
            var expressionMatches = printStringExpressionRegex.Matches(rawText);
            if(expressionMatches.Count == 0) {
                TriggerPrintEvent(rawText, printInPass);
                return new PrintLine() { Line = rawText, EffectiveLineLength = walker.SourceLine.Length, PrintInPass = printInPass };
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
                if(colonIndex != -1 && colonIndex != expressionText.Length-1 && colonIndex != 0) {
                    formatSpecifier = expressionText[(colonIndex + 1)..].Replace('h', 'x').Replace('H', 'X');
                    expressionText = expressionText[..colonIndex];
                }

                try {
                    var expression = Expression.Parse(expressionText);
                    expression.ValidateAndPostifixize();
                    expressionValue = expression.TryEvaluate();
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
            TriggerPrintEvent(finalText, printInPass);
            return new PrintLine() { PrintedText = finalText, EffectiveLineLength = walker.SourceLine.Length, PrintInPass = printInPass };
        }
    }
}
