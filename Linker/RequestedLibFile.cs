using Konamiman.Nestor80.Linker.Parsing;

namespace Konamiman.Nestor80.Linker;

internal class RequestedLibFile
{
    public string Name { get; set; }

    public IRelocatableFilePart[] Contents { get; set; }

    public bool MustLoad { get; set; }

    public string[] PublicSymbols { get; set; }
}
