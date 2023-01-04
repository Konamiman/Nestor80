using Konamiman.Nestor80.Assembler.Errors;
using Konamiman.Nestor80.Assembler.Expressions;
using Konamiman.Nestor80.Assembler.Expressions.ExpressionParts;
using Konamiman.Nestor80.Assembler.Expressions.ExpressionParts.ArithmeticOperators;
using Konamiman.Nestor80.Assembler.Infrastructure;
using Konamiman.Nestor80.Assembler.Relocatable;

namespace Konamiman.Nestor80.Assembler
{
    internal partial class Expression
    {
        public bool IsPostfixized { get; private set; } = false;

        public bool HasTypeOperator => Parts.Any(p => p is TypeOperator);

        /// <summary>
        /// Validate the expression and convert it to postfix format using the shunting yard algorithm.
        /// This needs to be done before the expression is evaluated.
        /// </summary>
        /// <exception cref="Exception"></exception>
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
            else if(part is UnaryOperator) {
                if(!(previous is null or OpeningParenthesis or ArithmeticOperator)) {
                    Throw($"{part} can only be preceded by an operator or (");
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

        /// <summary>
        /// Convert the expression to postfix format using the shunting yard algorithm.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
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

        private bool externalSymbolFound;

        /// <summary>
        /// Try to evaluate the expression, if an unknown symbol is found throw an exception.
        /// </summary>
        /// <returns></returns>
        public Address Evaluate() => EvaluateCore(false, true);

        /// <summary>
        /// Evaluate the expression, but if it contains symbols return null;
        /// </summary>
        /// <returns></returns>
        public Address EvaluateIfNoSymbols() => EvaluateCore(true, false);

        /// <summary>
        /// Try to evaluate the expression, if an unknown symbol is found return null.
        /// </summary>
        /// <returns></returns>
        public Address TryEvaluate() => EvaluateCore(false, false);

        private Address EvaluateCore(bool stopOnSymbolFound, bool throwOnUnknownSymbol)
        {
            if(!IsPostfixized) {
                throw new InvalidOperationException($"{nameof(Expression)}: {nameof(ValidateAndPostifixize)} must be executed before {nameof(Evaluate)} or {nameof(EvaluateIfNoSymbols)}");
            }

            if(stopOnSymbolFound) {
                var symbolParts = Parts.OfType<SymbolReference>();
                if(symbolParts.Any()) {
                    //We need each symbol to be registered, even if value is still unknown
                    foreach(var sr in symbolParts) {
                        GetSymbol(sr.SymbolName, sr.IsExternal, sr.IsRoot);
                    }
                    return null;
                }
            }

            if(HasTypeOperator) {
                var unknownSymbolsFound = ResolveTypeOperators(throwOnUnknownSymbol);
                if(unknownSymbolsFound) {
                    return null;
                }
            }

            externalSymbolFound = false;

            var stack = new Stack<IExpressionPart>();

            Address operationResult;

            var processedPartsCount = 0;
            foreach(var part in Parts) {
                var item = part;
                if(item is UnaryOperator uop) {
                    if(stack.Count == 0) {
                        throw new InvalidOperationException($"{nameof(Expression)}.{nameof(Parse)}: found an unary operator ({uop}) but the items stack is empty. Expression: {Source}");
                    }

                    var poppedItem = stack.Pop();
                    var poppedAddress = ResolveAddressOrSymbol(poppedItem);
                    if(poppedAddress is null) {
                        return null;
                    }
                    
                    if(uop is not UnaryPlusOperator && !poppedAddress.IsAbsolute && isByte) {
                        if(uop.ExtendedLinkItemType is null) {
                            Throw($"Operator {uop} is not allowed in expressions involving relocatable addresses that evaluate to a single byte", AssemblyErrorCode.InvalidForRelocatable);
                            return null;
                        }
                        HasRelocatableToStoreAsByte = true;
                        return null;
                    }

                    operationResult = uop.Operate(poppedAddress, null);
                    stack.Push(operationResult);
                }
                else if(item is BinaryOperator bop) {
                    if(stack.Count < 2) {
                        throw new InvalidOperationException($"{nameof(Expression)}.{nameof(Parse)}: found a binary operator ({bop}) but the items stack contains {stack.Count} items (expected at least 2). Expression: {Source}");
                    }

                    var poppedItem2 = stack.Pop();
                    var poppedAddress2 = ResolveAddressOrSymbol(poppedItem2);
                    if(poppedAddress2 is null) {
                        return null;
                    }

                    var poppedItem1 = stack.Pop();
                    var poppedAddress1 = ResolveAddressOrSymbol(poppedItem1);
                    if(poppedAddress1 is null) {
                        return null;
                    }

                    if((!poppedAddress1.IsAbsolute || !poppedAddress2.IsAbsolute) && isByte) {
                        if(bop.ExtendedLinkItemType is null) {
                            Throw($"Operator {bop} is not allowed in expressions involving relocatable addresses that evaluate to a single byte", AssemblyErrorCode.InvalidForRelocatable);
                            return null;
                        }

                        HasRelocatableToStoreAsByte = true;
                        return null;
                    }

                    operationResult = bop.Operate(poppedAddress1, poppedAddress2);
                    stack.Push(operationResult);
                }
                else {
                    var address = ResolveAddressOrSymbol(item);
                    if(externalSymbolFound) {
                        if(processedPartsCount > 0) {
                            //At least keep what we have calculated so far.
                            Parts = Parts.Skip(processedPartsCount - 1).ToArray();
                            Parts[0] = stack.Pop();
                        }
                        throw new ExpressionContainsExternalReferencesException($"Symbol is external: {((SymbolReference)item).SymbolName}");
                    }
                    if(address is null) {
                        if(throwOnUnknownSymbol) {
                            var symbolName = ModularizeSymbolName(((SymbolReference)item).SymbolName);
                            Throw($"Unknown symbol: {symbolName}");
                        }
                        else {
                            return null;
                        }
                    }

                    stack.Push(address);
                }
                processedPartsCount++;
            }

            if(stack.Count != 1) {
                throw new Exception($"Unexpected expression parse result: the resulting stack should have one item, but it has {stack.Count}.");
            }

            if(externalSymbolFound) {
                return null;
            }

            var result = stack.Pop();
            if(result is not Address) {
                throw new Exception($"Unexpected expression parse result: the resulting item should be an {nameof(Address)}, but is an {result.GetType().Name} ({result}).");
            }

            return (Address)result;
        }

        /// <summary>
        /// Transform the expression parts so that TYPE operators are resolved.
        /// </summary>
        /// <remarks>
        /// The TYPE operator is special in that it's the only one that makes external symbol references
        /// disappear (TYPE FOO## evaluates to absolute 80h). We want these to be resolved before the actual
        /// expression evaluation happens so that we don't end up having later "fake" external references
        /// (especially if the evaluation is interrupted due to e.g. a relocatable address having to be stored
        /// as a byte and thus the expression needs to be written "as is" to the resulting relocatable file).
        /// 
        /// The TYPE operator is the one having the highest precedence and thus doing such a "selective evaluation"
        /// is easy, we just need to replace pairs of TYPE+argument with the calculated value.
        /// </remarks>
        /// <param name="throwOnUnknownSymbol">True to throw an exception if an unknown symbol is found.</param>
        /// <returns>True if unknown symbols were found (and no exception was thrown)</returns>
        private bool ResolveTypeOperators(bool throwOnUnknownSymbol)
        {
            var newParts = Parts.ToList();

            var index = 1; //We know that item at 0 is not a TYPE operator, validation would have failed if so.
            Address calculatedType = null;

            while(index < newParts.Count) {
                if(newParts[index] is not TypeOperator) {
                    index++;
                    continue;
                }

                var previousPart = newParts[index-1];
                if(previousPart is not Address and not SymbolReference) {
                    Throw("The TYPE operator can only be followed by a numeric constant or a symbol");
                }

                var operand = ResolveAddressOrSymbol(previousPart);
                if(externalSymbolFound) {
                    calculatedType = Address.Absolute(0x80);
                    externalSymbolFound = false;
                }
                else if(operand is not null) {
                    calculatedType = TypeOperator.Instance.Operate(operand, null);
                }
                else if(throwOnUnknownSymbol) {
                    var symbolName = ModularizeSymbolName(((SymbolReference)previousPart).SymbolName);
                    Throw($"Unknown symbol: {symbolName}");
                }
                else {
                    return true;
                }

                newParts[index] = calculatedType;
                newParts.RemoveAt(index - 1);
            }

            Parts = newParts.ToArray();
            return false;
        }

        private Address ResolveAddressOrSymbol(IExpressionPart part)
        {
            if(part is Address address) {
                return address;
            }
            else if(part is not SymbolReference) {
                throw new InvalidOperationException($"{nameof(Expression)}.{nameof(Parse)}: unexpected expression part type found: {part.GetType().Name} ({part}).");
            }

            var sr = (SymbolReference)part;

            var symbol = GetSymbol(sr.SymbolName, sr.IsExternal, sr.IsRoot);
            if(symbol is null) {
                throw new InvalidOperationException($"{nameof(Expression)}.{nameof(Parse)} isn't supposed to be executed before all the referenced symbols are registered (even if the symbol value isn't yet known). Symbol: {sr.SymbolName}");
            }

            if(symbol.IsExternal) {
                externalSymbolFound = true;
                return Address.AbsoluteZero;
            }

            if(symbol.HasKnownValue) {
                return symbol.Value;
            }
            else {
                return null;
            }
        }
    }
}
