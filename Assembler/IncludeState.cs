using Konamiman.Nestor80.Assembler.Output;

namespace Konamiman.Nestor80.Assembler
{
    /// <summary>
    /// Class used to hold state information while an INCLUDEd file is being processed.
    /// </summary>
    internal class IncludeState
    {
        public IncludeLine ProcessedLine { get; set; }

        public string PreviousFileName { get; set; }

        public List<ProcessedSourceLine> PreviousProcessedLines { get; set; }

        public int PreviousLineNumber { get; set; }

        public StreamReader PreviousSourceStreamReader { get; set; }
    }
}
