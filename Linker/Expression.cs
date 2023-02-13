using Konamiman.Nestor80.Assembler.Relocatable;
using Konamiman.Nestor80.Linker.Parsing;

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
                    items = items.Take(items.Length - 1).ToArray();
                }
                else if(operatorCode is ArithmeticOperatorCode.StoreAsWord) {
                    StoreAsWord = true;
                    items = items.Take(items.Length - 1).ToArray();
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
                    var address =
                        (item.Address.Type is Assembler.AddressType.CSEG ? CodeSegmentStart :
                        item.Address.Type is Assembler.AddressType.DSEG ? DataSegmentStart :
                        0) + item.Address.Value;

                    stack.Push(address);
                }
                else if(type is ExtensionLinkItemType.ReferenceExternal) {
                    if(!Symbols.ContainsKey(item.Symbol)) {
                        Throw($"Can't resolve external symbol reference: {item.Symbol}");
                    }

                    var symbolValue = Symbols[item.Symbol];
                    stack.Push(symbolValue);
                }
                else if(type is not ExtensionLinkItemType.ArithmeticOperator) {
                    Throw($"Unexpected link item extended type found: {type}");
                }

                var operatorCode = (ArithmeticOperatorCode)item.SymbolBytes[0];
                if(operatorCode < ArithmeticOperatorCode.First || operatorCode > ArithmeticOperatorCode.Last) {
                    //WIP
                }

                //WIP
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
            throw new ExpressionEvaluationException($"In program {ProgramName}: {message}");
        }

        private void ThrowUnexpected(string message)
        {
            throw new Exception($"{nameof(Expression)}.{nameof(Evaluate)}: {message}");
        }
    }
}
