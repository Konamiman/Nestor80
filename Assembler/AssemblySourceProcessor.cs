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

        private static readonly Regex labelRegex = new("^[\\w$@?._][\\w$@?._0-9]*:{0,2}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex externalSymbolRegex = new("^[a-zA-Z_$@?.][a-zA-Z_$@?.0-9]*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
                Expression.GetSymbol = GetSymbolForExpression;

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
                EndAddress = state.EndAddress ?? Address.AbsoluteZero,
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

            if(!state.EndReached) {
                state.AddError(AssemblyErrorCode.NoEndStatement, "No END statement found");
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
                    ProcessLabelDefinition(label);
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
                processedLine = processor(op, walker);
            }
            else {
                throw new NotImplementedException("Can't parse line (yet): " + line);
            }

            if(!walker.AtEndOfLine) {
                state.AddError(AssemblyErrorCode.UnexpectedContentAtEndOfLine, $"Unexpected content found at the end of the line: {walker.GetRemaining()}");
            }

            processedLine.EffectiveLineLength = walker.DiscardRemaining();
            processedLine.Label = label;
            state.ProcessedLines.Add(processedLine);
        }

        public static Symbol GetSymbolForExpression(string name, bool isExternal)
        {
            if(name == "$")
                return new Symbol() { Name = "$", Value = new Address(state.CurrentLocationArea, state.CurrentLocationPointer) };

            var symbol = state.GetSymbol(name);
            if(symbol is null) {
                state.AddSymbol(name, isExternal: isExternal);
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
                state.AddSymbol(labelValue, state.GetCurrentLocation(), isPublic: isPublic, isLabel: true);
            }
            else if(symbol.IsExternal) {
                state.AddError(AssemblyErrorCode.DuplicatedSymbol, $"Label has been declared already as external: {labelValue}");
            }
            else if(symbol.IsKnown) {
                if(symbol.Value != state.GetCurrentLocation()) {
                    state.AddError(AssemblyErrorCode.DuplicatedSymbol, $"Duplicate label: {labelValue}");
                }
            }
            else {
                //PUBLIC declaration preceded label in code
                symbol.Value = state.GetCurrentLocation();
            };
        }
    }
}
