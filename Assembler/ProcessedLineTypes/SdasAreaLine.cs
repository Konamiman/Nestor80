using Konamiman.Nestor80.Assembler.Output;
using Konamiman.Nestor80.Assembler.Relocatable;

namespace Konamiman.Nestor80.Assembler.ProcessedLineTypes
{
    public class SdasAreaLine : ProcessedSourceLine, IChangesLocationCounter
    {
        public SdasAreaLine() : this(null, false, false) { }

        public SdasAreaLine(string name, bool isAbsolute, bool isOverlay) {
            Area = new SdasArea(name, isAbsolute, isOverlay );
        }

        public SdasArea Area { get; }

        public string Name => Area.Name;

        public bool IsAbsolute => Area.IsAbsolute;

        public bool IsOverlay => Area.IsOverlay;

        public AddressType NewLocationArea { get; set; }
        public ushort NewLocationCounter { get; set; }

        public override string ToString() => Area.ToString();
    }
}
