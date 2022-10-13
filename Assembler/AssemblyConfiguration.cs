using System.Text;

namespace Konamiman.Nestor80.Assembler
{
    public class AssemblyConfiguration
    {
        public string OutputStringEncoding { get; init; } = "ASCII";

        public BuildType BuildType { get; init; } = BuildType.Automatic;

        public string CpuName { get; init; } = "Z80";

        public bool AllowEscapesInStrings { get; init; } = true;

        public Func<string, Stream> GetStreamForInclude { get; init; } = _ => null;

        public Action<string> Print { get; init; } = _ => { };

        public (string, ushort)[] PredefinedSymbols = Array.Empty<(string, ushort)>();

        public int MaxErrors { get; init; } = 0;

        public bool AllowBareExpressions { get; init; } = false;
    }
}
