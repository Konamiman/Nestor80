using Konamiman.Nestor80.Assembler.ArithmeticOperations;
using Konamiman.Nestor80.Assembler.Expressions;

namespace Konamiman.Nestor80.Assembler
{
    internal partial class Expression
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

            if(Parts[^1] is ArithmeticOperator) {
                Throw($"{Parts[^1]} found at the end of the expression");
            }

            Postfixize();
            IsPostfixized = true;
        }

        public bool IsRawBytesOutput => Parts.Length == 1 && Parts[0] is RawBytesOutput;

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

        public SymbolReference[] ReferencedSymbols => 
            Parts.Where(p => p is SymbolReference).Cast<SymbolReference>().ToArray();

        public Address Evaluate() => EvaluateCore(true);
        public Address TryEvaluate() => EvaluateCore(false);

        private Address EvaluateCore(bool throwOnUnknownSymbol)
        {
            if(!IsPostfixized) {
                throw new InvalidOperationException($"{nameof(Expression)}: {nameof(ValidateAndPostifixize)} must be executed before {nameof(Evaluate)} or {nameof(TryEvaluate)}");
            }

            var stack = new Stack<IExpressionPart>();

            var hasUnknownSymbols = false;

            foreach(var part in Parts) {
                var item = part;
                if(item is UnaryOperator uop) {
                    if(stack.Count == 0) {
                        throw new InvalidOperationException($"{nameof(Expression)}.{nameof(Parse)}: found an unary operator ({uop}) but the items stack is empty. Expression: {Source}");
                    }

                    var poppedItem = stack.Pop();
                    var poppedAddress = ResolveAddressOrSymbol(poppedItem, throwOnUnknownSymbol);
                    if(poppedAddress is null) {
                        return null;
                    }

                    var operationResult = uop.Operate(poppedAddress, null);
                    stack.Push(operationResult);
                }
                else if(item is BinaryOperator bop) {
                    if(stack.Count < 2) {
                        throw new InvalidOperationException($"{nameof(Expression)}.{nameof(Parse)}: found a binary operator ({bop}) but the items stack contains {stack.Count} items (expected at least 2). Expression: {Source}");
                    }

                    var poppedItem2 = stack.Pop();
                    var poppedAddress2 = ResolveAddressOrSymbol(poppedItem2, throwOnUnknownSymbol);
                    if(poppedAddress2 is null) {
                        return null;
                    }

                    var poppedItem1 = stack.Pop();
                    var poppedAddress1 = ResolveAddressOrSymbol(poppedItem1, throwOnUnknownSymbol);
                    if(poppedAddress1 is null) {
                        return null;
                    }

                    var operationResult = bop.Operate(poppedAddress1, poppedAddress2);
                    stack.Push(operationResult);
                }
                else {
                    var address = ResolveAddressOrSymbol(item, throwOnUnknownSymbol);
                    if(address is null) {
                        hasUnknownSymbols = true;
                        address = Address.AbsoluteZero;
                    }

                    stack.Push(address);
                }
            }

            if(hasUnknownSymbols) {
                return null;
            }

            if(stack.Count != 1) {
                throw new Exception($"Unexpected expression parse result: the resulting stack should have one item, but it has {stack.Count}.");
            }

            var result = stack.Pop();
            if(result is not Address) {
                throw new Exception($"Unexpected expression parse result: the resulting item should be an {nameof(Address)}, but is an {result.GetType().Name} ({result}).");
            }

            return (Address)result;
        }

        private Address ResolveAddressOrSymbol(IExpressionPart part, bool throwOnUnknownSymbol)
        {
            if(part is Address address) {
                return address;
            }
            else if(part is not SymbolReference) {
                throw new InvalidOperationException($"{nameof(Expression)}.{nameof(Parse)}: unexpected expression part type found: {part.GetType().Name} ({part}).");
            }

            var sr = (SymbolReference)part;

            var symbol = GetSymbol(sr.SymbolName, sr.IsExternal);
            if(symbol is null) {
                throw new InvalidOperationException($"{nameof(Expression)}.{nameof(Parse)} isn't supposed to be executed before all the referenced symbols are registered (even if the symbol value isn't yet known). Symbol: {sr.SymbolName}");
            }

            if(symbol.IsExternal) {
                if(throwOnUnknownSymbol)
                    throw new InvalidOperationException($"{nameof(Expression)}.{nameof(Parse)} isn't supposed to be executed when the expression contains external symbols. Symbol: {sr.SymbolName}");
                else
                    return null;
            }

            if(symbol.HasKnownValue) {
                return symbol.Value;
            }
            else if(throwOnUnknownSymbol) {
                Throw($"Unknown symbol: {sr.SymbolName}");
            }

            return null;
        }
    }
}
