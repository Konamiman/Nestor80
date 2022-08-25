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
                        Throw($"A string of {rbo.Length} (more than 2) bytes can't be part of an expression");
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
            if(part is UnaryOperator ) {
                if(!(previous is null or ArithmeticOperator or OpeningParenthesis)) {
                    Throw($"{part} can only be preceded by another operator or by (");
                }
            }
            else if(part is BinaryOperator) {
                if(!(previous is Address or SymbolReference or ClosingParenthesis)) {
                    Throw($"{part} can only be preceded by a number, a symbol, or )");
                }
            }
            else if(part is Address or SymbolReference) {
                if(!(previous is null or BinaryOperator or OpeningParenthesis)) {
                    Throw($"A { (part is Address ? "number" : "symbol") } can only be preceded by a binary operator or by (");
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
            //https://itdranik.com/en/math-expressions-shunting-yard-algorithm-en/

            var operators = new Stack<IExpressionPart>();
            var result = new List<IExpressionPart>();

            foreach(var part in Parts) {
                if(part is Address or SymbolReference) {
                    result.Add(part);
                }
                else if(part is OpeningParenthesis) {
                    operators.Push(part);
                }
                else if(part is ClosingParenthesis) {
                    var openingParenthesisFound = false;
                    while(operators.Count > 0 && !openingParenthesisFound) {
                        var popped = operators.Pop();
                        if(popped is OpeningParenthesis)
                            openingParenthesisFound = true;
                        else
                            operators.Push(popped);
                    }
                    if(!openingParenthesisFound) {
                        Throw("Missing (");
                    }
                }
                else {
                    var op = (ArithmeticOperator)part;
                    var operatorPriority = op.Precedence;

                    while(operators.Count > 0) {
                        var stackOperatorToken = operators.Peek();
                        if(stackOperatorToken is OpeningParenthesis) {
                            break;
                        }

                        var stackOperatorPriority = ((ArithmeticOperator)stackOperatorToken).Precedence;
                        if(stackOperatorPriority < operatorPriority) {
                            break;
                        }

                        result.Add(operators.Pop());
                    }

                    operators.Push(op);
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
