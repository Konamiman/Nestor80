using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Konamiman.Nestor80.Assembler.Output;

[assembly: InternalsVisibleTo("AssemblerTests")]

namespace Konamiman.Nestor80.Assembler
{
    public partial class AssemblySourceProcessor
    {
        const int MAX_LINE_LENGTH = 1034;

        private AssemblyConfiguration configuration;

        private static AssemblyState state;

        private Encoding sourceStreamEncoding;

        private Stream sourceStream;

        private static readonly Regex labelRegex = new("^[\\w$@?._][\\w$@?._0-9]*:{0,2}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex externalSymbolRegex = new("^[a-zA-Z_$@?.][a-zA-Z_$@?.0-9]*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ProgramNameRegex = new(@"^\('(?<name>[a-zA-Z_$@?.][a-zA-Z_$@?.0-9]*)'\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex LegacySubtitleRegex = new(@"^\('(?<name>[^']*)'\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        //Constant definitions are considered pseudo-ops, but they are handled as a special case
        //(instead of being included in PseudoOpProcessors) because the actual opcode comes after the name of the constant
        private static readonly string[] constantDefinitionOpcodes = { "EQU", "DEFL", "SET", "ASET" };

        private static readonly ProcessedSourceLine blankLineWithoutLabel = new BlankLine();

        private AssemblySourceProcessor()
        {
        }

        static AssemblySourceProcessor()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
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
            this.configuration = configuration;
            this.sourceStream = sourceStream;
            this.sourceStreamEncoding = sourceStreamEncoding;
            state = new AssemblyState() { Configuration = configuration };

            try {
                state = new AssemblyState { 
                    Configuration = configuration
                };

                var validInitialStringEncoding = SetStringEncoding(configuration.OutputStringEncoding, initial: true);
                if(!validInitialStringEncoding)
                    Expression.OutputStringEncoding = Encoding.ASCII;

                state.DefaultOutputStringEncoding = Expression.OutputStringEncoding;

                Expression.GetSymbol = GetSymbolForExpression;
                Expression.AllowEscapesInStrings = configuration.AllowEscapesInStrings;

                DoPass1();
                if(!state.HasErrors) {
                    state.SwitchToPass2();
                    DoPass2();
                }
            }
            catch(FatalErrorException ex) {
                state.AddError(ex.Error);
            }
            catch(Exception ex) {
                state.AddError(
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

            return new AssemblyResult() {
                ProgramName = state.ProgramName,
                ProgramAreaSize = state.GetAreaSize(AddressType.CSEG),
                DataAreaSize = state.GetAreaSize(AddressType.DSEG),
                CommonAreaSizes = new(), //TODO: Handle commons
                ProcessedLines = state.ProcessedLines.ToArray(),
                Symbols = symbols,
                Errors = state.GetErrors(),
                EndAddressArea = state.EndAddress is null ? AddressType.ASEG : state.EndAddress.Type,
                EndAddress = (ushort)(state.EndAddress is null ? 0 : state.EndAddress.Value),
            };
        }

        private void DoPass2()
        {
            //throw new NotImplementedException();
        }

        private void DoPass1()
        {
            var sourceStream = new StreamReader(this.sourceStream, this.sourceStreamEncoding, true, 4096);

            int lineLength;

            while(!state.EndReached) {
                var sourceLine = sourceStream.ReadLine();
                if(sourceLine == null) break;
                if((lineLength = sourceLine.Length) > MAX_LINE_LENGTH) {
                    sourceLine = sourceLine[..MAX_LINE_LENGTH];
                    state.AddError(
                        AssemblyErrorCode.SourceLineTooLong,
                        $"Line is too long ({lineLength} bytes), actual line processed: {sourceLine.Trim()}"
                    );
                }

                ProcessSourceLine(sourceLine);
                state.IncreaseLineNumber();
            }

            if(!state.EndReached) {
                state.AddError(AssemblyErrorCode.NoEndStatement, "No END statement found");
            }

            if(state.InsideMultiLineComment) {
                state.AddError(AssemblyErrorCode.UnterminatedComment, $"Unterminated .COMMENT block (delimiter: '{state.MultiLineCommandDelimiter}')");
            }
        }

        private void ProcessSourceLine(string line)
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
                state.ProcessedLines.Add(processedLine);
                return;
            }

            if(string.IsNullOrWhiteSpace(line)) {
                state.ProcessedLines.Add(
                    formFeedCharsCount == 0 ?
                    blankLineWithoutLabel :
                    new BlankLine() { FormFeedsCount = formFeedCharsCount });
                return;
            }

            walker = new SourceLineWalker(line);
            if(walker.AtEndOfLine) {
                state.ProcessedLines.Add(new CommentLine() { Line = line, EffectiveLineLength = walker.EffectiveLength, FormFeedsCount = formFeedCharsCount });
                return;
            }

            string label = null;
            string opcode = null;
            var symbol = walker.ExtractSymbol();

            if(symbol.EndsWith(':')) {
                if(labelRegex.IsMatch(symbol)) {
                    label = symbol;
                    ProcessLabelDefinition(label);
                }
                else {
                    state.AddError(AssemblyErrorCode.InvalidLabel, $"Invalid label (contains illegal characters): {symbol}");
                }

                if(walker.AtEndOfLine) {
                    if(walker.EffectiveLength == walker.SourceLine.Length) {
                        state.ProcessedLines.Add(new BlankLine() { Label = label});
                    }
                    else {
                        state.ProcessedLines.Add(new CommentLine() { Line = walker.SourceLine, EffectiveLineLength = walker.EffectiveLength, Label = label });
                    }
                    return;
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
            if(label is null && !walker.AtEndOfLine) {
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

            if(processedLine is null) {
                if(PseudoOpProcessors.ContainsKey(symbol)) {
                    opcode = symbol;
                    var processor = PseudoOpProcessors[opcode];
                    processedLine = processor(opcode, walker);
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
                state.AddError(AssemblyErrorCode.UnexpectedContentAtEndOfLine, $"Unexpected content found at the end of the line: {walker.GetRemaining()}");
            }

            if(opcode is not null) {
                processedLine.Opcode = opcode;
            }

            processedLine.Line = line;
            if(processedLine.EffectiveLineLength == -1) processedLine.EffectiveLineLength = walker.DiscardRemaining();
            processedLine.Label = label;
            processedLine.FormFeedsCount = formFeedCharsCount;
            state.ProcessedLines.Add(processedLine);
        }

        internal static SymbolInfo GetSymbolForExpression(string name, bool isExternal)
        {
            if(name == "$")
                return new SymbolInfo() { Name = "$", Value = new Address(state.CurrentLocationArea, state.CurrentLocationPointer) };

            var symbol = state.GetSymbol(name);
            if(symbol is null) {
                state.AddSymbol(name, isExternal ? SymbolType.External : SymbolType.Unknown);
                symbol = state.GetSymbol(name);
            }

            return symbol;
        }

        private static void ProcessLabelDefinition(string label)
        {
            var isPublic = label.EndsWith("::");
            var labelValue = label.TrimEnd(':');

            if(labelValue == "$") {
                state.AddError(AssemblyErrorCode.DollarAsLabel, "'$' defined as a label, but it actually represents the current location pointer");
            }

            //TODO: Warn if register used as label (e.g.: if B is a label, LD A,B loads the label and not reg B)

            var symbol = state.GetSymbol(labelValue);
            if(symbol == null) {
                if(isPublic && !externalSymbolRegex.IsMatch(labelValue)) {
                    state.AddError(AssemblyErrorCode.InvalidLabel, $"{labelValue} is not a valid public label name, it contains invalid characters");
                };
                state.AddSymbol(labelValue, SymbolType.Label, state.GetCurrentLocation(), isPublic: isPublic);
            }
            else if(symbol.IsExternal) {
                state.AddError(AssemblyErrorCode.DuplicatedSymbol, $"Symbol has been declared already as external: {labelValue}");
            }
            else if(symbol.IsConstant) {
                state.AddError(AssemblyErrorCode.DuplicatedSymbol, $"Symbol has been declared already with {symbol.Type.ToString().ToUpper()}: {labelValue}");
            }
            else if(symbol.HasKnownValue) {
                if(symbol.Value != state.GetCurrentLocation()) {
                    state.AddError(AssemblyErrorCode.DuplicatedSymbol, $"Duplicate label: {labelValue}");
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
                state.AddError(
                    AssemblyErrorCode.UnknownStringEncoding,
                    isCodePage ?
                        $"There's no known string encoding with the code page {codePage}" :
                        $"There's no known string encoding with the name '{encodingNameOrCodepage}'",
                    withLineNumber: !initial
                );
                return false;
            }
        }
    }
}
