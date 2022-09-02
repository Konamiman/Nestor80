using Konamiman.Nestor80.Assembler.Expressions;
using Konamiman.Nestor80.Assembler.Output;

namespace Konamiman.Nestor80.Assembler
{
    public partial class AssemblySourceProcessor
    {
        readonly Dictionary<string, Action<AssemblyState, SourceLineWalker>> PseudoOpProcessors = new(StringComparer.OrdinalIgnoreCase) {
            { "DB", ProcessDefb },
            { "DEFB", ProcessDefb }
        };

        static void ProcessDefb(AssemblyState state, SourceLineWalker walker)
        {
            var outputBytes = new List<byte>();
            var outputExpressions = new List<Tuple<int, IExpressionPart[]>>();
            var index = 0;

            if(walker.AtEndOfLine) {
                state.AddError(AssemblyErrorCode.MisssingOperand, "DB needs at least one byte value");
                outputBytes.Add(0);
            }

            while(!walker.AtEndOfLine) {
                var expressionText = walker.ExtractExpression();
                if(expressionText == "") {
                    if(walker.AtEndOfLine) {
                        state.AddError(AssemblyErrorCode.UnexpectedContentAtEndOfLine, "Unexpected ',' found at the end of the line");
                        break;
                    }
                    else {
                        outputBytes.Add(0);
                        state.AddError(AssemblyErrorCode.InvalidExpression, "Empty expression found");
                        continue;
                    }
                }
                try {
                    var expression = Expression.Parse(expressionText, forDefb: true);
                    expression.ValidateAndPostifixize();

                    if(expression.IsRawBytesOutput) {
                        outputBytes.AddRange((RawBytesOutput)expression.Parts[0]);
                        continue;
                    }

                    var unknownSymbols = expression.ReferencedSymbols.Where(s => !state.HasSymbol(s.SymbolName));
                    foreach(var symbol in unknownSymbols) {
                        state.AddSymbol(symbol.SymbolName, isExternal: symbol.IsExternal);
                    }

                    var value = expression.TryEvaluate();
                    if(value is null) {
                        outputBytes.Add(0);
                        outputExpressions.Add(new(index, expression.Parts.ToArray()));
                    }
                    else if(!value.IsValidByte) {
                        outputBytes.Add(0);
                        state.AddError(AssemblyErrorCode.InvalidExpression, $"Invalid expression: value {value:X4} can't be stored as a byte");
                    }
                    else if(value.IsAbsolute) {
                        outputBytes.Add(value.ValueAsByte);
                    }
                    else {
                        outputBytes.Add(0);
                        outputExpressions.Add(new(index, new IExpressionPart[] { value }));
                    }
                }
                catch(InvalidExpressionException ex) {
                    outputBytes.Add(0);
                    state.AddError(AssemblyErrorCode.InvalidExpression, $"Invalid expression: {ex.Message}");
                }
                index++;
            }

            state.IncreaseLocationPointer(outputBytes.Count);

            state.ProcessedLines.Add(
                new DefbLine() {
                    Line = walker.SourceLine, 
                    EffectiveLineLength = walker.EffectiveLength,
                    Expressions = outputExpressions.ToArray(), 
                    OutputBytes = outputBytes.ToArray(),
                    NewLocationCounter = new Address(state.CurrentLocationArea, state.CurrentLocationPointer)
                }
            );
        }
    }
}
