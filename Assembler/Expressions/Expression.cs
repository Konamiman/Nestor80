using Konamiman.Nestor80.Assembler.ArithmeticOperations;
using Konamiman.Nestor80.Assembler.Expressions;
using Konamiman.Nestor80.Assembler.Expressions.ArithmeticOperations;
using Konamiman.Nestor80.Assembler.Output;
using System.Text;
using System.Text.RegularExpressions;

namespace Konamiman.Nestor80.Assembler
{
    internal partial class Expression {
        private Expression(IExpressionPart[] parts = null, string source = null)
        {
            this.Parts = parts ?? Array.Empty<IExpressionPart>();
            this.Source = source;
        }

        const RegexOptions RegxOp = RegexOptions.Compiled | RegexOptions.IgnoreCase;

        private static readonly Dictionary<int, Regex> numberRegexes = new();
        private static readonly Regex xNumberRegex = new("(?<=x')[0-9a-f]*(?=')", RegxOp);
        private static readonly Regex symbolRegex = new("(?<root>:?)(?<symbol>[\\w$@?.]+)(?<external>(##)?)", RegxOp);
        private static readonly Dictionary<char, Regex> unescapedStringRegexes = new() {
            {'\'', new Regex("(?<=')((?<quot>'')|[^'])*(?=')", RegxOp) },
            {'"',  new Regex("(?<=\")((?<quot>\"\")|[^\"])*(?=\")", RegxOp) },
        };
        private static readonly Dictionary<char, Regex> escapedStringRegexes = new() {
            {'\'', new Regex("(?<=')((?<quot>\\\\')|[^'])*(?=')", RegxOp) },
            {'"',  new Regex("(?<=\")((?<quot>\\\\\")|[^\"])*(?=\")", RegxOp) },
        };

        private static Regex currentRadixRegex;
        private static string parsedString = null;
        private static int parsedStringLength = 0;
        private static int parsedStringPointer = 0;
        private static bool extractingForDb = false;
        private static readonly List<IExpressionPart> parts = new();
        private static IExpressionPart lastExtractedPart = null;
        private bool isByte;

        private static bool AtEndOfString = false;

        private static readonly Dictionary<int, byte[]> ZeroCharBytesByEncoding = new();

        public static byte[] ZeroCharBytes { get; private set; }

        public bool HasRelocatableToStoreAsByte { get; private set; }

        private static Encoding _OutputStringEncoding;
        public static Encoding OutputStringEncoding
        {
            get => _OutputStringEncoding;
            set
            {
                _OutputStringEncoding = value;
                if(!ZeroCharBytesByEncoding.ContainsKey(value.CodePage)) {
                    ZeroCharBytesByEncoding[value.CodePage] = value.GetBytes("\0");
                }
                ZeroCharBytes = ZeroCharBytesByEncoding[value.CodePage];
            }
        }


        public static Func<string, bool, bool, SymbolInfo> GetSymbol { get; set; } = (_, _, _) => null;

        public IExpressionPart[] Parts { get; private set; }

        static Expression()
        {
            DefaultRadix = 10;
            operators["NEQ"] = NotEqualsOperator.Instance;
        }

        public string Source { get; init; }

        public static bool AllowEscapesInStrings = false;

        private static int _DefaultRadix;
        public static int DefaultRadix
        {
            get => _DefaultRadix;
            set
            {
                if(value is < 2 or > 16) {
                    throw new InvalidOperationException($"{nameof(Expression)}.{nameof(DefaultRadix)}: value must be between 2 and 16");
                }

                _DefaultRadix = value;

                if(numberRegexes.ContainsKey(value)) {
                    currentRadixRegex = numberRegexes[value];
                    return;
                }

                var extraBinarySuffix = value < 12 ? "b" : "";
                var extraDecimalSuffix = value < 14 ? "d" : "";
                var extraAllowedDigits = value switch {
                    < 11 => $"{value - 1}",
                    11 => "9a",
                    _ => $"9a-{(char)('a' + value - 11)}"
                };

                currentRadixRegex = new Regex(
                    $"(#(?<number_hex_hash>[0-9a-f]+))|" +
                    $"((?<number_hex>[0-9a-f]+)h)|" +
                    $"(%(?<number_bin_percent>[01]+))|" +
                    $"((?<number_bin>[01]+)[{extraBinarySuffix}i])|" +
                    $"((?<number_dec>[0-9]+)[{extraDecimalSuffix}m])|" +
                    $"((?<number_oct>[0-7]+)[oq])|" +
                    $"((?<number>[0-{extraAllowedDigits}]+)(?![hbdmi0-{extraAllowedDigits}]))",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled);

                numberRegexes[value] = currentRadixRegex;
            }
        }

        private static readonly Dictionary<char, string> OperatorsAsStrings = new() {
            { '+', "+" }, { '-', "-" }, { '*', "*" },
            { '/', "/" }, { '(', "(" }, { ')', ")" },
            { '=', "=" }
        };

        private static readonly Dictionary<char, string> UnaryOperatorsAsStrings = new() {
            { '+', "u+" }, { '-', "u-" },
        };

        public static Expression FromParts(params IExpressionPart[] parts)
        {
            return new Expression(parts.ToArray());
        }

        public static Expression Empty => FromParts();

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
        /// <param name="isByte">True if the expression is intended to be evaluated as a byte.
        /// This is used to determine if the expression can be evaluated or needs to be stored
        /// as link items when it involves relocatable addresses.</param>
        /// 
        /// <returns>An expression object</returns>
        /// <exception cref="ArgumentException">The supplied string is too short</exception>
        /// <exception cref="InvalidExpressionException">The supplied string doesn't represent a valid expression</exception>
        public static Expression Parse(string expressionString, bool forDefb = false, bool isByte = false)
        {
            if(OutputStringEncoding is null) {
                throw new InvalidOperationException($"{nameof(Expression)}.{nameof(Parse)}: { nameof(OutputStringEncoding)} is null");
            }

            if(expressionString[0] is ' ' or '\t' || expressionString[^1] is ' ' or '\t') {
                throw new ArgumentException($"The string passed to {nameof(Expression)}.{nameof(Parse)} isn't supposed to contain spaces/tabs at the beginning or the end");
            }

            if(string.IsNullOrWhiteSpace(expressionString)) {
                throw new ArgumentNullException(nameof(expressionString));
            }

            parts.Clear();

            parsedString = expressionString;
            parsedStringPointer = 0;
            parsedStringLength = expressionString.Length;
            extractingForDb = forDefb;
            lastExtractedPart = null;

            AtEndOfString = false;

            while(!AtEndOfString)
                ExtractNextPart();

            return new Expression(parts.ToArray(), expressionString) { isByte = isByte };
        }

        private static void ExtractNextPart()
        {
            while(parsedString[parsedStringPointer] is ' ' or '\t')
                IncreaseParsedStringPointer();

            var currentChar = parsedString[parsedStringPointer];

            if(char.IsDigit(currentChar) || currentChar is '#' or '%') {
                ExtractNumber();
            }
            else if(currentChar is 'x' or 'X' && parsedStringPointer < parsedStringLength - 2 && parsedString[parsedStringPointer + 1] == '\'') {
                ExtractXNumber();
            }
            else if(currentChar is '"' or '\'') {
                ExtractString(currentChar);
            }
            else if(currentChar is '+' or '-') {
                ProcessPlusOrMinus(currentChar);
            }
            else if(currentChar is '*' or '/' or '(' or ')' or '=') {
                ProcessOperator(OperatorsAsStrings[currentChar]);
            }
            else if(currentChar is ':' || IsValidSymbolChar(currentChar)) {
                ExtractSymbolOrOperator();
            }
            else {
                Throw($"Unexpected character found: {currentChar}");
            }
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
            Match match = null;
            try {
                match = currentRadixRegex.Match(parsedString, parsedStringPointer);
            }
            catch {
                Throw("Invalid number");
            }

            if(!match.Success) {
                Throw("Invalid number");
            }

            var matchKey = match.Groups.Keys.FirstOrDefault(k => k[0] == 'n' && match.Groups[k].Success);
            if(matchKey is null) {
                Throw("Invalid number");
            }

            var extractedNumber = match.Groups[matchKey].Value;
            var radix = matchKey switch {
                "number" => DefaultRadix,
                "number_hex" => 16,
                "number_hex_hash" => 16,
                "number_bin" => 2,
                "number_bin_percent" => 2,
                "number_dec" => 10,
                "number_oct" => 8,
                _ => throw new InvalidOperationException($"Something weird happened in {nameof(Expression)}.{nameof(ExtractNumber)}: got unknown regex group name, {matchKey}")
            };

            ParseAndRegisterNumber(extractedNumber, radix, increaseStringPointerBy: match.Length);
        }

        private static void ExtractXNumber()
        {
            Match match = null;
            try {
                match = xNumberRegex.Match(parsedString, parsedStringPointer);
            }
            catch {
                Throw("Invalid X'' number");
            }

            if(!match.Success) {
                Throw("Invalid X'' number");
            }

            var extractedNumber = match.Value;
            ParseAndRegisterNumber(
                extractedNumber, 
                radix: 16,
                increaseStringPointerBy: match.Length + 3); //+3 to include the opening X' and the closing ' that were excluded from the match
        }

        private static void ParseAndRegisterNumber(string numberString, int radix, int increaseStringPointerBy)
        {
            int value = 0;
            if(numberString.Length > 0) {
                try {
                    value = ParseNumber(numberString, radix);
                }
                catch {
                    Throw($"Invalid number");
                }
            }

            IncreaseParsedStringPointer(increaseStringPointerBy);
            if(!AtEndOfString && (parsedString[parsedStringPointer] is not ' ' and not '\t' and not '+' and not '-' and not '*' and not '/' and not ')' and not '=')) {
                Throw($"Unexpected character found after number: {parsedString[parsedStringPointer]}");
            }

            //TODO: Warning if truncated value
            var address = Address.Absolute((ushort)(value & 0xFFFF));
            AddExpressionPart(address);
        }

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

        public static string NumberToStringInCurrentRadix(int number)
        {
            string result;
            if(DefaultRadix is 2 or 8 or 10 or 16) {
                result = Convert.ToString(number, DefaultRadix).ToUpper();
            }
            else {
                int i = 16;
                char[] buffer = new char[16];

                if(number % DefaultRadix > 9) {
                    buffer[--i] = '0';
                }
                do {
                    buffer[--i] = hexChars[number % DefaultRadix];
                    number /= DefaultRadix;
                }
                while(number > 0);

                var resultLength = 16 - i;
                char[] resultChars = new char[16 - i];
                Array.Copy(buffer, i, resultChars, 0, resultLength);

                result = new string(resultChars);
            }

            return result[0] > '9' ? "0" + result : result;
        }

        private static void ProcessPlusOrMinus(char currentChar)
        {
            if(lastExtractedPart is null or ArithmeticOperator or OpeningParenthesis)
                ProcessOperator(UnaryOperatorsAsStrings[currentChar]);
            else
                ProcessOperator(OperatorsAsStrings[currentChar]);
        }

        public static bool IsValidSymbolChar(char theChar)
        {
            return char.IsLetter(theChar) || theChar is '?' or '_' or '@' or '.' or '$';
        }

        private static void ExtractString(char delimiter)
        {
            if(parsedStringPointer == parsedStringLength-1) {
                // This covers the edge case of a stray ' or " at the end of the string,
                // e.g. 'ABC''
                Throw("Unterminated string");
            }

            var escapesAllowed = delimiter == '"' && AllowEscapesInStrings;

            Match match = null;
            try {
                var regexes = escapesAllowed ? escapedStringRegexes : unescapedStringRegexes;
                match = regexes[delimiter].Match(parsedString, parsedStringPointer);
            }
            catch {
                Throw("Invalid string");
            }

            if(!match.Success) {
                Throw("Unterminated string");
            }

            var theString = match.Value;
            var matchLength = theString.Length;
            if(escapesAllowed) {
                try {
                    theString = Regex.Unescape(theString);
                }
                catch(Exception ex) {
                    Throw($"Error when parsing string: {ex.Message}");
                }
            }
            else if(match.Groups["quot"].Success) {
                theString = theString.Replace(doubleDelimiters[delimiter], singleDelimiters[delimiter]);
            }

            byte[] stringBytes = null;
            try {
                stringBytes = OutputStringEncoding.GetBytes(theString);
            }
            catch(EncoderFallbackException) {
                Throw($"string contains characters that aren't supported by the current encoding ({OutputStringEncoding.WebName})");
            }

            if(extractingForDb) {
                AddExpressionPart(new RawBytesOutput(stringBytes.Length > 0 ? stringBytes : Array.Empty<byte>(), theString));
            }
            else {
                if(stringBytes.Length > 2) {
                    Throw($"The string \"{theString}\" generates more than two bytes in the current output encoding ({OutputStringEncoding.EncodingName})");
                }
                var value = RawBytesOutput.NumericValueFor(stringBytes);
                AddExpressionPart(Address.Absolute(value));
            }

            IncreaseParsedStringPointer(matchLength + 2); // +2 for the delimiters
        }

        private static void ProcessOperator(string theOperator)
        {
            if(theOperator is "=") {
                AddExpressionPart(operators["EQ"]);
            }
            else if(theOperator is "(") {
                AddExpressionPart(OpeningParenthesis.Value);
            }
            else if(theOperator is ")") {
                AddExpressionPart(ClosingParenthesis.Value);
            }
            else {
                AddExpressionPart(operators[theOperator]);
            }
            
            IncreaseParsedStringPointer(theOperator is "u+" or "u-" ? 1 : theOperator.Length);
        }

        private static void ExtractSymbolOrOperator()
        {
            Match match = null;
            try {
                match = symbolRegex.Match(parsedString, parsedStringPointer);
            }
            catch {
                Throw("Invalid symbol");
            }

            if(!match.Success) {
                Throw("Invalid symbol");
            }

            var symbol = match.Groups["symbol"].Value;
            var isExternalRef = match.Groups["external"].Length > 0;
            var isRoot = match.Groups["root"].Length > 0;

            if(symbol == "$") {
                var currentLocationSymbolRef = GetSymbol("$", false, false);
                AddExpressionPart(currentLocationSymbolRef.Value);
                IncreaseParsedStringPointer(1);
                return;
            }

            if(string.Equals(symbol, "NUL", StringComparison.OrdinalIgnoreCase)) {
                /*
                 * The NUL operator is a special case.
                 * If it's the last item in the expression it evaluates to -1.
                 * Otherwise it evaluates to 0 and the remaning of the expression is discarded.
                 */ 
                IncreaseParsedStringPointer(3); //To make AtEndOfString accurate
                var part = AtEndOfString ? Address.AbsoluteMinusOne : Address.AbsoluteZero;
                AddExpressionPart(part);
                AtEndOfString = true; //This will cause the rest of the expression to be discarded
                return;
            }

            var theOperator = operators.GetValueOrDefault(symbol);
            if(theOperator is null) {
                var part = new SymbolReference() { SymbolName = symbol, IsExternal = isExternalRef, IsRoot = isRoot };
                AddExpressionPart(part);
                IncreaseParsedStringPointer(match.Length);
            }
            else if(isExternalRef) {
                Throw($"{symbol.ToUpper()} is an operator, can't be used as external symbol reference");
            } else { 
                ProcessOperator(symbol);
            }
        }

        private static readonly Dictionary<char, int> hexDigitValues = new() {
            { '0', 0 }, { '1', 1 }, { '2', 2 }, { '3', 3 }, { '4', 4 }, 
            { '5', 5 }, { '6', 6 }, { '7', 7 }, { '8', 8 }, { '9', 9 },
            { 'a', 10 }, { 'b', 11 }, { 'c', 12 }, { 'd', 13 }, { 'e', 14 },
            { 'A', 10 }, { 'B', 11 }, { 'C', 12 }, { 'D', 13 }, { 'E', 14 }
        };

        private static readonly char[] hexChars = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };


        private static void AddExpressionPart(IExpressionPart part)
        {
            parts.Add(part);
            lastExtractedPart = part;
        }

        private static bool IsValidHexChar(char theChar)
        {
            return char.IsDigit(theChar) ||
                (theChar >= 'a' && theChar <= 'f') ||
                (theChar >= 'A' && theChar <= 'F');
        }

        private static readonly Dictionary<char, string> singleDelimiters = new() {
            { '"', "\"" }, { '\'', "'" }
        };

        private static readonly Dictionary<char, string> doubleDelimiters = new() {
            { '"', "\"\"" }, { '\'', "''" }
        };

        private static void IncreaseParsedStringPointer(int amount = 1)
        {
            parsedStringPointer += amount;

            if(parsedStringPointer == parsedStringLength) {
                AtEndOfString = true;
            }
            else if(parsedStringPointer == parsedStringLength) {
                throw new InvalidOperationException($"{nameof(Expression)}.{nameof(IncreaseParsedStringPointer)}: parsed string length is {parsedStringLength} and the string pointer was set to {parsedStringPointer}");
            }
        }

        private static void Throw(string message, AssemblyErrorCode errorCode = AssemblyErrorCode.InvalidExpression)
        {
            throw new InvalidExpressionException(message, errorCode);
        }

        private static readonly Dictionary<string, ArithmeticOperator> operators = new ArithmeticOperator[] {
            AndOperator.Instance,
            DivideOperator.Instance,
            EqualsOperator.Instance,
            GreaterThanOperator.Instance,
            GreaterThanOrEqualOperator.Instance,
            HighOperator.Instance,
            LessThanOperator.Instance,
            LessThanOrEqualOperator.Instance,
            LowOperator.Instance,
            MinusOperator.Instance,
            ModOperator.Instance,
            MultiplyOperator.Instance,
            NotEqualsOperator.Instance,
            NotOperator.Instance,
            OrOperator.Instance,
            PlusOperator.Instance,
            ShiftLeftOperator.Instance,
            ShiftRightOperator.Instance,
            UnaryMinusOperator.Instance,
            UnaryPlusOperator.Instance,
            XorOperator.Instance,
            TypeOperator.Instance,
        }.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

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
