using Konamiman.Nestor80.Assembler;
using Konamiman.Nestor80.Assembler.Relocatable;
using Konamiman.Nestor80.Linker.Infrastructure;
using System.Text;

namespace Konamiman.Nestor80.Linker.Parsing;

public class RelocatableFileParser
{
    static readonly byte[] extendedFileFormatHeader = { 0x85, 0xD3, 0x13, 0x92, 0xD4, 0xD5, 0x13, 0xD4, 0xA5, 0x00, 0x00, 0x13, 0x8F, 0xFF, 0xF0, 0x9E };

    private static readonly List<byte> rawBytes = new();

    private static BitStreamReader bsr;

    private static byte[] input;

    private static readonly List<ParsedProgram> result = new();

    private static readonly List<IRelocatableFilePart> currentProgramParts = new();

    private static bool extendedFormat;

    public static byte[] LastParsedProgramBytes { get; private set; } = null;

    /// <summary>
    /// Parses a relocatable file and returns the items it's composed of.
    /// </summary>
    /// <param name="inputStream">The stream to read the relocatablle file from.</param>
    /// <returns>An array with the items that compose the relocatable file, in the order in which they are found in the file.</returns>
    public static ParsedProgram[] Parse(Stream inputStream)
    {
        var ms = new MemoryStream();
        inputStream.CopyTo(ms);
        input = ms.ToArray();
        var beginningOfProgram = true;

        rawBytes.Clear();
        result.Clear();
        currentProgramParts.Clear();

        extendedFormat = false;

        bsr = new BitStreamReader(input);

        while(!bsr.EndOfStream) {
            if(beginningOfProgram) {
                var maybeHeader = bsr.PeekBytes(extendedFileFormatHeader.Length);
                if(Enumerable.SequenceEqual(maybeHeader, extendedFileFormatHeader)) {
                    currentProgramParts.Add(ExtendedRelocatableFileHeader.Instance);
                    bsr.DiscardBytes(extendedFileFormatHeader.Length);
                    extendedFormat = true;
                }
                else {
                    extendedFormat = false;
                }

                beginningOfProgram = false;
            }

            var nextItemIsRelocatable = bsr.ReadBit();
            if(!nextItemIsRelocatable) {
                var nextAbsoluteByte = bsr.ReadByte(8);
                rawBytes.Add(nextAbsoluteByte);
                continue;
            }

            if(rawBytes.Count > 0) {
                currentProgramParts.Add(new RawBytes() { Bytes = rawBytes.ToArray() });
                rawBytes.Clear();
            }

            var relocatableItemType = bsr.ReadByte(2);
            if(relocatableItemType != 0) {
                var addressValue = bsr.ReadUInt16(16);
                currentProgramParts.Add(new RelocatableAddress() { Type = (AddressType)relocatableItemType, Value = addressValue });
                continue;
            }

            var linkItem = ExtractLinkItem();
            if(linkItem.Type == LinkItemType.EndProgram) {
                bsr.ForceByteBoundary();
                extendedFormat = false;
                beginningOfProgram = true;
                var programBytes = bsr.GetAccummulatedBytes();
                currentProgramParts.Add(linkItem);
                var programName = (currentProgramParts.FirstOrDefault(x => x is LinkItem li && li.Type is LinkItemType.ProgramName) as LinkItem)?.Symbol ?? "";
                result.Add(new() { ProgramName = programName, Parts = currentProgramParts.ToArray(), Bytes = programBytes });
                currentProgramParts.Clear();
            }
            else if(linkItem.Type == LinkItemType.EndFile) {
                LastParsedProgramBytes = bsr.GetAccummulatedBytes();
                break;
            }
            else {
                currentProgramParts.Add(linkItem);
            }
        }

        if(rawBytes.Count > 0) {
            // Should never happen (an "End of file" link item would have been found) but just in case.
            currentProgramParts.Add(new RawBytes() { Bytes = rawBytes.ToArray() });
        }

        return result.ToArray();
    }

    private static LinkItem ExtractLinkItem()
    {
        var linkItemType = (LinkItemType)bsr.ReadByte(4);
        var linkItem = new LinkItem() { Type = linkItemType };

        if(linkItemType == LinkItemType.EndFile) {
            return linkItem;
        }

        if(linkItemType >= (LinkItemType)5) {
            var addressType = (AddressType)bsr.ReadByte(2);
            var address = bsr.ReadUInt16(16);
            linkItem.Address = new RelocatableAddress() { Type = addressType, Value = address };
        }

        if(linkItemType <= (LinkItemType)7) {
            var symbolLength = bsr.ReadByte(3);
            var symbolBytes = new byte[symbolLength];
            for(int i = 0; i < symbolLength; i++) {
                symbolBytes[i] = bsr.ReadByte(8);
            }

            if(symbolLength > 1 && extendedFormat && symbolBytes[0] == 0xFF) {
                int extendedSymbolLength = symbolLength switch {
                    2 => symbolBytes[1],
                    3 => symbolBytes[1] + symbolBytes[2] << 8,
                    4 => symbolBytes[1] + symbolBytes[2] << 8 + symbolBytes[3] << 16,
                    5 => symbolBytes[1] + symbolBytes[2] << 8 + symbolBytes[3] << 16 + symbolBytes[4] << 24,
                    _ => throw new ArgumentException($"Found B field whose first byte is FFh and field length is {symbolLength} (illegal in the extended file format)")
                };
                if(extendedSymbolLength > 255) {
                    symbolBytes = bsr.ReadDirectBytes(extendedSymbolLength);
                }
                else {
                    symbolBytes = new byte[extendedSymbolLength];
                    for(int i = 0; i < extendedSymbolLength; i++) {
                        symbolBytes[i] = bsr.ReadByte(8);
                    }
                }
            }

            if(linkItemType == LinkItemType.ExtensionLinkItem) {
                linkItem.ExtendedType = (ExtensionLinkItemType)symbolBytes[0];
                symbolBytes = symbolBytes.Skip(1).ToArray();
            }

            linkItem.SymbolBytes = symbolBytes;
            try {
                linkItem.Symbol = (extendedFormat ? Encoding.ASCII : Encoding.UTF8).GetString(symbolBytes);
            }
            catch {
                // Symbol bytes don't actually represent a valid string, so leave symbol as null
            }
        }

        return linkItem;
    }
}
