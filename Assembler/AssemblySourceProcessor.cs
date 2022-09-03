using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Konamiman.Nestor80.Assembler.Output;

[assembly: InternalsVisibleTo("AssemblerTests")]

namespace Konamiman.Nestor80.Assembler
{
    public partial class AssemblySourceProcessor
    {
        private AssemblyConfiguration configuration;

        private static AssemblyState state;

        private Encoding sourceStreamEncoding;

        private Stream sourceStream;

        private static readonly Regex labelRegex = new("^[\\w$@?.]+:{0,2}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private AssemblySourceProcessor()
        {
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

                Expression.OutputStringEncoding = configuration.OutputStringEncoding;
                Expression.GetSymbol = state.GetSymbolForExpression;

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

            return new AssemblyResult() {
                ProgramName = state.ProgramName,
                ProgramAreaSize = state.GetAreaSize(AddressType.CSEG),
                DataAreaSize = state.GetAreaSize(AddressType.DSEG),
                CommonAreaSizes = new(), //TODO: Handle commons
                ProcessedLines = state.ProcessedLines.ToArray(),
                Symbols = state.GetSymbols(),
                Errors = state.GetErrors(),
            };
        }

        private void DoPass2()
        {
            throw new NotImplementedException();
        }

        private void DoPass1()
        {
            state.AddSymbol("BAR", Address.Code(0x34));

            var sourceStream = new StreamReader(this.sourceStream, this.sourceStreamEncoding, true, 4096);

            int lineLength;

            while(true) {
                var sourceLine = sourceStream.ReadLine();
                if(sourceLine == null) break;
                if(configuration.MaxLineLength is not null && (lineLength = sourceLine.Length) > configuration.MaxLineLength) {
                    sourceLine = sourceLine[..configuration.MaxLineLength.Value];
                    state.AddError(
                        AssemblyErrorCode.SourceLineTooLong,
                        $"Line is too long ({lineLength} bytes), actual line processed: {sourceLine.Trim()}"
                    );
                }

                ProcessSourceLine(sourceLine);
                state.IncreaseLineNumber();
            }
        }

        private void ProcessSourceLine(string line)
        {
            if(string.IsNullOrWhiteSpace(line)) {
                state.ProcessedLines.Add(BlankLineWithoutLabel.Instance);
                return;
            }

            var walker = new SourceLineWalker(line);
            if(walker.AtEndOfLine) {
                state.ProcessedLines.Add(new CommentLine(line, walker.EffectiveLength));
                return;
            }

            ProcessedSourceLine processedLine = null;
            string label = null;

            var op = walker.ExtractSymbol();
            if(op.EndsWith(':')) {
                if(labelRegex.IsMatch(op)) {
                    label = op;
                    ProcessLabel(label);
                }
                else {
                    state.AddError(AssemblyErrorCode.InvalidLabel, $"Invalid label (contains illegal characters): {op}");
                }

                if(walker.AtEndOfLine) {
                    if(walker.EffectiveLength == walker.SourceLine.Length) {
                        state.ProcessedLines.Add(new BlankLine(label));
                    }
                    else {
                        state.ProcessedLines.Add(new CommentLine(walker.SourceLine, walker.EffectiveLength, label));
                    }
                    return;
                }

                op = walker.ExtractSymbol();
            }

            if(PseudoOpProcessors.ContainsKey(op)) {
                var processor = PseudoOpProcessors[op];
                processedLine = processor(walker);
            }
            else {

                throw new NotImplementedException("Can't parse line (yet): " + line);
            }

            processedLine.Label = label;
            state.ProcessedLines.Add(processedLine);
        }

        private static void ProcessLabel(string label)
        {
            var isPublic = label.EndsWith("::");
            var labelValue = label.TrimEnd(':');

            if(labelValue == "$") {
                state.AddError(AssemblyErrorCode.MaskedDollar, "Using '$' as a label name will prevent using it as the current location pointer");
            }

            var symbol = state.GetSymbol(labelValue);
            if(symbol == null) {
                state.AddSymbol(labelValue, state.GetCurrentLocation(), isPublic: isPublic);
                return;
            }

            if(symbol.IsExternal) {
                state.AddError(AssemblyErrorCode.DuplicateLabel, $"Label has been declared already as external: {labelValue}");
            }
            else if(symbol.IsKnown) {
                if(symbol.Value != state.GetCurrentLocation()) {
                    state.AddError(AssemblyErrorCode.DuplicateLabel, $"Duplicate label: {labelValue}");
                }
            };
        }
    }
}
