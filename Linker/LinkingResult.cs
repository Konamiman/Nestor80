using Konamiman.Nestor80.Linker.Parsing;

namespace Konamiman.Nestor80.Linker
{
    public class LinkingResult
    {
        public string[] Warnings { get; set; }

        public string[] Errors { get; set; }

        public ProgramData[] ProgramsData { get; set; }

        public ushort StartAddress { get; set; }

        public ushort EndAddress { get; set; }
    }
}
