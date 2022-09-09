namespace Konamiman.Nestor80.Assembler.Output
{
    internal class DelimitedCommandLine : ProcessedSourceLine
    {
        public char? Delimiter { get; set; }

        public bool IsFirstLine => Delimiter.HasValue && (!IsLastLine || Delimiter == '\0');

        public bool IsLastLine { get; set; }

        public override string ToString()
        {
            if(IsFirstLine && IsLastLine)
                return $"{base.ToString()}, invalid";
            else if(IsFirstLine)
                return $"{base.ToString()}, first, delimiter: {Delimiter}";
            else if(IsLastLine)
                return $"Multiline comment, last, {Delimiter}";
            else
                return "Multiline comment";
        }
    }
}
