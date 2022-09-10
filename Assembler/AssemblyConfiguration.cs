using System.Text;

namespace Konamiman.Nestor80.Assembler
{
    public class AssemblyConfiguration
    {
        public string DefaultProgramName { get; init; }

        public string OutputStringEncoding { get; init; } = "ASCII";

        public int? MaxLineLength { get; init; } = null;

        public Func<string, Stream> GetStreamForInclude { get; init; } = _ => null;

        public Action<string> Print { get; init; } = _ => { };
    }
}
