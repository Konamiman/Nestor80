using Konamiman.Nestor80.Assembler.Output;
using Konamiman.Nestor80.Assembler.Relocatable;

namespace Konamiman.Nestor80.Assembler.ProcessedLineTypes
{
    public class IncbinLine : DefbLine
    {
        public IncbinLine() : base()
        {
            this.RelocatableParts = Array.Empty<RelocatableOutputPart>();
        }

        public string FullPath { get; set; }

        public string FileName { get; set; }
    }
}
