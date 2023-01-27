using Konamiman.Nestor80.Linker.Parsing;

namespace Konamiman.Nestor80.N80
{
    internal partial class Program
    {
        static int Main(string[] args)
        {
            var stream = File.OpenRead(args[0]);
            var parsed = RelocatableFileParser.Parse(stream);
            return 0;
        }
    }
}