namespace Konamiman.Nestor80.Linker
{
    public class LinkingResult
    {
        public string[] Warnings { get; set; }

        public string[] Errors { get; set; }

        public Dictionary<string, ushort> Symbols { get; set; }
    }
}
