using Konamiman.Nestor80.Assembler;
using Konamiman.Nestor80.Assembler.Relocatable;
using Konamiman.Nestor80.Linker.Parsing;
using System.Net.Sockets;

namespace Konamiman.Nestor80.Linker
{
    internal class Expression
    {
        public static Dictionary<string, ushort> Symbols { get; set; }

        public Expression(LinkItem[] items, ushort targetAddress, string programName, ushort codeSegmentStart, ushort dataSegmentStart)
        {
            this.items = items;
            this.TargetAddress = targetAddress;
            this.ProgramName = programName;
            this.CodeSegmentStart = codeSegmentStart;
            this.DataSegmentStart = dataSegmentStart;

            var lastItem = items.Last();
            if(lastItem.ExtendedType is ExtensionLinkItemType.ArithmeticOperator) {
                var operatorCode = (ArithmeticOperatorCode)lastItem.SymbolBytes[0];
                if(operatorCode is ArithmeticOperatorCode.StoreAsByte) {
                    StoreAsByte = true;
                    this.items = items.Take(items.Length - 1).ToArray();
                }
                else if(operatorCode is ArithmeticOperatorCode.StoreAsWord) {
                    StoreAsWord = true;
                    this.items = items.Take(items.Length - 1).ToArray();
                }
            }
        }

        private readonly LinkItem[] items;

        public ushort TargetAddress { get; }

        public bool StoreAsWord { get; }

        public bool StoreAsByte { get; }

        public string ProgramName { get; }

        public ushort CodeSegmentStart { get; }

        public ushort DataSegmentStart { get; }

        public ushort Evaluate()
        {
            if(Symbols is null) {
                throw new InvalidOperationException($"{nameof(Expression)}.{nameof(Evaluate)}: '{nameof(Symbols)}' is null");
            }

            var stack = new Stack<object>();

            foreach(var item in items) {
                var type = item.ExtendedType;

                if(type is ExtensionLinkItemType.Address) {
                    var addressType = (AddressType)item.SymbolBytes[0];
                    var address = (ushort)
                        (addressType is AddressType.CSEG ? CodeSegmentStart :
                        addressType is AddressType.DSEG ? DataSegmentStart :
                        0);

                    address += (ushort)(item.SymbolBytes[1] + (item.SymbolBytes[2] << 8));

                    stack.Push(address);
                    continue;
                }
                else if(type is ExtensionLinkItemType.ReferenceExternal) {
                    if(!Symbols.ContainsKey(item.Symbol)) {
                        Throw($"can't resolve external symbol reference: {item.Symbol}");
                    }

                    var symbolValue = Symbols[item.Symbol];
                    stack.Push(symbolValue);
                    continue;
                }
                else if(type is not ExtensionLinkItemType.ArithmeticOperator) {
                    Throw($"unexpected link item extended type found: {type}");
                }

                var operatorCode = (ArithmeticOperatorCode)item.SymbolBytes[0];
                if(operatorCode < ArithmeticOperatorCode.First || operatorCode > ArithmeticOperatorCode.Last) {
                    Throw($"unexpected arithmetic operator code found: {operatorCode}");
                }

                if(operatorCode >= ArithmeticOperatorCode.High && operatorCode <= ArithmeticOperatorCode.UnaryMinus) {
                    if(stack.Count == 0) {
                        ThrowUnexpected($"found an unary operator ({operatorCode}) but the stack is empty");
                    }

                    var popped = stack.Pop();
                    if(popped is not ushort) {
                        ThrowUnexpected($"found an unexpected value ({popped}) when expecting a number");
                    }

                    var unaryOperand = (ushort)popped;

                    unchecked {
                        var unaryOperationResult = operatorCode switch {
                            ArithmeticOperatorCode.High => (ushort)(unaryOperand >> 8),
                            ArithmeticOperatorCode.Low => (ushort)(unaryOperand & 0xFF),
                            ArithmeticOperatorCode.Not => (ushort)(~unaryOperand),
                            _ => (ushort)-unaryOperand //Unary minus is the only option left
                        };
                        stack.Push(unaryOperationResult);
                    }

                    continue;
                }

                if(stack.Count < 2) {
                    ThrowUnexpected($"found a binary operator ({operatorCode}) but the stack has {stack.Count} items");
                }

                var popped1 = stack.Pop();
                if(popped1 is not ushort) {
                    ThrowUnexpected($"found an unexpected value ({popped1}) when expecting a number");
                }
                var popped2 = stack.Pop();
                if(popped2 is not ushort) {
                    ThrowUnexpected($"found an unexpected value ({popped2}) when expecting a number");
                }

                var operand1 = (ushort)popped1;
                var operand2 = (ushort)popped2;

                if(operatorCode is ArithmeticOperatorCode.Divide && operand2 == 0) {
                    Throw("Division by zero");
                }

                unchecked {
                    var binaryOperationResult = operatorCode switch {
                        ArithmeticOperatorCode.Minus => (ushort)(operand1 - operand2),
                        ArithmeticOperatorCode.Plus => (ushort)(operand1 + operand2),
                        ArithmeticOperatorCode.Multiply => (ushort)(operand1 * operand2),
                        ArithmeticOperatorCode.Divide => (ushort)(operand1 / operand2),
                        ArithmeticOperatorCode.Mod => (ushort)(operand1 % operand2),
                        ArithmeticOperatorCode.ShiftRight => (ushort)(operand1 >> operand2),
                        ArithmeticOperatorCode.ShiftLeft => (ushort)(operand1 << operand2),
                        ArithmeticOperatorCode.Equals => (ushort)(operand1 == operand2 ? 0xFFFF : 0),
                        ArithmeticOperatorCode.NotEquals => (ushort)(operand1 == operand2 ? 0 : 0xFFFF),
                        ArithmeticOperatorCode.LessThan => (ushort)(operand1 < operand2 ? 0xFFFF : 0),
                        ArithmeticOperatorCode.LessThanOrEqual => (ushort)(operand1 <= operand2 ? 0xFFFF : 0),
                        ArithmeticOperatorCode.GreaterThan => (ushort)(operand1 > operand2 ? 0xFFFF : 0),
                        ArithmeticOperatorCode.GreaterThanOrEqual => (ushort)(operand1 >= operand2 ? 0xFFFF : 0),
                        ArithmeticOperatorCode.And => (ushort)(operand1 & operand2),
                        ArithmeticOperatorCode.Or => (ushort)(operand1 | operand2),
                        _ => (ushort)(operand1 ^ operand2), //Xor is the only option left
                    };

                    stack.Push(binaryOperationResult);
                }
            }

            if(stack.Count != 1) {
                ThrowUnexpected($"size of stack at the end should be 1, but it's {stack.Count}");
            }

            var result = stack.Pop();
            if(result is not ushort) {
                ThrowUnexpected($"final result should be of type ushort, but it's {result.GetType().Name}");
            }


            return (ushort)result;
        }

        private void Throw(string message) {
            throw new ExpressionEvaluationException($"In program {ProgramName}: evaluating expression: {message}");
        }

        private void ThrowUnexpected(string message)
        {
            throw new Exception($"In program {ProgramName}: {nameof(Expression)}.{nameof(Evaluate)}: {message}");
        }
    }
}
