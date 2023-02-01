using Konamiman.Nestor80.Linker.Parsing;

namespace Konamiman.Nestor80.Linker
{
    public class LinkingResult
    {
        public string[] Warnings { get; set; }

        public string[] Errors { get; set; }

        public Dictionary<string, ushort> Symbols { get; set; }

        public AddressRange[] Areas { get; set; }

        public ushort StartAddress { get; set; }

        public ushort EndAddress { get; set; }
    }
}
