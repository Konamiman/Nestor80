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

        public string ExtractAngleBracketed()
        {
            if(AtEndOfLine) {
                return null;
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


        private bool PhysicalEndOfLineReached => linePointer >= lineLength;

        private bool PointingToSpace() => !AtEndOfLine && (sourceLine[linePointer] == ' ' || sourceLine[linePointer] == '\t');

        private bool PointingToComma() => !AtEndOfLine && sourceLine[linePointer] == ',';

        public override string ToString() => "Walking: " + sourceLine;
    }
}
