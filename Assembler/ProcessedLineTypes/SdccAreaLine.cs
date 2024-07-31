using Konamiman.Nestor80.Assembler.Output;
using Konamiman.Nestor80.Assembler.Relocatable;

namespace Konamiman.Nestor80.Assembler.ProcessedLineTypes
{
    public class SdccAreaLine : ProcessedSourceLine, IChangesLocationCounter
    {
        public SdccAreaLine() : this(null, false, false) { }

        public SdccAreaLine(string name, bool isAbsolute, bool isOverlay) {
            Area = new SdccArea(name, isAbsolute, isOverlay );
        }

        public SdccArea Area { get; }

        public string Name => Area.Name;

        public bool IsAbsolute => Area.IsAbsolute;

        public bool IsOverlay => Area.IsOverlay;

        public AddressType NewLocationArea { get; set; }
        public ushort NewLocationCounter { get; set; }

        public override string ToString() => Area.ToString();
    }
}
