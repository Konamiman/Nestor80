using Konamiman.Nestor80.Assembler.Output;

namespace Konamiman.Nestor80.Assembler
{
    internal class IncludeState
    {
        public IncludeLine ProcessedLine { get; set; }

        public string PreviousFileName { get; set; }

        public List<ProcessedSourceLine> PreviousLines { get; set; }

        public int PreviousLineNumber { get; set; }

        public StreamReader PreviousSourceStreamReader { get; set; }
    }
}
