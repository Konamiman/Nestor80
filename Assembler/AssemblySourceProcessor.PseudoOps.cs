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
            { "EXT", ProcessExternalDeclarationLine },
            { "EXTRN", ProcessExternalDeclarationLine },
            { "EXTERNAL", ProcessExternalDeclarationLine },
            { "PUBLIC", ProcessPublicDeclarationLine },
            { "END", ProcessEndLine }
        };

        static ProcessedSourceLine ProcessDefbLine(string opcode, SourceLineWalker walker)
        {
            var line = new DefbLine();
            var outputBytes = new List<byte>();
            var relocatables = new List<RelocatableOutputPart>();
            var index = -1;

            if(walker.AtEndOfLine) {
                state.AddError(AssemblyErrorCode.MissingValue, "DB needs at least one byte value");
                outputBytes.Add(0);
            }

            while(!walker.AtEndOfLine) {
                index++;
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
                        state.RegisterPendingExpression(line, expression, index, 1);
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
                        relocatables.Add(new RelocatableAddress() { Index = index, IsByte = true, Type = value.Type, Value = value.Value });
                    }
                }
                catch(InvalidExpressionException ex) {
                    outputBytes.Add(0);
                    state.AddError(AssemblyErrorCode.InvalidExpression, $"Invalid expression: {ex.Message}");
                }
            }

            state.IncreaseLocationPointer(outputBytes.Count);

            line.OutputBytes = outputBytes.ToArray();
            line.RelocatableParts = relocatables.ToArray();
            line.NewLocationCounter = state.GetCurrentLocation();
            
            return line;
        }

        static ProcessedSourceLine ProcessCsegLine(string opcode, SourceLineWalker walker) => ProcessChangeAreaLine(AddressType.CSEG);

        static ProcessedSourceLine ProcessDsegLine(string opcode, SourceLineWalker walker) => ProcessChangeAreaLine(AddressType.DSEG);

        static ProcessedSourceLine ProcessAsegLine(string opcode, SourceLineWalker walker) => ProcessChangeAreaLine(AddressType.ASEG);

        static ProcessedSourceLine ProcessChangeAreaLine(AddressType area)
        {
            state.SwitchToArea(area);

            return new ChangeAreaLine() {
                NewLocationCounter = state.GetCurrentLocation(),
            };
        }

        static ProcessedSourceLine ProcessOrgLine(string opcode, SourceLineWalker walker)
        {
            if(walker.AtEndOfLine) {
                state.AddError(AssemblyErrorCode.LineHasNoEffect, "ORG without value will have no effect");
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
                    return new ChangeOriginLine() { NewLocationCounter = value };
                }
            }
            catch(InvalidExpressionException ex) {
                state.AddError(AssemblyErrorCode.InvalidExpression, $"Invalid expression for {opcode.ToUpper()}: {ex.Message}");
                return new ChangeOriginLine();
            }
        }

        static ProcessedSourceLine ProcessExternalDeclarationLine(string opcode, SourceLineWalker walker)
        {
            if(walker.AtEndOfLine) {
                state.AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} must be followed by a symbol name");
                return new ExternalDeclarationLine();
            }

            var symbolName = walker.ExtractSymbol();
            if(!externalSymbolRegex.IsMatch(symbolName)) {
                state.AddError(AssemblyErrorCode.InvalidLabel, $"{symbolName} is not a valid external symbol name, it contains invalid characters");
                return new ExternalDeclarationLine() { SymbolName = symbolName };
            }

            var existingSymbol = state.GetSymbol(symbolName);
            if(existingSymbol is null) {
                state.AddSymbol(symbolName, isExternal: true);
            }
            else if(existingSymbol.IsKnown) {
                state.AddError(AssemblyErrorCode.DuplicatedSymbol, $"{symbolName} is already defined, can't be declared as an external symbol");
            }

            return new ExternalDeclarationLine() { SymbolName = symbolName };
        }

        static ProcessedSourceLine ProcessPublicDeclarationLine(string opcode, SourceLineWalker walker)
        {
            if(walker.AtEndOfLine) {
                state.AddError(AssemblyErrorCode.MissingValue, $"{opcode.ToUpper()} must be followed by a label name");
                return new PublicDeclarationLine();
            }

            var symbolName = walker.ExtractSymbol();
            if(!externalSymbolRegex.IsMatch(symbolName)) {
                state.AddError(AssemblyErrorCode.InvalidLabel, $"{symbolName} is not a valid public symbol name, it contains invalid characters");
                return new ExternalDeclarationLine() { SymbolName = symbolName };
            }


            var existingSymbol = state.GetSymbol(symbolName);
            if(existingSymbol is null) {
                state.AddSymbol(symbolName, isLabel: true, isPublic: true);
            }
            else if(existingSymbol.IsExternal) {
                state.AddError(AssemblyErrorCode.DuplicatedSymbol, $"{symbolName} is already defined as an external symbol, can't be defined as public");
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
                return new AssemblyEndLine() { EndAddress = endAddress };
            }
            catch(InvalidExpressionException ex) {
                state.AddError(AssemblyErrorCode.InvalidExpression, $"Invalid expression for {opcode.ToUpper()}: {ex.Message}");
                state.End(Address.AbsoluteZero);
                return new AssemblyEndLine();
            }
        }
    }
}
