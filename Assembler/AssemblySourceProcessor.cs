using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Konamiman.Nestor80.Assembler.ArithmeticOperations;
using Konamiman.Nestor80.Assembler.Expressions;
using Konamiman.Nestor80.Assembler.Expressions.ArithmeticOperations;
using Konamiman.Nestor80.Assembler.Output;

[assembly: InternalsVisibleTo("AssemblerTests")]

namespace Konamiman.Nestor80.Assembler
{
    public partial class AssemblySourceProcessor
    {
        const int MAX_LINE_LENGTH = 1034;
        const int MAX_INCLUDE_NESTING = 34;

        const RegexOptions RegxOp = RegexOptions.Compiled | RegexOptions.IgnoreCase;

        public const int MaxEffectiveExternalNameLength = 6;

        private static AssemblyState state;

        private static BuildType buildType;

        private static Stream includeStream;

        private static int maxErrors = 0;
        private static int errorsGenerated = 0;
        private static string programName = null;

        private static readonly string[] z80RegisterNames = new[] {
            "A", "B", "C", "D", "E", "F", "H", "L", "I", "R",
            "AF", "HL", "BC", "DE", "IX", "IY",
            "SP", "IXH", "IXL", "IYH", "IYL",
            "NC", "Z", "NZ", "P", "M", "PE", "PO"
        };

        private static readonly string[] conditionalInstructions = new[] {
            "IF", "IFT", "IFE", "IFF",
            "IFDEF", "IFNDEF", "IF1", "IF2",
            "IFB", "IFNB", "IFIDN", "IFDIF",
            "IFABS", "IFREL", "ELSE", "ENDIF",
            "IFCPU", "IFNCPU"
        };

        private static readonly string[] macroDefinitionOrExpansionInstructions = new[] {
            "MACRO", "REPT", "IRP", "IRPC"
        };

        private static readonly string[] instructionsNeedingPass2Reevaluation;

        private static CpuType currentCpu;

        private static readonly Regex labelRegex = new("^[\\w$@?._][\\w$@?._0-9]*:{0,2}$", RegxOp);
        private static readonly Regex externalSymbolRegex = new("^[a-zA-Z_$@?.][a-zA-Z_$@?.0-9]*$", RegxOp);
        private static readonly Regex ProgramNameRegex = new(@"^\('(?<name>[a-zA-Z_$@?.][a-zA-Z_$@?.0-9]*)'\)", RegxOp);
        private static readonly Regex LegacySubtitleRegex = new(@"^\('(?<name>[^']*)'\)", RegxOp);
        private static readonly Regex printStringExpressionRegex = new(@"(?<=\{)[^}]*(?=\})", RegxOp);

        //Constant definitions are considered pseudo-ops, but they are handled as a special case
        //(instead of being included in PseudoOpProcessors) because the actual opcode comes after the name of the constant
        private static readonly string[] constantDefinitionOpcodes = { "EQU", "DEFL", "SET", "ASET" };

        //Constant definition opcodes that are allowed after a symbol name followed by a colon
        private static readonly string[] constantDefinitionOpcodesMinusSet = { "EQU", "DEFL", "ASET" };

        private static readonly ProcessedSourceLine blankLineWithoutLabel = new BlankLine();

        public static event EventHandler<AssemblyError> AssemblyErrorGenerated;
        public static event EventHandler<string> PrintMessage;
        public static event EventHandler<(string, int, BuildType)> BuildTypeAutomaticallySelected;
        public static event EventHandler Pass2Started;
        public static event EventHandler IncludedFileFinished;

        private AssemblySourceProcessor()
        {
        }

        static AssemblySourceProcessor()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            instructionsNeedingPass2Reevaluation = conditionalInstructions.Concat(new[] { ".PHASE", ".DEPHASE" }).ToArray();
        }

        public static bool IsValidCpu(string cpuName)
        {
            return Enum.GetNames<CpuType>().Contains(cpuName, StringComparer.OrdinalIgnoreCase);
        }

        public static AssemblyResult Assemble(string source, AssemblyConfiguration configuration = null)
        {
            var sourceStream = new MemoryStream(Encoding.UTF8.GetBytes(source));
            return Assemble(sourceStream, Encoding.UTF8, configuration ?? new AssemblyConfiguration());
        }

        public static AssemblyResult Assemble(Stream sourceStream, Encoding sourceStreamEncoding, AssemblyConfiguration configuration)
        {
            try {
                includeStream = null;
                state = new AssemblyState(configuration, sourceStream, sourceStreamEncoding);

                ProcessPredefinedsymbols(configuration.PredefinedSymbols);
                maxErrors = configuration.MaxErrors;

                SetCurrentCpu(configuration.CpuName);
                buildType = configuration.BuildType;
                state.SwitchToArea(buildType != BuildType.Absolute ? AddressType.CSEG : AddressType.ASEG);

                var validInitialStringEncoding = SetStringEncoding(configuration.OutputStringEncoding, initial: true);
                if(!validInitialStringEncoding)
                    Expression.OutputStringEncoding = Encoding.ASCII;

                state.DefaultOutputStringEncoding = Expression.OutputStringEncoding;

                Expression.GetSymbol = GetSymbolForExpression;
                Expression.AllowEscapesInStrings = configuration.AllowEscapesInStrings;

                DoPass();
                if(!state.HasErrors) {
                    state.SwitchToPass2(buildType);
                    
                    if(Pass2Started is not null) {
                        Pass2Started(null, EventArgs.Empty);
                    }

                    DoPass();
                }
            }
            catch(FatalErrorException ex) {
                state.AddError(ex.Error);
                if(AssemblyErrorGenerated is not null) {
                    AssemblyErrorGenerated(null, ex.Error);
                }
            }
            catch(Exception ex) {
                AddError(
                    code: AssemblyErrorCode.UnexpectedError,
                    message: $"Unexpected error: ({ex.GetType().Name}) {ex.Message}"
                );
                
                throw;
            }

            state.WrapUp();

            var symbols =
                state.GetSymbols().Select(s => new Symbol() {
                    Name = s.Name,
                    EffectiveName = s.EffectiveName,
                    Type = s.Type,
                    IsPublic = s.IsPublic,
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
                ProgramName = programName,
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

        private static void ProcessPredefinedsymbols((string, ushort)[] predefinedSymbols)
        {
            foreach((var symbol, var value) in predefinedSymbols) {
                if(!IsValidSymbolName(symbol)) {
                    AddError(AssemblyErrorCode.InvalidLabel, $"{symbol} is not a valid symbol name");
                    continue;
                }

                if(state.HasSymbol(symbol)) {
                    //Since this runs before processing the source code,
                    //if the symbol exists it must have been added by this same method,
                    //thus it's a DEFL and can be safely redefined.
                    state.GetSymbolWithoutLocalNameReplacement(symbol).Value = Address.Absolute(value);
                }
                else {
                    state.AddSymbol(symbol, SymbolType.Defl, Address.Absolute(value));
                }
            }
        }

        private static void DoPass()
        {
            while(!state.EndReached) {
                var sourceLine = state.GetNextMacroExpansionLine();
                if(sourceLine is null) {
                    sourceLine = state.SourceStreamReader.ReadLine();
                    if(sourceLine == null) {
                        if(state.InsideIncludedFile) {
                            state.PopIncludeState();
                            if(IncludedFileFinished is not null) IncludedFileFinished(null, EventArgs.Empty);
                            continue;
                        }
                        break;
                    }
                }
                state.CurrentSourceLineText = sourceLine;
                if(sourceLine.Length > MAX_LINE_LENGTH) {
                    ThrowFatal(AssemblyErrorCode.SourceLineTooLong, $"Line is too long, maximum allowed line length is {MAX_LINE_LENGTH} characters");
                }

                var processedLine = ProcessSourceLine(sourceLine);

                if(state.InPass2 && state.ExpressionsPendingEvaluation.Any()) {
                    ProcessExpressionsPendingEvaluation(processedLine, state.ExpressionsPendingEvaluation);
                    state.ClearExpressionsPeindingEvaluation();
                }

                state.RegisterProcessedLine(processedLine);

                if(processedLine is IncludeLine il && includeStream is not null) {
                    state.PushIncludeState(includeStream, il);
                }
                else if(processedLine is PrintLine pl) {
                    TriggerPrintEvent(pl);
                }

                state.IncreaseLineNumber();
            }

            if(state.CurrentMacroMode is not MacroMode.None) {
                AddError(AssemblyErrorCode.UnterminatedMacro, "Unterminated macro");
            }

            //In case END is found inside an included file
            while(state.InsideIncludedFile) {
                state.PopIncludeState();
                if(IncludedFileFinished is not null) IncludedFileFinished(null, EventArgs.Empty);
            }

            AssemblyEndWarnings();
        }

        private static ProcessedSourceLine ProcessSourceLine(string line, int? formFeedCharsCount = null)
        {
            ProcessedSourceLine processedLine = null;
            SourceLineWalker walker;

            var definingMacro = state.CurrentMacroMode is MacroMode.Definition;

            if(formFeedCharsCount is null) {
                formFeedCharsCount = line.Count(ch => ch == '\f');
                if(formFeedCharsCount > 0) {
                    line = line.Replace("\f", "");
                }
            }

            if(line.Any(ch => char.IsControl(ch) && ch != '\t')) {
                line = new string(line.Where(ch => ch == '\t' || !char.IsControl(ch)).ToArray());
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
                processedLine.FormFeedsCount = formFeedCharsCount.Value;
                return processedLine;
            }

            if(string.IsNullOrWhiteSpace(line)) {
                if(definingMacro) {
                    AssemblyState.RegisterMacroDefinitionLine(line, false);
                    processedLine = new MacroDefinitionBodyLine() { Line = line, EffectiveLineLength = line.Length, FormFeedsCount = formFeedCharsCount.Value };
                }
                else if(state.InFalseConditional) {
                    processedLine = new SkippedLine() { Line = line, EffectiveLineLength = 0, FormFeedsCount = formFeedCharsCount.Value };
                }
                else {
                    processedLine =
                        formFeedCharsCount == 0 ?
                        blankLineWithoutLabel :
                        new BlankLine() { FormFeedsCount = formFeedCharsCount.Value };
                }
                return processedLine;
            }

            walker = new SourceLineWalker(line);
            if(walker.AtEndOfLine) {
                if(definingMacro) {
                    AssemblyState.RegisterMacroDefinitionLine(line, false);
                    walker.DiscardRemaining();
                    processedLine = new MacroDefinitionBodyLine() { Line = line, EffectiveLineLength = line.Length, FormFeedsCount = formFeedCharsCount.Value };
                }
                else if(state.InFalseConditional) {
                    processedLine = new SkippedLine() { Line = line, EffectiveLineLength = 0, FormFeedsCount = formFeedCharsCount.Value };
                }
                else {
                    processedLine = new CommentLine() { Line = line, EffectiveLineLength = walker.EffectiveLength, FormFeedsCount = formFeedCharsCount.Value };
                }
                return processedLine;
            }

            string label = null;
            string opcode = null;
            var symbol = walker.ExtractSymbol();

            if(definingMacro) {
                opcode = symbol;
                
                var stillInMacroDefinitionMode = true;
                if(string.Equals(symbol, "ENDM", StringComparison.InvariantCultureIgnoreCase)) {
                    processedLine = ProcessEndmLine(symbol, walker);

                    //We need to check if we are still in macro definition mode
                    //because the ENDM could be part of a nested REPT inside a MACRO or another REPT;
                    //and in that case the processed ENDM is just another regular macro definition line.
                    stillInMacroDefinitionMode = state.CurrentMacroMode is MacroMode.Definition;
                }

                if(stillInMacroDefinitionMode) {
                    AssemblyState.RegisterMacroDefinitionLine(line, macroDefinitionOrExpansionInstructions.Contains(symbol, StringComparer.OrdinalIgnoreCase));
                    walker.DiscardRemaining();
                    processedLine = new MacroDefinitionBodyLine() { Line = line, EffectiveLineLength = line.Length };
                }
            }

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

            if(!definingMacro && symbol.EndsWith(':')) {
                if(IsValidSymbolName(symbol)) {
                    label = symbol;
                }
                else {
                    AddError(AssemblyErrorCode.InvalidLabel, $"Invalid label: {symbol}");
                }

                if(walker.AtEndOfLine) {
                    if(state.InFalseConditional) {
                        processedLine = new SkippedLine() { Line = line, EffectiveLineLength = 0, FormFeedsCount = formFeedCharsCount.Value };
                    }
                    else if(walker.EffectiveLength == walker.SourceLine.Length) {
                        ProcessLabelDefinition(label);
                        processedLine =  new BlankLine() { Line = line, Label = label};
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
            if(!definingMacro && !state.InFalseConditional && !walker.AtEndOfLine) {
                if(label is not null && constantDefinitionOpcodesMinusSet.Contains(symbol, StringComparer.OrdinalIgnoreCase)) {
                    opcode = symbol;
                    processedLine = ProcessConstantDefinition(opcode: opcode, name: label.TrimEnd(':'), walker: walker);
                    label = null;
                }
                else if(label is not null && symbol.Equals("MACRO", StringComparison.OrdinalIgnoreCase)) {
                    opcode = symbol;
                    processedLine = ProcessNamedMacroDefinitionLine(name: label.TrimEnd(':'), walker: walker);
                }
                else if(label is null) {
                    walker.BackupPointer();
                    var symbol2 = walker.ExtractSymbol();
                    if(constantDefinitionOpcodes.Contains(symbol2, StringComparer.OrdinalIgnoreCase)) {
                        opcode = symbol2;
                        processedLine = ProcessConstantDefinition(opcode: opcode, name: symbol, walker: walker);
                    }
                    else if(symbol2.Equals("MACRO", StringComparison.OrdinalIgnoreCase)) {
                        opcode = symbol;
                        processedLine = ProcessNamedMacroDefinitionLine(name: symbol, walker: walker);
                    }
                    else {
                        walker.RestorePointer();
                    }
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
                        processedLine = new SkippedLine() { Line = line, EffectiveLineLength = 0, FormFeedsCount = formFeedCharsCount.Value };
                        return processedLine;
                    }
                }
                else {
                    processedLine = new SkippedLine() { Line = line, EffectiveLineLength = 0, FormFeedsCount = formFeedCharsCount.Value };
                    return processedLine;
                }
            }

            if(processedLine is null) {
                if(label is not null) {
                    ProcessLabelDefinition(label);
                }

                if(PseudoOpProcessors.ContainsKey(symbol)) {
                    opcode = symbol;
                    var processor = PseudoOpProcessors[opcode];
                    processedLine = processor(opcode, walker);
                }
                else if(Z80InstructionOpcodes.Contains(symbol, StringComparer.OrdinalIgnoreCase) ||
                    (currentCpu is CpuType.R800 && R800SpecificOpcodes.Contains(symbol, StringComparer.OrdinalIgnoreCase))) {
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
                else if(state.Configuration.AllowBareExpressions) {
                    opcode = "RAW DB";
                    processedLine = ProcessDefbLine(opcode, new SourceLineWalker(symbol + " " + walker.GetRemainingRaw()));
                }
                else if(state.NamedMacroExists(symbol)) {
                    opcode = "MACROEX";
                    processedLine = ProcessNamedMacroExpansion(opcode, symbol, walker);
                }
                else {
                    opcode = symbol;
                    AddError(AssemblyErrorCode.UnknownInstruction, $"Unknown instruction: {opcode}");
                    walker.DiscardRemaining();
                    processedLine = new UnknownInstructionLine() { Opcode = opcode, EffectiveLineLength = 0 };
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
            processedLine.FormFeedsCount = formFeedCharsCount.Value;

            if(buildType == BuildType.Automatic) {
                //Build type is also automatically selected when a constant/label
                //is defined/referenced as public or external
                if(processedLine is ExternalDeclarationLine or PublicDeclarationLine) {
                    SetBuildType(BuildType.Relocatable);
                }
                else if(processedLine is ChangeAreaLine cal && cal.NewLocationArea != AddressType.ASEG) {
                    SetBuildType(BuildType.Relocatable);
                }
                else if(processedLine is ChangeOriginLine col) {
                    SetBuildType(BuildType.Absolute);
                    state.SwitchToArea(AddressType.ASEG);
                    state.SwitchToLocation(col.NewLocationCounter);
                }
                else if(processedLine is IProducesOutput or DefineSpaceLine or LinkerFileReadRequestLine) {
                    SetBuildType(BuildType.Relocatable);
                }
            }

            return processedLine;
        }

        private static void AssemblyEndWarnings()
        {
            if(state.InConditionalBlock) {
                AddError(AssemblyErrorCode.UnterminatedConditional, "Unterminated conditional block", withLineNumber: false);
            }

            if(state.InsideMultiLineComment) {
                AddError(AssemblyErrorCode.UnterminatedComment, $"Unterminated .COMMENT block (delimiter: '{state.MultiLineCommandDelimiter}')", withLineNumber: false);
            }

            if(state.IsCurrentlyPhased) {
                AddError(AssemblyErrorCode.UnterminatedPhase, "Unterminated .PHASE block", withLineNumber: false);
            }

            if(!state.EndReached && buildType == BuildType.Relocatable) {
                AddError(AssemblyErrorCode.NoEndStatement, "No END statement found", withLineNumber: false);
            }

            if(state.CurrentModule is not null) {
                AddError(AssemblyErrorCode.UnterminatedModule, $"Unterminated module: {state.CurrentModule}");
            }
        }

        private static void ProcessExpressionsPendingEvaluation(ProcessedSourceLine processedLine, List<ExpressionPendingEvaluation> expressionsPendingEvaluation)
        {
            if(processedLine is ConstantDefinitionLine cdl) {
                foreach(var expressionPendingEvaluation in expressionsPendingEvaluation) {
                    ProcessConstantDefinition(cdl.Opcode, cdl.Name, expression: expressionPendingEvaluation.Expression);
                }
                return;
            }

            var line = (IProducesOutput)processedLine;
            var relocatables = new List<RelocatableOutputPart>();

            foreach(var expressionPendingEvaluation in expressionsPendingEvaluation) {
                var referencedSymbolNames = expressionPendingEvaluation.Expression.ReferencedSymbols.Select(s => new { s.SymbolName, IsRoot = s.IsRoot || s.IsExternal });
                var referencedSymbols = referencedSymbolNames.Select(s => state.GetSymbolWithoutLocalNameReplacement(s.IsRoot ? s.SymbolName : state.Modularize(s.SymbolName)));
                var hasExternalsOutsideTypeOperator = false;
                Address expressionValue = null;

                if(!expressionPendingEvaluation.Expression.HasTypeOperator && referencedSymbols.Any(s => s.IsExternal)) {
                    hasExternalsOutsideTypeOperator = true;
                }
                else {
                    try {
                        expressionValue = expressionPendingEvaluation.Expression.Evaluate();
                    }
                    catch(ExpressionReferencesExternalsException) {
                        hasExternalsOutsideTypeOperator = true;
                    }
                    catch(InvalidExpressionException ex) {
                        AddError(AssemblyErrorCode.InvalidExpression, $"Invalid expression for {processedLine.Opcode.ToUpper()}: {ex.Message}");
                        continue;
                    }
                }

                if(hasExternalsOutsideTypeOperator) {
                    var unknownSymbols = referencedSymbols.Where(s => !s.IsExternal && !s.HasKnownValue);
                    foreach(var symbol in unknownSymbols) {
                        AddError(AssemblyErrorCode.InvalidExpression, $"Invalid expression for {processedLine.Opcode.ToUpper()}: unknown symbol {symbol.Name}");
                    }

                    if(unknownSymbols.Any()) {
                        continue;
                    }

                    var linkItems = GetLinkItemsGroupFromExpression(processedLine, expressionPendingEvaluation);
                    if(linkItems != null) {
                        relocatables.Add(linkItems);
                    }
                }
                else {
                    if(expressionValue.IsAbsolute || expressionPendingEvaluation.ArgumentType == CpuInstrArgType.OffsetFromCurrentLocation) {
                        ProcessArgumentForInstruction(
                            expressionPendingEvaluation.ArgumentType,
                            line.OutputBytes, 
                            expressionValue, 
                            expressionPendingEvaluation.LocationInOutput,
                            expressionPendingEvaluation.IsNegativeIxy);

                    }
                    else {
                        relocatables.Add(new RelocatableAddress() { 
                            Index = expressionPendingEvaluation.LocationInOutput, 
                            IsByte = expressionPendingEvaluation.IsByte,
                            Type = expressionValue.Type, 
                            Value = expressionValue.Value
                        });
                    }
                }
            }

            line.RelocatableParts = relocatables.ToArray();
        }

        private static LinkItemsGroup GetLinkItemsGroupFromExpression(ProcessedSourceLine processedLine, ExpressionPendingEvaluation expressionPendingEvaluation)
        {
            var items = new List<LinkItem>();

            foreach(var part in expressionPendingEvaluation.Expression.Parts) {
                if(part is Address ad) {
                    items.Add(LinkItem.ForAddressReference(ad.Type, ad.Value));
                }
                else if(part is SymbolReference sr) {
                    var symbol = state.GetSymbolWithoutLocalNameReplacement(sr.SymbolName);
                    if(symbol is null) {
                        throw new InvalidOperationException($"{nameof(GetLinkItemsGroupFromExpression)}: {symbol} doesn't exist (this should have been catched earlier)");
                    }
                    if(symbol.IsExternal) {
                        items.Add(LinkItem.ForExternalReference(symbol.EffectiveName));
                    }
                    else {
                        items.Add(LinkItem.ForAddressReference(symbol.Value.Type, symbol.Value.Value));
                    }
                }
                else if(part is ArithmeticOperator op) {
                    if(op is TypeOperator) {
                        AddError(AssemblyErrorCode.InvalidExpression, $"Operator TYPE is not allowed in expressions involving external references (except when the external reference is the argument for TYPE)");
                        return null;
                    }
                    if(op is not UnaryPlusOperator) {
                        if(op.ExtendedLinkItemType is null) {
                            AddError(AssemblyErrorCode.InvalidForRelocatable, $"Operator {op} is not allowed in expressions involving external references");
                            return null;
                        }
                        items.Add(LinkItem.ForArithmeticOperator((ArithmeticOperatorCode)op.ExtendedLinkItemType));
                    }
                }
                else {
                    throw new InvalidOperationException($"{nameof(GetLinkItemsGroupFromExpression)}: unexpected expression part: {part}");
                }
            }

            return new LinkItemsGroup() { Index = expressionPendingEvaluation.LocationInOutput, IsByte = expressionPendingEvaluation.IsByte, LinkItems = items.ToArray() };
        }

        private static SymbolInfo GetSymbolForExpression(string name, bool isExternal, bool isRoot)
        {
            if(name == "$") {
                return new SymbolInfo() { Name = "$", Value = new Address(state.CurrentLocationArea, state.CurrentLocationPointer) };
            }

            if(!isRoot && !isExternal) {
                name = state.Modularize(name);
            }

            var symbol = state.GetSymbol(ref name);
            if(symbol is null) {
                state.AddSymbol(name, isExternal ? SymbolType.External : SymbolType.Unknown);
                symbol = state.GetSymbolWithoutLocalNameReplacement(name);

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
            if(label is null) {
                return;
            }

            var isPublic = label.EndsWith("::");
            var labelValue = isPublic ? label.TrimEnd(':') : state.Modularize(label.TrimEnd(':'));

            if(labelValue == "$") {
                AddError(AssemblyErrorCode.DollarAsLabel, "'$' defined as a label, but it actually represents the current location pointer");
            }

            var symbol = state.GetSymbol(ref labelValue);
            if(symbol == null) {
                if(isPublic && !externalSymbolRegex.IsMatch(labelValue)) {
                    AddError(AssemblyErrorCode.InvalidLabel, $"{labelValue} is not a valid public label name, it contains invalid characters");
                };
                if(z80RegisterNames.Contains(labelValue, StringComparer.OrdinalIgnoreCase)) {
                    AddError(AssemblyErrorCode.SymbolWithCpuRegisterName, $"{labelValue.ToUpper()} is a {currentCpu} register name, so it won't be possible to use it as a symbol in {currentCpu} instructions");
                }
                state.AddSymbol(labelValue, SymbolType.Label, state.GetCurrentLocation(), isPublic: isPublic);

                if(buildType == BuildType.Automatic) {
                    SetBuildType(BuildType.Relocatable);
                }
                else if(isPublic && buildType == BuildType.Absolute) {
                    AddError(AssemblyErrorCode.IgnoredForAbsoluteOutput, $"Label {labelValue.ToUpper()} is declared as public, but that has no effect when the output type is absolute");
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
                    if(state.InPass1) {
                        AddError(AssemblyErrorCode.DuplicatedSymbol, $"Duplicate label: {labelValue}");
                    }
                    else {
                        AddError(AssemblyErrorCode.DifferentPassValues, $"Label {labelValue} has different values in pass 1 ({symbol.Value:X4}h) and in pass 2 ({state.GetCurrentLocation().Value:X4}h)");
                    }
                }

                //Needed in case symbol is declared public with "::" only in pass 2
                if(!symbol.IsPublic) {
                    symbol.IsPublic = isPublic;
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

        private static bool IsValidSymbolName(string name) =>
            !string.IsNullOrWhiteSpace(name) && labelRegex.IsMatch(name) && !char.IsDigit(name[0]);

        private static void SetBuildType(BuildType type)
        {
            buildType = type;
            if(BuildTypeAutomaticallySelected is not null) {
                BuildTypeAutomaticallySelected(null, (state.CurrentIncludeFilename, state.CurrentLineNumber, buildType));
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

        static void AddError(AssemblyErrorCode code, string message, bool withLineNumber = true)
        {
            var error = state.AddError(code, message, withLineNumber);
            if(AssemblyErrorGenerated is not null) {
                AssemblyErrorGenerated(null, error);
            }

            if(!error.IsWarning && maxErrors != 0) {
                errorsGenerated++;
                if(errorsGenerated >= maxErrors) {
                    message = errorsGenerated == 1 ?
                        "Assembly stopped after first error" :
                        $"Assembly stopped after reaching {errorsGenerated} errors";
                    ThrowFatal(AssemblyErrorCode.MaxErrorsReached, message);
                }
            }
        }

        static void ThrowFatal(AssemblyErrorCode errorCode, string message)
        {
            throw new FatalErrorException(new AssemblyError(errorCode, message, state.CurrentLineNumber, state.CurrentSourceLineText, state.CurrentIncludeFilename));
        }

        static Address EvaluateIfNoSymbolsOrPass2(Expression expression) =>
            state.InPass2 ? expression.Evaluate() : expression.EvaluateIfNoSymbols();
    }
}
