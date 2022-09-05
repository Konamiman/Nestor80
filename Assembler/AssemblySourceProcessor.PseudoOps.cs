using Konamiman.Nestor80.Assembler.Expressions;
using Konamiman.Nestor80.Assembler.Output;

namespace Konamiman.Nestor80.Assembler
{
    public partial class AssemblySourceProcessor
    {
        readonly Dictionary<string, Func<string, SourceLineWalker, ProcessedSourceLine>> PseudoOpProcessors = new(StringComparer.OrdinalIgnoreCase) {
            { "DB", ProcessDefbLine },
            { "DEFB", ProcessDefbLine },
            { "CSEG", ProcessCsegLine },
            { "DSEG", ProcessDsegLine },
            { "ASEG", ProcessAsegLine },
            { "ORG", ProcessOrgLine },
        };

        static ProcessedSourceLine ProcessDefbLine(string operand, SourceLineWalker walker)
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

            return new DefbLine(
                line: walker.SourceLine,
                outputBytes: outputBytes.ToArray(),
                expressions: outputExpressions.ToArray(),
                newLocationCounter: new Address(state.CurrentLocationArea, state.CurrentLocationPointer),
                operand: operand
            );
        }

        static ProcessedSourceLine ProcessCsegLine(string operand, SourceLineWalker walker) => ProcessChangeAreaLine(operand, AddressType.CSEG, walker);

        static ProcessedSourceLine ProcessDsegLine(string operand, SourceLineWalker walker) => ProcessChangeAreaLine(operand, AddressType.DSEG, walker);

        static ProcessedSourceLine ProcessAsegLine(string operand, SourceLineWalker walker) => ProcessChangeAreaLine(operand, AddressType.ASEG, walker);

        static ProcessedSourceLine ProcessChangeAreaLine(string operand, AddressType area, SourceLineWalker walker)
        {
            state.SwitchToArea(area);

            return new ChangeAreaLine(
                line: walker.SourceLine,
                newLocationCounter: state.GetCurrentLocation(),
                operand: operand
            );
        }

        static ProcessedSourceLine ProcessOrgLine(string operand, SourceLineWalker walker)
        {
            if(walker.AtEndOfLine) {
                state.AddError(AssemblyErrorCode.LineHasNoEffect, "ORG without value will have no effect");
                return new ChangeOriginLine(walker.SourceLine, null, operand);
            }

            var valueExpressionString = walker.ExtractExpression();
            var valueExpression = Expression.Parse(valueExpressionString);
            valueExpression.ValidateAndPostifixize();
            var value = valueExpression.TryEvaluate();
            if(value is null) {
                return new ChangeOriginLine(walker.SourceLine, null, operand) { NewLocationCounterExpression = valueExpression };
            }
            else {
                state.SwitchToLocation(value.Value);
                return new ChangeOriginLine(walker.SourceLine, value, operand);
            }
        }
    }
}
