namespace Konamiman.Nestor80.Linker.Parsing
{
    /// <summary>
    /// Represents a relocatable program that has been extracted
    /// from a relocatable (or library) file.
    /// </summary>
    public class ParsedProgram
    {
        public string ProgramName { get; set; }

        public IRelocatableFilePart[] Parts { get; set; }

        public byte[] Bytes { get; set; }
    }
}
