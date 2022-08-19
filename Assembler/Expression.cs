using Konamiman.Nestor80.Assembler.ArithmeticOperations;
using System.Text;

namespace Konamiman.Nestor80.Assembler
{
    internal class Expression : IAssemblyOutputPart
    {
        private Expression(IExpressionPart[] parts = null)
        {
            this.Parts = parts ?? new IExpressionPart[0];
        }

        public static Expression Parse(string expressionString) =>
            expressionString switch { 
                { Length: < 2 } => throw new ArgumentException("Too short string supplied to Expression.Parse"), 
                _ when expressionString[0] is '"' or '\'' => ParseString(expressionString),
                _ => ParseNonString(expressionString)
            };
 
        private static Expression ParseNonString(string expressionString)
        {
            throw new NotImplementedException();
        }

        private static Dictionary<char, string> singleDelimiters = new() {
            { '"', "\"" },
            { '\'', "'" }
        };

        private static Dictionary<char, string> doubleDelimiters = new() {
            { '"', "\"\"" },
            { '\'', "''" }
        };

        private static Expression ParseString(string expressionString)
        {
            var delimiter = expressionString[0];
            if(expressionString[1] == delimiter) {
                if(expressionString.Length > 2) {
                    Throw("Extra content found after string");
                }
                else {
                    return new Expression();
                }
            }

            var hasEscapedDelimiter = false;
            if(expressionString.Contains(doubleDelimiters[delimiter])) {
                hasEscapedDelimiter = true;
                expressionString = expressionString.Replace(doubleDelimiters[delimiter], "\0");
            }

            var extraDelimiterIndex = expressionString.IndexOf(delimiter, 1);
            if(extraDelimiterIndex == -1) {
                Throw("Unterminated string");
            }
            else if(extraDelimiterIndex != expressionString.Length-1) {
                Throw("Extra content found after string");
            }

            if(hasEscapedDelimiter) {
                expressionString = expressionString.Replace("\0", singleDelimiters[delimiter]);
            }

            expressionString = expressionString[1..^1];
            return new Expression(new RawBytesOutput[] { new(OutputStringEncoding.GetBytes(expressionString)) });
        }

        public static Encoding OutputStringEncoding { get; set; }

        public static int NumericRadix { get; set; }

        public IExpressionPart[] Parts { get; private set; }

        private static void Throw(string message)
        {
            throw new InvalidExpressionException(message);
        }

        private static Dictionary<string, ArithmeticOperator> operators = new ArithmeticOperator[] {
            new AndOperator(),
            new DivideOperator(),
            new EqualsOperator(),
            new GreaterThanOperator(),
            new GreaterThanOrEqualOperator(),
            new HighOperator(),
            new LessThanOperator(),
            new LessThanOrEqualOperator(),
            new LowOperator(),
            new MinusOperator(),
            new ModOperator(),
            new MultiplyOperator(),
            new NotEqualsOperator(),
            new NotOperator(),
            new OrOperator(),
            new PlusOperator(),
            new ShiftLeftOperator(),
            new ShiftRightOperator(),
            new UnaryMinusOperator(),
            new UnaryPlusOperator(),
            new XorOperator()
        }.ToDictionary(x => x.Name);
    }
}
