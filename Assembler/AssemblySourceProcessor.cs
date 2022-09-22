using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Konamiman.Nestor80.Assembler.Output;

[assembly: InternalsVisibleTo("AssemblerTests")]

namespace Konamiman.Nestor80.Assembler
{
    public partial class AssemblySourceProcessor
    {
        const int MAX_LINE_LENGTH = 1034;
        const int MAX_INCLUDE_NESTING = 34;

        private static AssemblyState state;

        private static BuildType buildType;

        private static readonly string[] z80RegisterNames = new[] {
            "A", "B", "C", "D", "E", "F", "H", "L", "I", "R",
            "AF", "HL", "BC", "DE", "IX", "IY",
            "SP", "IXH", "IXL", "IYH", "IYL",
            "NC", "Z", "NZ", "P", "PE", "PO"
        };

        private static readonly string[] conditionalInstructions = new[] {
            "IF", "IFT", "IFE", "IFF",
            "IFDEF", "IFNDEF", "IF1", "IF2",
            "IFB", "IFNB", "IFIDN", "IFDIF",
            "ELSE", "ENDIF"
        };

        private static CpuType currentCpu;
        private static Dictionary<CpuType, Dictionary<string, CpuInstruction[]>> cpuInstructions;
        private static Dictionary<string, CpuInstruction[]> currentCpuInstructions;

        private static readonly Regex labelRegex = new("^[\\w$@?._][\\w$@?._0-9]*:{0,2}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex externalSymbolRegex = new("^[a-zA-Z_$@?.][a-zA-Z_$@?.0-9]*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ProgramNameRegex = new(@"^\('(?<name>[a-zA-Z_$@?.][a-zA-Z_$@?.0-9]*)'\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex LegacySubtitleRegex = new(@"^\('(?<name>[^']*)'\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex printStringExpressionRegex = new(@"(?<=\{)[^}]*(?=\})", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        //Constant definitions are considered pseudo-ops, but they are handled as a special case
        //(instead of being included in PseudoOpProcessors) because the actual opcode comes after the name of the constant
        private static readonly string[] constantDefinitionOpcodes = { "EQU", "DEFL", "SET", "ASET" };

        private static readonly ProcessedSourceLine blankLineWithoutLabel = new BlankLine();

        public static event EventHandler<AssemblyError> AssemblyErrorGenerated;
        public static event EventHandler<string> PrintMessage;
        public static event EventHandler<(int, BuildType)> BuildTypeAutomaticallySelected;

        private AssemblySourceProcessor()
        {
        }

        static AssemblySourceProcessor()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            cpuInstructions = new() {
                [CpuType.Z80] = Z80Instructions,
                [CpuType.R800] = Z80Instructions.ToDictionary(x => x.Key, x=>x.Value, StringComparer.OrdinalIgnoreCase)
            };
            foreach(var entry in R800Instructions) {
                cpuInstructions[CpuType.R800].Add(entry.Key, entry.Value);
            }
        }

        public static AssemblyResult Assemble(string source, AssemblyConfiguration configuration = null)
        {
            var sourceStream = new MemoryStream(Encoding.UTF8.GetBytes(source));
            return Assemble(sourceStream, Encoding.UTF8, configuration ?? new AssemblyConfiguration());
        }

        public static AssemblyResult Assemble(Stream sourceStream, Encoding sourceStreamEncoding, AssemblyConfiguration configuration = null)
        {
            return new AssemblySourceProcessor().AssembleCore(sourceStream, sourceStreamEncoding, configuration ?? new AssemblyConfiguration());
        }

        private AssemblyResult AssembleCore(Stream sourceStream, Encoding sourceStreamEncoding, AssemblyConfiguration configuration)
        {
            try {
                state = new AssemblyState(configuration, sourceStream, sourceStreamEncoding);

                SetCurrentCpu(configuration.CpuName);
                buildType = configuration.BuildType;
                state.SwitchToArea(buildType != BuildType.Absolute ? AddressType.CSEG : AddressType.ASEG);

                var validInitialStringEncoding = SetStringEncoding(configuration.OutputStringEncoding, initial: true);
                if(!validInitialStringEncoding)
                    Expression.OutputStringEncoding = Encoding.ASCII;

                state.DefaultOutputStringEncoding = Expression.OutputStringEncoding;

                Expression.GetSymbol = GetSymbolForExpression;
                Expression.AllowEscapesInStrings = configuration.AllowEscapesInStrings;

                DoPass1();
                if(!state.HasErrors) {
                    state.SwitchToPass2();
                    state.SwitchToArea(buildType != BuildType.Absolute ? AddressType.CSEG : AddressType.ASEG);
                    DoPass2();
                }
            }
            catch(FatalErrorException ex) {
                AddError(ex.Error);
            }
            catch(Exception ex) {
                AddError(
                    code: AssemblyErrorCode.UnexpectedError,
                    message: $"Unexpected error: ({ex.GetType().Name}) {ex.Message}"
                );
            }

            state.WrapUp();

            var symbols =
                state.GetSymbols().Select(s => new Symbol() {
                    Name = s.Name,
                    Type = s.Type,
                    Value = s.Value?.Value ?? 0,
                    ValueArea = s.Value?.Type ?? AddressType.ASEG,
                    CommonName = s.Value?.CommonBlockName
                }).ToArray();

            if(buildType == BuildType.Automatic)
                buildType = BuildType.Absolute;

            int programSize;
            if(buildType == BuildType.Absolute) {
                var firstOutputOrOrg = state.ProcessedLines.FirstOrDefault(l => l is ChangeOriginLine or IProducesOutput or DefineSpaceLine);
                if(firstOutputOrOrg is null || firstOutputOrOrg is not ChangeOriginLine) {
                    programSize = state.GetAreaSize(AddressType.ASEG);
                }
                else {
                    programSize = state.GetAreaSize(AddressType.ASEG) - ((ChangeOriginLine)firstOutputOrOrg).NewLocationCounter;
                }
            }
            else {
                programSize = state.GetAreaSize(AddressType.CSEG);
            }

            return new AssemblyResult() {
                ProgramName = state.ProgramName,
                ProgramAreaSize = programSize,
                DataAreaSize = state.GetAreaSize(AddressType.DSEG),
                CommonAreaSizes = new(), //TODO: Handle commons
                ProcessedLines = state.ProcessedLines.ToArray(),
                Symbols = symbols,
                Errors = state.GetErrors(),
                EndAddressArea = state.EndAddress is null ? AddressType.ASEG : state.EndAddress.Type,
                EndAddress = (ushort)(state.EndAddress is null ? 0 : state.EndAddress.Value),
                BuildType = buildType
            };
        }

        private void DoPass2()
        {
            //throw new NotImplementedException();
        }

        private void DoPass1()
        {
            while(!state.EndReached) {
                var sourceLine = state.SourceStreamReader.ReadLine();
                if(sourceLine == null) {
                    if(state.InsideIncludedFile) {
                        state.PopIncludeState();
                        continue;
                    }
                    break;
                }
                if(sourceLine.Length > MAX_LINE_LENGTH) {
                    ThrowFatal(AssemblyErrorCode.SourceLineTooLong, $"Line is too long, maximum allowed line length is {MAX_LINE_LENGTH} characters");
                }

                var processedLine = ProcessSourceLine(sourceLine);
                state.ProcessedLines.Add(processedLine);
                state.IncreaseLineNumber();
            }

            //In case END is found inside an included file
            while(state.InsideIncludedFile) {
                state.PopIncludeState();
            }

            if(state.InConditionalBlock) {
                AddError(AssemblyErrorCode.UnterminatedConditional, "Unterminated conditional block", withLineNumber: false);
            }

            if(!state.EndReached) {
                AddError(AssemblyErrorCode.NoEndStatement, "No END statement found", withLineNumber: false);
            }

            if(state.InsideMultiLineComment) {
                AddError(AssemblyErrorCode.UnterminatedComment, $"Unterminated .COMMENT block (delimiter: '{state.MultiLineCommandDelimiter}')", withLineNumber: false);
            }
        }

        private ProcessedSourceLine ProcessSourceLine(string line)
        {
            ProcessedSourceLine processedLine = null;
            SourceLineWalker walker;

            var formFeedCharsCount = 0;
            formFeedCharsCount = line.Count(ch => ch == '\f');
            if(formFeedCharsCount > 0) {
                line = line.Replace("\f", "");
            }

            if(state.InsideMultiLineComment) {
                if(string.IsNullOrWhiteSpace(line) || (walker = new SourceLineWalker(line)).AtEndOfLine) {
                    processedLine = new DelimitedCommandLine();
                }
                else if(walker.ExtractSymbol().Contains(state.MultiLineCommandDelimiter.Value)) {
                    processedLine = new DelimitedCommandLine() { IsLastLine = true, Delimiter = state.MultiLineCommandDelimiter };
                    state.MultiLineCommandDelimiter = null;
                }
                else {
                    processedLine = new DelimitedCommandLine();
                }

                processedLine.Line = line;
                processedLine.EffectiveLineLength = 0;
                processedLine.FormFeedsCount = formFeedCharsCount;
                return processedLine;
            }

            if(string.IsNullOrWhiteSpace(line)) {
                processedLine =
                    formFeedCharsCount == 0 ?
                    blankLineWithoutLabel :
                    new BlankLine() { FormFeedsCount = formFeedCharsCount };
                return processedLine;
            }

            walker = new SourceLineWalker(line);
            if(walker.AtEndOfLine) {
                processedLine = new CommentLine() { Line = line, EffectiveLineLength = walker.EffectiveLength, FormFeedsCount = formFeedCharsCount };
                return processedLine;
            }

            string label = null;
            string opcode = null;
            var symbol = walker.ExtractSymbol();

            //Label processing is tricky due to conditionals.
            //A label defined in a line that is inside a false conditional block isn't registered as a symbol,
            //and this includes the ELSE or ENDIF at the end of the block; but a label defined in
            //an ELSE or ENDIF line at the end of a truthy block must be registered:
            //
            //if 1
            //FOO: else   --> label registered
            //BAR: endif  --> label NOT registered
            //
            //if 0
            //FOO: else   --> label NOT registered
            //BAR: endif  --> label registered
            //
            //That's why we don't register the label beforehand unless the line contains only the label.

            if(symbol.EndsWith(':')) {
                if(labelRegex.IsMatch(symbol)) {
                    label = symbol;
                }
                else {
                    AddError(AssemblyErrorCode.InvalidLabel, $"Invalid label (contains illegal characters): {symbol}");
                }

                if(walker.AtEndOfLine) {
                    if(state.InFalseConditional) {
                        processedLine = new SkippedLine() { Line = line, EffectiveLineLength = 0, FormFeedsCount = formFeedCharsCount };
                    }
                    else if(walker.EffectiveLength == walker.SourceLine.Length) {
                        ProcessLabelDefinition(label);
                        processedLine =  new BlankLine() { Label = label};
                    }
                    else {
                        ProcessLabelDefinition(label);
                        processedLine = new CommentLine() { Line = walker.SourceLine, EffectiveLineLength = walker.EffectiveLength, Label = label };
                    }
                    return processedLine;
                }

                symbol = walker.ExtractSymbol();
            }

            // Constant definition check must go before any other opcode check,
            // since pseudo-ops and cpu instructions are valid constant names too
            // (but only if no label is defined in the line)
            //
            // Interesting edge case (compatible with Macro80):
            //
            // TITLE EQU 1      ---> defines constant "TITLE" with value 1
            // FOO: TITLE EQU 1 ---> sets the program title as "EQU 1"
            if(!state.InFalseConditional && label is null && !walker.AtEndOfLine) {
                walker.BackupPointer();
                var symbol2 = walker.ExtractSymbol();
                if(constantDefinitionOpcodes.Contains(symbol2, StringComparer.OrdinalIgnoreCase)) {
                    opcode = symbol2;
                    processedLine = ProcessConstantDefinition(opcode: opcode, name: symbol, walker: walker);
                }
                else {
                    walker.RestorePointer();
                }
            }

            if(state.InFalseConditional) {
                if(conditionalInstructions.Contains(symbol, StringComparer.OrdinalIgnoreCase)) {
                    opcode = symbol;
                    var processor = PseudoOpProcessors[opcode];
                    processedLine = processor(opcode, walker);
                    //Note that we can still be inside a false conditional block even after an ENDIF, if there are nested conditional blocks,
                    //e.g: IF 0 - IF 1 - ENDIF (still in false block here) - ENDIF (out of false block now)
                    if(state.InFalseConditional) {
                        processedLine = new SkippedLine() { Line = line, EffectiveLineLength = 0, FormFeedsCount = formFeedCharsCount };
                        return processedLine;
                    }
                }
                else {
                    processedLine = new SkippedLine() { Line = line, EffectiveLineLength = 0, FormFeedsCount = formFeedCharsCount };
                    return processedLine;
                }
            }

            Stream includeStream = null;
            if(processedLine is null) {
                if(label is not null) {
                    ProcessLabelDefinition(label);
                }

                if(PseudoOpProcessors.ContainsKey(symbol)) {
                    opcode = symbol;
                    var processor = PseudoOpProcessors[opcode];
                    processedLine = processor(opcode, walker);
                }
                else if(currentCpuInstructions.ContainsKey(symbol)) {
                    opcode = symbol;
                    processedLine = ProcessCpuInstruction(opcode, walker);
                }
                else if(symbol.Equals("INCLUDE", StringComparison.OrdinalIgnoreCase)) {
                    if(state.CurrentIncludesDeepLevel >= MAX_INCLUDE_NESTING) {
                        ThrowFatal(AssemblyErrorCode.TooManyNestedIncludes, $"Too many nested INCLUDEs, maximum nesting level allowed is {MAX_INCLUDE_NESTING}");
                    }
                    opcode = symbol;
                    (includeStream, processedLine) = ProcessIncludeLine(opcode, walker);
                }
                else if(symbol.StartsWith("NAME(", StringComparison.OrdinalIgnoreCase)) {
                    opcode = symbol[..4];
                    processedLine = ProcessSetProgramName(opcode, walker, symbol[4..]);
                }
                else if(symbol.StartsWith("$TITLE(", StringComparison.OrdinalIgnoreCase)) {
                    opcode = symbol[..6];
                    processedLine = ProcessLegacySetListingSubtitle(opcode, walker, symbol[6..] + (walker.AtEndOfLine ? "" : " " + walker.GetUntil(')')));
                }
                else {
                    throw new NotImplementedException("Can't parse line (yet): " + line);
                }
            }

            if(!walker.AtEndOfLine && !state.InsideMultiLineComment) {
                AddError(AssemblyErrorCode.UnexpectedContentAtEndOfLine, $"Unexpected content found at the end of the line: {walker.GetRemaining()}");
            }

            if(opcode is not null) {
                processedLine.Opcode = opcode;
            }

            processedLine.Line = line;
            if(processedLine.EffectiveLineLength == -1) processedLine.EffectiveLineLength = walker.DiscardRemaining();
            processedLine.Label = label;
            processedLine.FormFeedsCount = formFeedCharsCount;

            if(includeStream is not null) {
                state.PushIncludeState(includeStream, (IncludeLine)processedLine);
            }

            if(buildType == BuildType.Automatic) {
                //Build type is also automatically selected when a constant/label
                //is defined/referenced as public or external
                if(processedLine is ExternalDeclarationLine or PublicDeclarationLine) {
                    SetBuildType(BuildType.Relocatable);
                }
                else if(processedLine is ChangeAreaLine cal && cal.NewLocationArea != AddressType.ASEG) {
                    SetBuildType(BuildType.Relocatable);
                }
                else if(processedLine is ChangeOriginLine) {
                    SetBuildType(BuildType.Absolute);
                    state.SwitchToArea(AddressType.ASEG);
                }
                else if(processedLine is IProducesOutput or DefineSpaceLine) {
                    SetBuildType(BuildType.Relocatable);
                }
            }

            return processedLine;
        }

        internal static SymbolInfo GetSymbolForExpression(string name, bool isExternal)
        {
            if(name == "$")
                return new SymbolInfo() { Name = "$", Value = new Address(state.CurrentLocationArea, state.CurrentLocationPointer) };

            var symbol = state.GetSymbol(name);
            if(symbol is null) {
                state.AddSymbol(name, isExternal ? SymbolType.External : SymbolType.Unknown);
                symbol = state.GetSymbol(name);

                if(isExternal) {
                    if(buildType == BuildType.Automatic) {
                        SetBuildType(BuildType.Relocatable);
                    }
                    else if(buildType == BuildType.Absolute) {
                        AddError(AssemblyErrorCode.InvalidForAbsoluteOutput, $"Expression references symbol {name.ToUpper()} as external, but that's not allowed when the output type is absolute");
                    }
                }
            }

            return symbol;
        }

        private static void ProcessLabelDefinition(string label)
        {
            var isPublic = label.EndsWith("::");
            var labelValue = label.TrimEnd(':');

            if(labelValue == "$") {
                AddError(AssemblyErrorCode.DollarAsLabel, "'$' defined as a label, but it actually represents the current location pointer");
            }

            var symbol = state.GetSymbol(labelValue);
            if(symbol == null) {
                if(isPublic && !externalSymbolRegex.IsMatch(labelValue)) {
                    AddError(AssemblyErrorCode.InvalidLabel, $"{labelValue} is not a valid public label name, it contains invalid characters");
                };
                if(z80RegisterNames.Contains(labelValue, StringComparer.OrdinalIgnoreCase)) {
                    AddError(AssemblyErrorCode.SymbolWithCpuRegisterName, $"{labelValue.ToUpper()} is a {currentCpu} register name, defining it as a label will prevent using it as a register in {currentCpu} instructions");
                }
                state.AddSymbol(labelValue, SymbolType.Label, state.GetCurrentLocation(), isPublic: isPublic);

                if(isPublic) {
                    if(buildType == BuildType.Automatic) {
                        SetBuildType(BuildType.Relocatable);
                    }
                    else if(buildType == BuildType.Absolute) {
                        AddError(AssemblyErrorCode.IgnoredForAbsoluteOutput, $"Label {labelValue.ToUpper()} is declared as public, but that has no effect when the output type is absolute");
                    }
                }

            }
            else if(symbol.IsExternal) {
                AddError(AssemblyErrorCode.DuplicatedSymbol, $"Symbol has been declared already as external: {labelValue}");
            }
            else if(symbol.IsConstant) {
                AddError(AssemblyErrorCode.DuplicatedSymbol, $"Symbol has been declared already with {symbol.Type.ToString().ToUpper()}: {labelValue}");
            }
            else if(symbol.HasKnownValue) {
                if(symbol.Value != state.GetCurrentLocation()) {
                    AddError(AssemblyErrorCode.DuplicatedSymbol, $"Duplicate label: {labelValue}");
                }
            }
            else {
                //Either PUBLIC declaration preceded label in code,
                //or the label first appeared as part of an expression
                symbol.Value = state.GetCurrentLocation();

                //In case the label first appeared as part of an expression
                //and thus was of type "Unknown"
                symbol.Type = SymbolType.Label;
            };
        }

        private static void SetBuildType(BuildType type)
        {
            buildType = type;
            if(BuildTypeAutomaticallySelected is not null) {
                BuildTypeAutomaticallySelected(null, (state.CurrentLineNumber, buildType));
            }
        }

        private static bool SetStringEncoding(string encodingNameOrCodepage, bool initial = false)
        {
            if(encodingNameOrCodepage.Equals("ASCII", StringComparison.OrdinalIgnoreCase))
                encodingNameOrCodepage = "US-ASCII";

            var isCodePage = int.TryParse(encodingNameOrCodepage, out int codePage);

            try {
                Expression.OutputStringEncoding =
                    isCodePage ?
                        Encoding.GetEncoding(codePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback) :
                        Encoding.GetEncoding(encodingNameOrCodepage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
                return true;
            }
            catch(Exception e) when (e is ArgumentException or NotSupportedException) {
                AddError(
                    AssemblyErrorCode.UnknownStringEncoding,
                    isCodePage ?
                        $"There's no known string encoding with the code page {codePage}" :
                        $"There's no known string encoding with the name '{encodingNameOrCodepage}'",
                    withLineNumber: !initial
                );
                return false;
            }
        }

        static void AddError(AssemblyError error)
        {
            state.AddError(error);
            if(AssemblyErrorGenerated is not null) {
                AssemblyErrorGenerated(null, error);
            }
        }

        static void AddError(AssemblyErrorCode code, string message, bool withLineNumber = true)
        {
            var error = state.AddError(code, message, withLineNumber);
            if(AssemblyErrorGenerated is not null) {
                AssemblyErrorGenerated(null, error);
            }
        }

        static void ThrowFatal(AssemblyErrorCode errorCode, string message)
        {
            throw new FatalErrorException(new AssemblyError(errorCode, message, state.CurrentLineNumber, state.CurrentIncludeFilename));
        }
    }
}
