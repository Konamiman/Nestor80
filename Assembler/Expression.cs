using Konamiman.Nestor80.Assembler.ArithmeticOperations;
using System.Linq;
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

        public static Expression FromParts(IEnumerable<IExpressionPart> parts)
        {
            return new Expression(parts.ToArray());
        }

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

        /// <summary>
        /// Extract a number from the current parsed string pointer.
        /// </summary>
        /// <remarks>
        /// Input state:
        /// If isXDelimited = true lastExtractedChar is the opening ' of x'nnnn'
        /// If isXDelimited = false lastExtractedChar is the first digit of the number
        /// 
        /// There's a gotcha with the 'b' and 'd' suffixes when the default radix is 16:
        /// 
        /// .radix 10
        /// dw 4096d --> same as dw 4096
        /// dw 100b  --> same as dw 4
        /// 
        /// .radix 16
        /// dw 4096d --> same as dw 096dH (overflow is ignored)
        /// dw 100b  --> same as dw 100bH
        /// </remarks>
        /// <param name="isXDelimited"></param>
        /// <exception cref="InvalidExpressionException"></exception>
        private static void ExtractNumber(bool isXDelimited = false)
        {
            var radix = DefaultRadix;
            extractedChars.Clear();

            if(isXDelimited) {
                if(AtEndOfString) {
                    Throw ("Unterminated x'nnnn' number");
                }
                ExtractCharFromParsedString();
            }

            while(IsValidHexChar(lastExtractedChar)) {
                extractedChars.Add(lastExtractedChar);
                if(AtEndOfString) {
                    break;
                }
                ExtractCharFromParsedString();
            }

            if(isXDelimited) {
                if(AtEndOfString || lastExtractedChar != '\'') {
                    Throw("x'nnnn' number is unterminated or has invalid characters");
                }
                radix = 16;
            }
            else if(!IsValidNumericChar(lastExtractedChar, radix)) {
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
                    if(!IsValidNumericChar(extractedChars[extractedChars.Count - 1], radix)) {
                        extractedChars.RemoveAt(extractedChars.Count - 1);
                    }
                }
            }

            int value = 0;
            var extractedString = new string(extractedChars.ToArray());
            try {
                value = ParseNumber(extractedString, radix);
            } catch {
                Throw($"{extractedString} is not a valid base {radix} number");
            }

            var address = new Address(AddressType.ASEG, (ushort)(value & 0xFFFF));
            AddExpressionPart(address);
        }

        private static Dictionary<char, int> hexDigitValues = new() {
            { '0', 0 }, { '1', 1 }, { '2', 2 }, { '3', 3 }, { '4', 4 }, 
            { '5', 5 }, { '6', 6 }, { '7', 7 }, { '8', 8 }, { '9', 9 },
            { 'a', 10 }, { 'b', 11 }, { 'c', 12 }, { 'd', 13 }, { 'e', 14 },
            { 'A', 10 }, { 'B', 11 }, { 'C', 12 }, { 'D', 13 }, { 'E', 14 }
        };

        private static int ParseNumber(string number, int radix)
        {
            if(radix is 2 or 8 or 10 or 16) {
                return Convert.ToInt32(number, radix);
            }

            var result = 0;
            var power = number.Length - 1;
            for(int i = 0; i < number.Length; i++) {
                result += hexDigitValues[number[i]] * (int)Math.Pow(radix, power--);
            }

            return result;
        }
    
        private static void AddExpressionPart(IExpressionPart part)
        {
            parts.Add(part);
            lastExtractedPart = part;
        }

        private static bool IsValidNumericChar(char theChar, int radix)
        {
            theChar = char.ToUpper(theChar);
            return char.IsDigit(theChar) ||
                ((theChar = char.ToUpper(theChar)) >= 'A' && theChar <= 'A' + radix - 11);
        }

        private static bool IsValidHexChar(char theChar)
        {
            return char.IsDigit(theChar) ||
                (theChar >= 'a' && theChar <= 'f') ||
                (theChar >= 'A' && theChar <= 'F');
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

        private static int _DefaultRadix = 10;
        public static int DefaultRadix {
            get => _DefaultRadix;
            set {
                if(value is < 2 or > 16) {
                    throw new InvalidOperationException($"{nameof(Expression)}.{nameof(DefaultRadix)}: value must be between 2 and 16");
                }

                _DefaultRadix = value;
            }
        }

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

        public static bool operator ==(Expression expression1, Expression expression2)
        {
            if(expression1 is null)
                return expression2 is null;

            return expression1.Equals(expression2);
        }

        public static bool operator !=(Expression expression1, Expression expression2)
        {
            return !(expression1 == expression2);
        }

        public override bool Equals(object obj)
        {
            if(obj == null || GetType() != obj.GetType())
                return false;

            var b2 = (Expression)obj;

            if(Parts.Length != b2.Parts.Length) {
                return false;
            }

            for(int i = 0; i < Parts.Length; i++) {
                if(!Parts[i].Equals(b2.Parts[i])) return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            int result = 0;
            foreach(var part in Parts) {
                result ^= part.GetHashCode();
            }
            return result;
        }

        public override string ToString()
        {
            return String.Join(", ", Parts.Select(p => p.ToString()));
        }
    }
}
