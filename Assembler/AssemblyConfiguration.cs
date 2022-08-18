using System.Text;

namespace Konamiman.Nestor80.Assembler
{
    public class AssemblyConfiguration
    {
        public string DefaultProgramName { get; init; }

        public Stream SourceStream { get; init; }

        public Encoding SourceStreamEncoding { get; init; } = Encoding.ASCII;

        public int MaxLineLength { get; init; } = 131;

        public Stream TargetStream { get; init; }

        public Stream ListingStream { get; init; }

        public bool ListingAsCrossReference { get; init; }

        public bool OctalListing { get; init; }

        public bool ListFalseConditionals { get; init; }

        public bool Support8bitChars { get; init; }

        public Func<string, Stream> GetStreamForInclude { get; init; }

        public Action<string> Print { get; init; }
    }
}
