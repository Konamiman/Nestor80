using Konamiman.Nestor80.Assembler.ArithmeticOperations;

namespace Konamiman.Nestor80.Assembler
{
    internal partial class Expression : IAssemblyOutputPart
    {
        public bool IsPostfixized { get; private set; } = false;

        public void ValidateAndPostifixize()
        {
            if(IsPostfixized) return;

            if(Parts.Length == 0) {
                IsPostfixized = true;
                return;
            }

            if(Parts.Length == 1) {
                if(!(Parts[0] is Address or SymbolReference or RawBytesOutput)) {
                    Throw($"{Parts[0]} can't be an expression by itself");
                }
                IsPostfixized = true;
                return;
            }

            for(int i = 0; i < Parts.Length; i++) {
                var part = Parts[i];
                if(part is RawBytesOutput rbo) {
                    if(rbo.Length > 2) {
                        throw new Exception($"String of {rbo.Length} bytes found as part of an expression, this should have been filtered out by {nameof(Expression)}.{nameof(Parse)}");
                    }
                    Parts[i] = part = Address.Absolute(rbo.NumericValue);
                }
                ValidatePart(part, i == 0 ? null : Parts[i - 1]);
            }

            Postfixize();
            IsPostfixized = true;
        }

        private void ValidatePart(IExpressionPart part, IExpressionPart previous)
        {
            if(part is UnaryPlusOperator or UnaryMinusOperator) {
                if(!(previous is null or ArithmeticOperator or OpeningParenthesis)) {
                    Throw($"{part} can only be preceded by another operator or by (");
                }
            }
            else if(part is UnaryOperator ) {
                if(!(previous is null or OpeningParenthesis)) {
                    Throw($"{part} can only be preceded by (");
                }
            }
            else if(part is BinaryOperator) {
                if(!(previous is Address or SymbolReference or ClosingParenthesis)) {
                    Throw($"{part} can only be preceded by a number, a symbol, or )");
                }
            }
            else if(part is Address or SymbolReference) {
                if(!(previous is null or ArithmeticOperator or OpeningParenthesis)) {
                    Throw($"A { (part is Address ? "number" : "symbol") } can only be preceded by an operator or by (");
                }
            }
            else if(part is OpeningParenthesis) {
                if(!(previous is null or ArithmeticOperator or OpeningParenthesis)) {
                    Throw($"( can only be preceded by a an operator or by another (");
                }
            }
            else if(part is ClosingParenthesis) {
                if(!(previous is Address or SymbolReference or ClosingParenthesis)) {
                    Throw($") can only be preceded by a an address, a symbol or another )");
                }
            }
            else {
                throw new Exception($"Found an unexpected type of {nameof(IExpressionPart)}: {part.GetType().Name}");
            }
        }

        private void Postfixize()
        {
            // https://itdranik.com/en/math-expressions-shunting-yard-algorithm-en/
            // (Support for unary operators: https://stackoverflow.com/a/44562047/4574 )

            var operators = new Stack<IExpressionPart>();
            var result = new List<IExpressionPart>();

            foreach(var part in Parts) {
                if(part is Address or SymbolReference) {
                    result.Add(part);
                }
                else if(part is OpeningParenthesis or UnaryOperator) {
                    operators.Push(part);
                }
                else if(part is ClosingParenthesis) {
                    var openingParenthesisFound = false;
                    while(operators.Count > 0 && !openingParenthesisFound) {
                        var popped = operators.Pop();
                        if(popped is OpeningParenthesis)
                            openingParenthesisFound = true;
                        else
                            result.Add(popped);
                    }
                    if(!openingParenthesisFound) {
                        Throw("Missing (");
                    }
                }
                else if(part is ArithmeticOperator op) {
                    var operatorPrecedence = op.Precedence;

                    while(operators.Count > 0) {
                        var stackOperatorToken = operators.Peek();
                        if(stackOperatorToken is OpeningParenthesis) {
                            break;
                        }

                        var stackOperatorPrecedence = ((ArithmeticOperator)stackOperatorToken).Precedence;
                        if(stackOperatorPrecedence > operatorPrecedence && stackOperatorToken is not UnaryOperator) {
                            break;
                        }

                        result.Add(operators.Pop());
                    }

                    operators.Push(op);
                }
                else {
                    throw new InvalidOperationException($"{nameof(Expression)}.{nameof(ValidateAndPostifixize)}: Unexpected expression part found: {part}");
                }
            }

            while(operators.Count > 0) {
                var op = operators.Pop();
                if(op is OpeningParenthesis) {
                    Throw("Extra ( found");
                }
                result.Add(op);
            }

            Parts = result.ToArray();
        }
    }
}
