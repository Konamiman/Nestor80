using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Konamiman.Nestor80.Assembler.Errors;
using Konamiman.Nestor80.Assembler.Expressions;
using Konamiman.Nestor80.Assembler.Expressions.ExpressionParts;
using Konamiman.Nestor80.Assembler.Expressions.ExpressionParts.ArithmeticOperators;
using Konamiman.Nestor80.Assembler.Infrastructure;
using Konamiman.Nestor80.Assembler.Output;
using Konamiman.Nestor80.Assembler.Relocatable;

[assembly: InternalsVisibleTo("AssemblerTests")]

namespace Konamiman.Nestor80.Assembler
{
    /// <summary>
    /// This class provides an <see cref="AssemblySourceProcessor.Assemble(Stream, Encoding, AssemblyConfiguration)"/> method that processes
    /// a unit of source code (coming from any stream) and returns an <see cref="AssemblyResult"/> object that can be used to
    /// generate an output file with <see cref="OutputGenerator"/> and a listing file with <see cref="ListingFileGenerator"/>.
    /// A handful of events are provided to monitor the progress of the assembly process.
    /// </summary>
    public static partial class AssemblySourceProcessor
    {
        const int MAX_LINE_LENGTH = 1034;
        const int MAX_INCLUDE_NESTING = 34;

        const string Z280_ALLOW_PRIV_SYMBOL = "z280.allowprivileged";
        const string Z280_ALLOW_IO_SYMBOL = "z280.allowio";
        const string Z280_INDEX_MODE_SYMBOL = "z280.indexmode";

        const RegexOptions RegxOp = RegexOptions.Compiled | RegexOptions.IgnoreCase;

        public const int MaxEffectiveExternalNameLength = 6;

        public const int MaxEffectiveRequestFilenameLength = 7;

        private static AssemblyState state;

        private static BuildType buildType;

        private static Stream includeStream;

        private static int maxErrors = 0;
        private static int errorsGenerated = 0;
        private static string programName = null;
        private static bool programNameInstructionFound = false;
        private static bool link80Compatibility;

        private static readonly string[] z80RegisterNames = new[] {
            "A", "B", "C", "D", "E", "F", "H", "L", "I", "R",
            "AF", "HL", "BC", "DE", "IX", "IY",
            "SP", "IXH", "IXL", "IYH", "IYL",
            "NC", "Z", "NZ", "P", "M", "PE", "PO"
        };

        private static readonly string[] conditionalInstructions = new[] {
            "IF", "IFT", "IFE", "IFF",
            "IFDEF", "IFNDEF", "IF1", "IF2",
            "IFB", "IFNB", "IFIDN", "IFDIF", "IFIDNI", "IFDIFI",
            "IFABS", "IFREL", "ELSE", "ENDIF",
            "IFCPU", "IFNCPU"
        };

        private static readonly string[] macroDefinitionOrExpansionInstructions = new[] {
            "MACRO", "REPT", "IRP", "IRPC", "IRPS"
        };

        private static readonly string[] macroDefinitionOrExpansionInstructionsMinusNamed = new[] {
            "REPT", "IRP", "IRPC", "IRPS"
        };

        private static readonly string[] instructionsThatAcceptFreeText = new[] {
            "TITLE", "SUBTTL", ".COMMENT", ".PRINTX", ".PRINT", ".PRINT1", ".PRINT2"
        };

        private static readonly string[] includeInstructions = new[] {
            "INCLUDE", "$INCLUDE", "MACLIB"
        };

        private static readonly string[] instructionsNeedingPass2Reevaluation;

        private static CpuType currentCpu;
        private static bool z280AllowPriviliegedInstructions;
        private static bool z280AllowIoInstructions;
        private static Z280IndexMode z280IndexMode;

        private static readonly Regex labelRegex = new("^([^\\d\\W]|[$@?._])[\\w$@?._]*:{0,2}$", RegxOp);

        private static Regex externalSymbolRegex;
        private static readonly Regex externalSymbolRegex_full = new("^([^\\d\\W]|[$@?._])[\\w$@?._]*$", RegxOp);
        private static readonly Regex externalSymbolRegex_link80 = new("^[A-Z_$@?.][A-Z_$@?.0-9]*$", RegxOp);

        private static Regex programNameRegex;
        private static readonly Regex programNameRegex_full = new(@"^\('(?<name>([^\d\W]|[$@?._])[\w$@?._]*)'\)", RegxOp);
        private static readonly Regex programNameRegex_link80 = new(@"^\('(?<name>[A-Z_$@?.][A-Z_$@?.0-9]*)'\)", RegxOp);

        private static readonly Regex LegacySubtitleRegex = new(@"^\('(?<name>[^']*)'\)", RegxOp);
        private static readonly Regex printStringExpressionRegex = new(@"(?<=\{)[^}]*(?=\})", RegxOp);
        private static readonly Regex commonBlockNameRegex = new(@"^/[ \t]*/|/[A-Z$@?._][A-Z$@?._0-9]*/$", RegxOp);
        
        //Constant definitions are considered pseudo-ops, but they are handled as a special case
        //(instead of being included in PseudoOpProcessors) because the actual opcode comes after the name of the constant
        private static readonly string[] constantDefinitionOpcodes = { "EQU", "DEFL", "ASET" };

        private static readonly ProcessedSourceLine blankLineWithoutLabel = new BlankLine();

        /// <summary>
        /// Event fired when the assembly process generates an assembly error.
        /// </summary>
        public static event EventHandler<AssemblyError> AssemblyErrorGenerated;

        /// <summary>
        /// Event fired when the .PRINTX, .PRINT, .PRINT1 or .PRINT2 instructions are processed,
        /// the argument is the text to print.
        /// </summary>
        public static event EventHandler<string> PrintMessage;

        /// <summary>
        /// Event fired when the build type is automatically selected (only if build type passed in configuration was "auto").
        /// The arguments are the file name and line number where the decision was made, and the build type chosen.
        /// </summary>
        public static event EventHandler<(string, int, BuildType)> BuildTypeAutomaticallySelected;

        /// <summary>
        /// Event fired when pass 2 starts.
        /// </summary>
        public static event EventHandler Pass2Started;

        /// <summary>
        /// Event fired when the processing of an INCLUDEd file finishes
        /// (so processing resumes at the point after the INCLUDE instruction
        /// in the previous INCLUDEd file or in the input file).
        /// </summary>
        public static event EventHandler IncludedFileFinished;

        //This runs the first time that any static member of the class is accessed.
        static AssemblySourceProcessor()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            instructionsNeedingPass2Reevaluation = conditionalInstructions.Concat(new[] { ".PHASE", ".DEPHASE" }).ToArray();
        }

        public static bool IsValidCpu(string cpuName)
        {
            return Enum.GetNames<CpuType>().Contains(cpuName, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Process (assemble) a unit of source code.
        /// </summary>
        /// <param name="source">The source code to process.</param>
        /// <param name="configuration">The configuration to use for the process.</param>
        /// <returns>The result of the processing.</returns>
        public static AssemblyResult Assemble(string source, AssemblyConfiguration configuration = null)
        {
            var sourceStream = new MemoryStream(Encoding.UTF8.GetBytes(source));
            return Assemble(sourceStream, Encoding.UTF8, configuration ?? new AssemblyConfiguration());
        }

        /// <summary>
        /// Process (assemble) a unit of source code.
        /// </summary>
        /// <param name="sourceStream">The source code to process.</param>
        /// <param name="configuration">The configuration to use for the process.</param>
        /// <returns>The result of the processing.</returns>
        public static AssemblyResult Assemble(Stream sourceStream, Encoding sourceStreamEncoding, AssemblyConfiguration configuration)
        {
            try {
                includeStream = null;
                state = new AssemblyState(configuration, sourceStream, sourceStreamEncoding);

                z280AllowPriviliegedInstructions = true;
                z280AllowIoInstructions = true;
                z280IndexMode = Z280IndexMode.Auto;
                ProcessPredefinedsymbols(configuration.PredefinedSymbols);
                maxErrors = configuration.MaxErrors;
                link80Compatibility = configuration.Link80Compatibility;

                SetCurrentCpu(configuration.CpuName);
                buildType = configuration.BuildType;
                state.SwitchToArea(buildType != BuildType.Absolute ? AddressType.CSEG : AddressType.ASEG);

                var validInitialStringEncoding = SetStringEncoding(configuration.OutputStringEncoding, initial: true);
                if(!validInitialStringEncoding)
                    Expression.OutputStringEncoding = Encoding.ASCII;

                state.DefaultOutputStringEncoding = Expression.OutputStringEncoding;

                Expression.GetSymbol = GetSymbolForExpression;
                Expression.ModularizeSymbolName = name => state.Modularize(name);
                Expression.AllowEscapesInStrings = configuration.AllowEscapesInStrings;
                Expression.Link80Compatibility = link80Compatibility;
                SymbolInfo.Link80Compatibility = link80Compatibility;
                LinkItem.Link80Compatibility = link80Compatibility;

                externalSymbolRegex = link80Compatibility ? externalSymbolRegex_link80 : externalSymbolRegex_full;
                programNameRegex = link80Compatibility ? programNameRegex_link80 : programNameRegex_full;

                incbinBuffer = new byte[configuration.MaxIncbinFileSize];

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

                #if DEBUG
                throw;
                #endif
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

            var duplicateExternals = symbols.Where(s => s.Type is SymbolType.External).GroupBy(s => s.EffectiveName).Where(s => s.Count() > 1).ToArray();
            foreach(var dupe in duplicateExternals) {
                var names = string.Join(", ", dupe.Select(s => s.Name).ToArray());
                AddError(AssemblyErrorCode.SameEffectiveExternal, $"External symbols {names} actually refer to the same one: {dupe.First().EffectiveName}", withLineNumber: false);
            }

            var duplicatePublics = symbols.Where(s => s.IsPublic).GroupBy(s => s.EffectiveName).Where(s => s.Count() > 1).ToArray();
            foreach(var dupe in duplicatePublics) {
                var names = string.Join(", ", dupe.Select(s => s.Name).ToArray());
                AddError(AssemblyErrorCode.SameEffectivePublic, $"Public symbols {names} actually refer to the same one: {dupe.First().EffectiveName}", withLineNumber: false);
            }

            if(configuration.Link80Compatibility) {
                var duplicateCommonBlocks = state
                    .GetCommonBlockSizes()
                    .Where(x => x.Key.Length > MaxEffectiveExternalNameLength)
                    .GroupBy(n => n.Key[..MaxEffectiveExternalNameLength])
                    .Where(n => n.Count() > 1)
                    .ToArray();
                foreach(var dupe in duplicateCommonBlocks) {
                    var names = string.Join(", ", dupe.Select(d => d.Key).ToArray());
                    AddError(AssemblyErrorCode.SameEffectiveCommon, $"Common block names {names} actually refer to the same one: {dupe.First().Key[..MaxEffectiveExternalNameLength]}", withLineNumber: false);
                }
            }

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
                CommonAreaSizes = state.GetCommonBlockSizes(),
                ProcessedLines = state.ProcessedLines.ToArray(),
                Symbols = symbols,
                Errors = state.GetErrors(),
                EndAddressArea = state.EndAddress is null ? AddressType.ASEG : state.EndAddress.Type,
                EndAddress = (ushort)(state.EndAddress is null ? 0 : state.EndAddress.Value),
                BuildType = buildType,
                MaxRelocatableSymbolLength = configuration.Link80Compatibility ? MaxEffectiveExternalNameLength : int.MaxValue,
                MacroNames = state.NamedMacros.Keys.ToArray()
            };
        }

        /// <summary>
        /// Process any symbols that were passed with the -ds or --define-symbols arguments.
        /// </summary>
        /// <param name="predefinedSymbols">A list of symbol name and value pairs.</param>
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

                MaybeProcessSpecialSymbol(symbol, Address.Absolute(value).Value);
            }
        }

        /// <summary>
        /// Given a symbol that is being (re)defined as a constant, check if it's a special symbol
        /// and update internal state accordingly if so.
        /// </summary>
        /// <param name="symbol">Symbol name</param>
        /// <param name="value">Symbol value</param>
        private static void MaybeProcessSpecialSymbol(string symbol, ushort value)
        {
            if(symbol.Equals(Z280_ALLOW_PRIV_SYMBOL, StringComparison.OrdinalIgnoreCase)) {
                z280AllowPriviliegedInstructions = value != 0;
            }
            else if(symbol.Equals(Z280_ALLOW_IO_SYMBOL, StringComparison.OrdinalIgnoreCase)) {
                z280AllowIoInstructions = value != 0;
            }
            else if(symbol.Equals(Z280_INDEX_MODE_SYMBOL, StringComparison.OrdinalIgnoreCase)) {
                if(value == 1) {
                    z280IndexMode = Z280IndexMode.Short;
                }
                else if(value == 2) {
                    z280IndexMode = Z280IndexMode.Long;
                }
                else {
                    z280IndexMode = Z280IndexMode.Auto;
                }
            }
        }

        /// <summary>
        /// Perform an assembly pass, basically by processing source lines one by one (<see cref="ProcessSourceLine(string, int?)"/> does that)
        /// and keeping track of macro expansion and INCLUDEd files.
        /// </summary>
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

        /// <summary>
        /// Process one single line of source code and return an instance of <see cref="ProcessedSourceLine"/>
        /// that represents the line and can be used when generating the output file or the listing file
        /// (each and every source line generates an instance of <see cref="ProcessedSourceLine"/>, no exceptions, even blank lines
        /// and lines inside false conditionals).
        /// </summary>
        /// <param name="line">The source line to process, with form feed chars removed.</param>
        /// <param name="formFeedCharsCount">How many form feed chars (FFh) were present in the original source line.</param>
        /// <returns>The result of processing the line.</returns>
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
                if(line.Contains(state.MultiLineCommandDelimiter.Value)) {
                    processedLine = new DelimitedCommentLine() { IsLastLine = true, Delimiter = state.MultiLineCommandDelimiter };
                    state.MultiLineCommandDelimiter = null;
                }
                else {
                    processedLine = new DelimitedCommentLine();
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
            var symbol = walker.ExtractSymbol(colonIsDelimiter: true);

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
            if(!definingMacro && symbol.EndsWith(':') && !(state.Configuration.AllowBareExpressions && symbol[0] is '"' or '\'')) {
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

            // Normally, when we are inside a false conditional we still need to process IF and ENDIF
            // statements so as to properly keep the conditional block "state machine".
            // However, if such statements are inside a macro definition these must be skipped. Example:
            //
            // if 0
            // foo macro bar
            // if something eq fizz&bar   --> This must NOT be processed! (and would yield an "invalid symbol fizz&bar" anyway)
            // ;some code
            // endif  --> This must NOT be processed!
            // endm
            // endif  --> This must be processed as the end of the "if 0"
            //
            // The logic is a bit convoluted because we need to take in account REPTs inside the named macro too
            // (we keep a count of "rept inside named macro inside alse conditional nesting level").
            // There are most likely even more complicated cases that aren't properly handled.
            if(state.InFalseConditional) {
                if(symbol.Equals("ENDM", StringComparison.OrdinalIgnoreCase)) {
                    if(state.IrpInsideNamedMacroInsideFalseConditionalNestingLevel > 0) {
                        state.IrpInsideNamedMacroInsideFalseConditionalNestingLevel--;
                    }
                    else {
                        state.InsideNamedMacroInsideFalseConditional = false;
                    }
                }
                else if(state.InsideNamedMacroInsideFalseConditional) {
                    if(macroDefinitionOrExpansionInstructionsMinusNamed.Contains(symbol, StringComparer.OrdinalIgnoreCase)) {
                        state.IrpInsideNamedMacroInsideFalseConditionalNestingLevel++;
                    }
                }
                else if(symbol.Equals("MACRO", StringComparison.OrdinalIgnoreCase)) {
                    state.InsideNamedMacroInsideFalseConditional = true;
                }
                else if(label is null && !walker.AtEndOfLine) {
                    walker.BackupPointer();
                    var instruction = walker.ExtractSymbol();
                    walker.RestorePointer();
                    if(instruction.Equals("MACRO", StringComparison.OrdinalIgnoreCase)) {
                        state.InsideNamedMacroInsideFalseConditional = true;
                    }
                }
            }

            // Constant definition check must go before any other opcode check,
            // since pseudo-ops and cpu instructions are valid constant names too
            // (but only if no label is defined in the line)
            //
            // Interesting edge case (compatible with MACRO-80):
            //
            // TITLE EQU 1      ---> defines constant "TITLE" with value 1
            // FOO: TITLE EQU 1 ---> sets the program title as "EQU 1"
            if(!definingMacro && !state.InFalseConditional && !walker.AtEndOfLine) {
                if(label is not null && constantDefinitionOpcodes.Contains(symbol, StringComparer.OrdinalIgnoreCase)) {
                    opcode = symbol;
                    processedLine = ProcessConstantDefinition(opcode: opcode, name: label.TrimEnd(':'), walker: walker);
                    label = null;
                }
                else if(label is not null && symbol.Equals("MACRO", StringComparison.OrdinalIgnoreCase)) {
                    opcode = symbol;
                    processedLine = ProcessNamedMacroDefinitionLine(name: label.TrimEnd(':'), walker: walker);
                }
                else if(label is null && !instructionsThatAcceptFreeText.Contains(symbol, StringComparer.OrdinalIgnoreCase)) {
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
                if(conditionalInstructions.Contains(symbol, StringComparer.OrdinalIgnoreCase) && !state.InsideNamedMacroInsideFalseConditional) {
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
                else if(includeInstructions.Contains(symbol, StringComparer.OrdinalIgnoreCase)) {
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
                else if(state.NamedMacroExists(symbol)) {
                    opcode = "MACROEX";
                    processedLine = ProcessNamedMacroExpansion(opcode, symbol, walker);
                }
                else if(state.Configuration.AllowBareExpressions) {
                    opcode = "RAW DB";
                    processedLine = ProcessDefbLine(opcode, new SourceLineWalker(symbol + " " + walker.GetRemaining()));
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
                else if(processedLine is ChangeAreaLine cal) {
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

        /// <summary>
        /// Once the assembly process has finished, throws the appropriate "global" (not related to a particular source line) warnings.
        /// </summary>
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

        /// <summary>
        /// Process the instructions that were left as "pending evaluation" when the corresponding source line was processed.
        /// An expression will be considered as pending evaluation during pass 1 when referencing symbols, this method will be used
        /// in pass 2 to resolve them. Expressions referencing relocatable values that need to be stored as a byte need special
        /// processing and thus will also be marked as pending evaluation and processed by this method.
        /// </summary>
        /// <param name="processedLine">The processed line.</param>
        /// <param name="expressionsPendingEvaluation">The expressions to process.</param>
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
                var hasExternalReferences = false;
                Address expressionValue = null;

                if(expressionPendingEvaluation.Expression.HasTypeOperator) {
                    throw new Exception($"Expression '{expressionPendingEvaluation.Expression.Source}' has unresolved TYPE operators, that should never happen");
                }

                if(referencedSymbols.Any(s => s.IsExternal)) {
                    if(expressionPendingEvaluation.ArgumentType is CpuInstrArgType.OffsetFromCurrentLocation) {
                        AddError(AssemblyErrorCode.InvalidExpression, $"Invalid expression for {processedLine.Opcode.ToUpper()}: the expression can't contain external references");
                        continue;
                    }
                    hasExternalReferences = true;
                }
                else {
                    try {
                        expressionValue = expressionPendingEvaluation.Expression.Evaluate();
                    }
                    catch(ExpressionReferencesExternalsException) {
                        hasExternalReferences = true;
                    }
                    catch(InvalidExpressionException ex) {
                        AddError(ex.ErrorCode, $"Invalid expression for {processedLine.Opcode.ToUpper()}: {ex.Message}");
                        continue;
                    }
                }

                if(hasExternalReferences) {
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
                else if(expressionPendingEvaluation.Expression.HasRelocatableToStoreAsByte) {
                    var linkItems = GetLinkItemsGroupFromExpression(processedLine, expressionPendingEvaluation);
                    if(linkItems != null) {
                        relocatables.Add(linkItems);
                    }
                }
                else {
                    if(expressionValue.IsAbsolute || expressionPendingEvaluation.ArgumentType == CpuInstrArgType.OffsetFromCurrentLocation) {
                        ProcessArgumentForInstruction(
                            processedLine.Opcode,
                            expressionPendingEvaluation.ArgumentType,
                            line.OutputBytes, 
                            expressionValue, 
                            expressionPendingEvaluation.LocationInOutput,
                            expressionPendingEvaluation.IsNegativeIxy);

                    }
                    else {
                        relocatables.Add(new RelocatableValue() { 
                            Index = expressionPendingEvaluation.LocationInOutput, 
                            IsByte = expressionPendingEvaluation.IsByte,
                            Type = expressionValue.Type, 
                            Value = expressionValue.Value,
                            CommonName = expressionValue.CommonBlockName
                        });
                    }
                }
            }

            line.RelocatableParts = relocatables.ToArray();
        }

        /// <summary>
        /// Converts an expression into a collection of objects representing link items that can be later used
        /// when generating a LINK-80 compatible relocatable file. This processing is needed when the expression
        /// contains external symbol references or relocatable values that are to be stored as a byte
        /// (these expressions can't be evaluated at assembly time and need to be evaluated at linking time).
        /// </summary>
        /// <param name="processedLine">The processed line that contains the expression.</param>
        /// <param name="expressionPendingEvaluation">The expression to process.</param>
        /// <returns>A <see cref="LinkItemsGroup"/> object containing the resulting link items.</returns>
        /// <exception cref="InvalidOperationException"></exception>
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
                    if(op is not UnaryPlusOperator) {
                        if(link80Compatibility && op.ExtendedLinkItemType > ArithmeticOperator.SmallestExtendedOperatorCode) {
                            AddError(AssemblyErrorCode.InvalidForRelocatable, $"Operator {op} is not allowed in expressions involving external references in LINK-80 compatibility mode");
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

        /// <summary>
        /// Get a symbol to be used in the evaluation of a expression, taking in account
        /// if the symbol already exists or not, if it's external, and if a prefix needs to be added
        /// (for modules and relative labels).
        /// </summary>
        /// <param name="name">Symbol name.</param>
        /// <param name="isExternal">True if the symbol is referenced as external with a ## suffix.</param>
        /// <param name="isRoot">True if a symbol is referenced as root with a : prefix.</param>
        /// <returns>The retrieved (or newly created) symbol object.</returns>
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
            else if(isExternal && !symbol.IsExternal) {
                if(symbol.HasKnownValue) {
                    AddError(AssemblyErrorCode.DuplicatedSymbol, $"{name.ToUpper()} is already defined, can't be declared as an external symbol");
                }
                else {
                    symbol.Type = SymbolType.External;
                }
            }

            return symbol;
        }

        /// <summary>
        /// Process a label definition found while processing a source line.
        /// This is more complex than just creating a new symbol, since we must check if a symbol with the same name already exists
        /// (and if so, is it actually the same label? Does it have the same value in passes 1 and 2? Is it now referred as external
        /// but it wasn't previously? Etc).
        /// </summary>
        /// <param name="label">The label as found in the source, with : or :: sufffix.</param>
        private static void ProcessLabelDefinition(string label)
        {
            if(label is null || (state.Configuration.AllowBareExpressions && label[0] is '"' or '\'')) {
                return;
            }

            var isPublic = label.EndsWith("::");
            var labelValue = isPublic ? label.TrimEnd(':') : state.Modularize(label.TrimEnd(':'));

            if(labelValue == "$") {
                AddError(AssemblyErrorCode.DollarAsLabel, "'$' defined as a label, but it actually represents the current location pointer");
            }

            var symbol = state.GetSymbol(ref labelValue);

            if(symbol?.IsNonRelativeLabel == true) {
                state.RegisterLastNonRelativeLabel(symbol.Name);
            }

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

                //In case the label first appeared as part of an expression
                if(!symbol.IsNonRelativeLabel) {
                    symbol.IsNonRelativeLabel = state.IsValidNonRelativeLabelName(symbol.Name);
                    if(symbol.IsNonRelativeLabel) {
                        state.RegisterLastNonRelativeLabel(symbol.Name);
                    }
                }
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

        /// <summary>
        /// Set the string encoding to use for converting strings provided in DEFB instructions into sequences of bytes.
        /// </summary>
        /// <param name="encodingNameOrCodepage">The encoding name or code page.</param>
        /// <param name="initial">True if the encoding is being selected initially via configuration, false if it's being selected in code with the .strenc instruction.</param>
        /// <returns>True on success, false on error (unknown or unsupported encoding).</returns>
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

        static ushort oldPointer, oldPhasedPointer;
        private static void IncreaseLocationPointer(int amount)
        {
            oldPointer = state.CurrentDephasedLocationPointer;
            if(state.IsCurrentlyPhased) {
                oldPhasedPointer = state.CurrentPhasedLocationPointer.Value;
            }

            state.IncreaseLocationPointer(amount);

            if(state.CurrentDephasedLocationPointer < oldPointer) {
                AddError(AssemblyErrorCode.LocationPointerOverflow, $"{(state.IsCurrentlyPhased ? "Main (not PHASEd) l" : "L")}ocation pointer overflowed beyond FFFFh and went back to 0");
            }

            if(state.IsCurrentlyPhased && state.CurrentPhasedLocationPointer < oldPhasedPointer) {
                AddError(AssemblyErrorCode.LocationPointerOverflow, $"PHASEd location pointer overflowed beyond FFFFh and went back to 0");
            }
        }

        /// <summary>
        /// Register a new assembly error. Most of the work is done by <see cref="AssemblyState.AddError(AssemblyErrorCode, string, bool)"/>,
        /// but here we fire the appropriate event and check if we have reached the maximum errors count.
        /// </summary>
        /// <param name="code"></param>
        /// <param name="message"></param>
        /// <param name="withLineNumber"></param>
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
