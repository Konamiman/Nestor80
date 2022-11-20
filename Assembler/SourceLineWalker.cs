using System.Runtime.CompilerServices;

namespace Konamiman.Nestor80.Assembler
{
    internal class SourceLineWalker
    {
        const char COMMENT_START = ';';

        string sourceLine;
        int lineLength;
        int linePointer;
        bool logicalEndOfLineReached;
        int linePointerBackup;

        public static bool AllowEscapesInStrings = false;

        public string SourceLine => sourceLine;

        public SourceLineWalker(string sourceLine)
        {
            this.sourceLine = sourceLine;
            this.linePointer = 0;
            this.lineLength = sourceLine.Length;

            SkipBlanks();
        }

        public bool AtEndOfLine => logicalEndOfLineReached || CheckEndOfLine();

        public void Rewind()
        {
            linePointer = 0;
            SkipBlanks();
        }

        public void BackupPointer()
        {
            linePointerBackup = linePointer;
        }

        public void RestorePointer()
        {
            linePointer = linePointerBackup;
            logicalEndOfLineReached = false;
        }

        public string GetUntil(char terminator)
        {
            if(AtEndOfLine) return "";

            char ch;
            var chars = new List<char>();
            do {
                ch = sourceLine[linePointer++];
                chars.Add(ch);
            }
            while(!AtEndOfLine && ch != terminator);

            SkipBlanks();
            return new string(chars.ToArray());
        }

        public string GetRemainingRaw()
        {
            var text = linePointer >= sourceLine.Length ? "" : sourceLine[linePointer..];
            linePointer = sourceLine.Length;
            return text;
        }

        public string GetRemaining()
        {
            if(AtEndOfLine) {
                return "";
            }

            var remaining = sourceLine[linePointer..];
            linePointer = sourceLine.Length;
            return remaining;
        }
        

        /// <summary>
        /// Extracts a symbol from the source line.
        /// </summary>
        /// <remarks>A symbol is defined as a sequence of characters that are not spaces or tabs.</remarks>
        /// <returns>The extracted symbol.</returns>
        public string ExtractSymbol()
        {
            if(AtEndOfLine) {
                return null;
            }

            var originalPointer = linePointer;
            while(!AtEndOfLine && !PointingToSpace()) {
                linePointer++;
            }

            SkipBlanks();
            return sourceLine.Substring(originalPointer, linePointer - originalPointer).Trim();
        }

        /// <summary>
        /// Extracts an expression from the source line.
        /// </summary>
        /// <remarks>
        /// An expression is defined as a sequence of characters that are not a comma, 
        /// but commas (and the comment start symbol) can appear inside a string.
        /// 
        /// Rules for strings:
        /// 
        /// 1. They are delimited by a pair of " or ': "foo", 'foo'
        /// 
        /// 2. The other delimiter can appear in the string and doesn't need escaping:
        ///    "foo 'bar' fizz"
        ///    'foo "bar" fizz'
        /// 
        /// 3. The delimiter used can appear in the string if it's escaped by doubling it:
        ///    "foo ""bar"" fizz"
        ///    'foo ''bar'' fizz'
        /// </remarks>
        /// <returns>The extracted expression. Strings will include their delimiters.</returns>
        public string ExtractExpression()
        {
            var insideString = false;
            char stringDelimiter = '\0';
            var lastCharWasStringDelimiter = false;
            var lastCharWasBackslash = false;

            if(AtEndOfLine) {
                return null;
            }

            if(PointingToComma()) {
                linePointer++;
            }

            if(!AtEndOfLine && sourceLine[linePointer] is 'A' or 'a' && linePointer <= lineLength - 3 && sourceLine[linePointer+1] is 'F' or 'f' && sourceLine[linePointer+2] is '\'') {
                // Ugly hack to recognize AF' as a symbol and not as AF followed by a string start
                var result = sourceLine.Substring(linePointer, 3);
                linePointer += 3;
                SkipBlanks();
                return result;
            }

            var originalPointer = linePointer;
            while(
                (insideString && !PhysicalEndOfLineReached) ||
                (!insideString && !AtEndOfLine && !PointingToComma())) {
                var currentChar = sourceLine[linePointer];
                if(!insideString) {
                    if(currentChar == '"' || currentChar == '\'') {
                        insideString = true;
                        stringDelimiter = currentChar;
                    }
                }
                else if(currentChar == '\\' && AllowEscapesInStrings && !lastCharWasBackslash) {
                    lastCharWasBackslash = true;
                }
                else if(currentChar == stringDelimiter) {
                    if(lastCharWasBackslash) {
                        lastCharWasBackslash = false;
                    }
                    else if(!lastCharWasStringDelimiter) {
                        insideString = false;
                        lastCharWasStringDelimiter = false;
                    }
                    else {
                        lastCharWasStringDelimiter = true;
                    }
                }
                else {
                    lastCharWasStringDelimiter = false;
                    lastCharWasBackslash = false;
                }

                linePointer++;
            }

            var expression = sourceLine.Substring(originalPointer, linePointer - originalPointer).Trim();

            SkipBlanks();
            return expression;
        }

        public string ExtractAngleBracketedOrSymbol()
        {
            if(AtEndOfLine) {
                return null;
            }

            if(PointingToComma()) {
                linePointer++;
                SkipBlanks();
                if(AtEndOfLine) {
                    return null;
                }
            }

            return sourceLine[linePointer] == '<' ? ExtractAngleBracketed() : ExtractSymbol();
        }

        public string ExtractAngleBracketed()
        {
            if(AtEndOfLine) {
                return null;
            }

            if(PointingToComma()) {
                linePointer++;
                SkipBlanks();
                if(AtEndOfLine) {
                    return null;
                }
            }

            var currentChar = sourceLine[linePointer];
            if(currentChar != '<') {
                return null;
            }

            var originalPointer = ++linePointer;
            while(!AtEndOfLine) {
                currentChar = sourceLine[linePointer];
                if(currentChar == '>') {
                    var text = sourceLine[originalPointer..linePointer];
                    linePointer++;
                    SkipBlanks();
                    return text;
                }
                linePointer++;
            }

            return null;
        }

        public bool SkipComma()
        {
            if(AtEndOfLine) {
                return false;
            }

            if(sourceLine[linePointer] == ',') {
                linePointer++;
                SkipBlanks();
                return true;
            }

            return false;
        }

        public void SkipBlanks()
        {
            while(PointingToSpace()) {
                linePointer++;
            }
        }

        private bool CheckEndOfLine()
        {
            if(!logicalEndOfLineReached) {
                logicalEndOfLineReached = PhysicalEndOfLineReached || sourceLine[linePointer] == COMMENT_START;
            }

            return logicalEndOfLineReached;
        }

        public int DiscardRemaining()
        {
            while(!AtEndOfLine) ExtractExpression();
            return EffectiveLength;
        }

        public int EffectiveLength
        { 
            get
            {
                if(!CheckEndOfLine())
                    throw new InvalidOperationException($"{nameof(SourceLineWalker)}.{nameof(EffectiveLength)} can't be invoked before reaching the end of the line. Walked line: {sourceLine}");

                return linePointer;
            }
        }

        /// <summary>
        /// If the currently pointer character is " then characters are extracted until the next ",
        /// but "" are considered as one single " part of the name.
        /// 
        /// If the currently pointer character is not " then this is equivalent to ExtractSymbol
        /// (extracts characters until a space is found).
        /// </summary>
        /// <returns></returns>
        public string ExtractFileName()
        {
            if(AtEndOfLine) {
                return null;
            }

            if(sourceLine[linePointer] != '"') {
                return ExtractSymbol();
            }

            var originalPointer = ++linePointer;
            var previousWasQuote = false;
            char currentChar = '\0';
            while(!AtEndOfLine) {
                currentChar = sourceLine[linePointer];
                if(currentChar == '"') {
                    previousWasQuote = !previousWasQuote;
                }
                else if(previousWasQuote) {
                    break;
                } else { 
                    previousWasQuote = false;
                }
                linePointer++;
            }

            var endPointer = previousWasQuote ? linePointer - 1 : linePointer;
            var line = sourceLine[originalPointer..endPointer].Replace("\"\"", "\"");
            linePointer++;
            SkipBlanks();
            return line;
        }

        //TODO: handle tabs too
        public (string[],int) ExtractArgsListForIrp()
        {
            SkipBlanks();
            if(PhysicalEndOfLineReached || !PointingToLessThan()) return (null,0);

            var delimiterNestingLevel = 1;
            var extractingExpression = false;
            var nextCharIsLiteral = false;
            var spaceFoundAfterArg = false;
            var args = new List<string>();
            var chars = new List<char>();
            char theChar = ' ';

            void RegisterArg()
            {
                if(extractingExpression) {
                    chars.Insert(0, '\u0001');
                    extractingExpression = false;
                }
                var arg = new string(chars.ToArray());
                args.Add(arg);
                chars.Clear();
                spaceFoundAfterArg = theChar is ' ';
            }

            linePointer++; //Skip opening '<'
            while(true) {
                if(PhysicalEndOfLineReached) {
                    RegisterArg();
                    break;
                }

                theChar = sourceLine[linePointer];
                linePointer++;

                if(extractingExpression) {
                    if(theChar is ',') {
                        RegisterArg();
                        continue;
                    } 
                    else if(theChar is '>' && delimiterNestingLevel is 1) {
                        RegisterArg();
                        break;
                    }
                    else {
                        chars.Add(theChar);
                        continue;
                    }
                }

                if(nextCharIsLiteral) {
                    chars.Add(theChar);
                    nextCharIsLiteral = false;
                    continue;
                }

                if(spaceFoundAfterArg) {
                    spaceFoundAfterArg = false;
                    if(theChar is ',') {
                        args.Add("");
                        continue;
                    }
                }

                if(theChar is '>') {
                    delimiterNestingLevel--;
                    if(delimiterNestingLevel == 0) {
                        RegisterArg();
                        break;
                    }
                    else if(delimiterNestingLevel == 1) {
                        continue;
                    }
                    chars.Add(theChar);
                    continue;
                }

                if(theChar is '<') {
                    nextCharIsLiteral = false;
                    if(delimiterNestingLevel > 1) {
                        chars.Add(theChar);
                    }
                    delimiterNestingLevel++;
                    continue;
                }

                if(theChar is '!') {
                    nextCharIsLiteral = true;
                    continue;
                }

                if(delimiterNestingLevel > 1) {
                    chars.Add(theChar);
                    continue;
                }

                if(chars.Count == 0) {
                    if(theChar is ' ') {
                        continue;
                    }
                    else if(theChar is '%') {
                        extractingExpression = true;
                        continue;
                    }
                }

                if(theChar is ',' or ' ') {
                    RegisterArg();
                    continue;
                }

                chars.Add(theChar);
            }

            SkipBlanks();
            return (args.ToArray(), delimiterNestingLevel);
        }

        //TODO: handle tabs too
        public (string[], int) ExtractArgsListForIrpc()
        {
            SkipBlanks();

            char theChar;
            var args = new List<string>();
            var delimiterNestingLevel = PointingToLessThan() ? 1 : 0;

            if(delimiterNestingLevel == 1) {
                linePointer++;
            }

            while(true) {
                if(PhysicalEndOfLineReached) {
                    break;
                }

                theChar = sourceLine[linePointer];
                linePointer++;

                if(theChar == ' ' && delimiterNestingLevel == 0) {
                    break;
                }

                if(theChar == '>' && delimiterNestingLevel > 0) {
                    delimiterNestingLevel--;
                    if(delimiterNestingLevel == 0) {
                        break;
                    }
                }

                if(theChar == '<' && delimiterNestingLevel > 0) {
                    delimiterNestingLevel++;
                }

                args.Add(theChar.ToString());
            }

            return (args.ToArray(), delimiterNestingLevel);
        }

        private bool PhysicalEndOfLineReached => linePointer >= lineLength;

        private bool PointingToSpace() => !AtEndOfLine && (sourceLine[linePointer] == ' ' || sourceLine[linePointer] == '\t');

        private bool PointingToComma() => !AtEndOfLine && sourceLine[linePointer] == ',';

        private bool PointingToLessThan() => !AtEndOfLine && sourceLine[linePointer] == '<';

        public override string ToString() => "Walking: " + sourceLine;
    }
}
