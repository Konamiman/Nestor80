using Konamiman.Nestor80.Assembler.ArithmeticOperations;
using System.Text;

namespace Konamiman.Nestor80.Assembler
{
    internal class Expression : IAssemblyOutputPart
    {
        private Expression(IExpressionPart[] parts = null)
        {
            this.Parts = parts ?? Array.Empty<IExpressionPart>();
        }

        private static string parsedString = null;
        private static int parsedStringLength = 0;
        private static int parsedStringPointer = 0;
        private static char lastExtractedChar = '\0';
        private static List<IExpressionPart> parts = new();
        private static IExpressionPart lastExtractedPart = null;
        private static List<char> extractedChars = new();

        private static char ExtractCharFromParsedString()
        {
            if(AtEndOfString) {
                Throw("End of expression reached unexpectedly");
            }

            lastExtractedChar = parsedString[parsedStringPointer++];
            if(parsedStringPointer == parsedStringLength) {
                AtEndOfString = true;
            }
            return lastExtractedChar;
        }

        private static void RewindToPreviousChar()
        {
            if(parsedStringPointer == 0) {
                throw new Exception($"{nameof(Expression)}.{nameof(RewindToPreviousChar)} invoked with {nameof(parsedStringPointer)} = 0, expression: {parsedString}");
            }

            parsedStringPointer--;
            AtEndOfString = false;
        }

        private static bool AtEndOfString = false;

        private static Dictionary<char, string> OperatorsAsStrings = new() {
            { '*', "*" },
            { '/', "/" }
        };

        /// <summary>
        /// Parse a string representing an expression.
        /// </summary>
        /// <remarks>
        /// The behavior of strings inside expressions depends of the length of the strings
        /// (in bytes after being converted with OutputStringEncoding) and the value of forDefb:
        /// 
        /// - 0 or 1 byte strings: they are converted to a 1 byte number, 
        ///                        and are always allowed as part of composite expressions (e.g. 'A'+1 => 42h)
        ///                        provided that they don't overflow.
        /// - 2 byte strings:
        ///   When forDefb is false: they are converted to a number, low byte first (e.g. 'AB' => 42h 41h),
        ///                          and being part of composite expressions is allowed (e.g. 'AB'+1 => 43h 41h) 
        ///   When forDefb is true:  they are converted to a number, high byte first (e.g. 'AB' => 41h 42h),
        ///                          and being part of composite expressions is not allowed
        ///                          (the string itself must be the entire expression)
        /// - 3+ byte strings: allowed only when forDefb is true, and can't be part of a composite expression
        /// 
        /// An empty string ('' or "") produces nothing when forDefb is true, and the number 0 when it's false.
        ///                    
        /// </remarks>
        /// <param name="expressionString">The expression string to parse</param>
        /// <param name="forDefb">Whether the expression is part of a DEFB statement</param>
        /// <returns>An expression object</returns>
        /// <exception cref="ArgumentException">The supplied string is too short</exception>
        /// <exception cref="InvalidExpressionException">The supplied string doesn't represent a valid expression</exception>
        public static Expression Parse(string expressionString, bool forDefb = false)
        {
            if(expressionString == null) {
                throw new ArgumentNullException(nameof(expressionString));
            }
            if(expressionString == "") {
                return new Expression();
            }

            parts.Clear();

            parsedString = expressionString;
            parsedStringPointer = 0;
            parsedStringLength = expressionString.Length;
            lastExtractedChar = '\0';
            bool insideString = false;
            char stringDelimiter = '\0';
            bool lastCharWasStringDelimiter = false;
            int stringstartPointer = 0;
            int stringEndPointer = 0;
            lastExtractedPart = null;

            AtEndOfString = false;

            while(!AtEndOfString) {
                SkipBlanks();

                if(char.IsDigit(lastExtractedChar)) {
                    ExtractNumber();
                    continue;
                }

                if(lastExtractedChar is 'x' or 'X') {
                    if(AtEndOfString) {
                        RewindToPreviousChar();
                        ExtractSymbolOrOperator();
                        continue;
                    }

                    ExtractCharFromParsedString();
                    if(lastExtractedChar == '\'') {
                        ExtractNumber(isXDelimited: true);
                        continue;
                    }
                    else {
                        RewindToPreviousChar();
                        RewindToPreviousChar();
                        ExtractSymbolOrOperator();
                        continue;
                    }
                }

                if(lastExtractedChar is '"' or '\'') {
                    ProcessString(forDefb);
                    continue;
                }

                if(lastExtractedChar == '-') {
                    ProcessMinus();
                }
                else if(lastExtractedChar == '+') {
                    ProcessPlus();
                }
                else if(lastExtractedChar is '*' or '/' or '(' or ')') {
                    ProcessOperator(OperatorsAsStrings[lastExtractedChar]);
                }
                else if(IsValidSymbolChar(lastExtractedChar)) {
                    RewindToPreviousChar();
                    ExtractSymbolOrOperator();
                }

                Throw($"Unexpected character found: {lastExtractedChar}");
            }


            return new Expression(parts.ToArray());
        }

        private static bool IsValidSymbolChar(char theChar)
        {
            return char.IsLetter(theChar) || theChar is '?' or '_' or '@' or '.' or '$';
        }

        private static string ProcessString(bool forDefb)
        {
            throw new NotImplementedException();
        }

        private static void ProcessOperator(string theOperator)
        {
            throw new NotImplementedException();
        }

        private static void SkipBlanks()
        {
            while(!AtEndOfString && ExtractCharFromParsedString() is ' ' or '\t') ;
        }

        private static void ProcessMinus()
        {
            throw new NotImplementedException();
        }

        private static void ProcessPlus()
        {
            throw new NotImplementedException();
        }

        private static void ExtractSymbolOrOperator()
        {
            throw new NotImplementedException();
        }

        private static void ExtractNumber(bool isXDelimited = false)
        {
            var radix = DefaultRadix;
            extractedChars.Clear();

            while(IsValidHexDigit(lastExtractedChar)) {
                extractedChars.Add(lastExtractedChar);
                if(AtEndOfString) {
                    break;
                }
                ExtractCharFromParsedString();
            }

            if(isXDelimited) {
                if(AtEndOfString || lastExtractedChar != '\'') {
                    Throw("Hex number in the form x'nnnn' is unterminated or has invalid characters");
                }
                radix = 16;
            }
            else if(!AtEndOfString) {
                if(lastExtractedChar is ' ' or '\t' or '+' or '-' or '*' or '/') {
                    RewindToPreviousChar();
                }
                else {
                    radix = lastExtractedChar switch {
                        'h' or 'H' => 16,
                        'd' or 'D' => 10,
                        'b' or 'B' => 2,
                        'o' or 'O' or 'q' or 'Q' => 8,
                        _ => throw new InvalidExpressionException($"Unexpected character found: {lastExtractedChar}")
                    };
                }
            }

            int value = 0;
            var extractedString = new string(extractedChars.ToArray());
            try {
                value = Convert.ToInt32(extractedString, radix);
            } catch {
                Throw($"{extractedString} is not a valid base {radix} number");
            }

            var address = new Address(AddressType.ASEG, (ushort)(value & 0xFFFF));
            AddExpressionPart(address);
        }
    
        private static void AddExpressionPart(IExpressionPart part)
        {
            parts.Add(part);
            lastExtractedPart = part;
        }

        private static bool IsValidHexDigit(char theChar)
        {
            return char.IsDigit(theChar) || theChar is >= 'a' and <= 'f' || theChar is >= 'A' and <= 'F';
        }

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

        public static int DefaultRadix { get; set; } = 10;

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
