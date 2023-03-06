using Konamiman.Nestor80.Assembler;
using Konamiman.Nestor80.Assembler.Relocatable;
using Konamiman.Nestor80.Linker.Infrastructure;
using System.Text;

namespace Konamiman.Nestor80.Linker.Parsing;

public class RelocatableFileParser
{
    private static readonly List<byte> rawBytes = new();

    private static BitStreamReader bsr;

    private static byte[] input;

    private static readonly List<IRelocatableFilePart> result = new();

    private static bool extendedFormat;

    /// <summary>
    /// Parses a relocatable file and returns the items it's composed of.
    /// </summary>
    /// <param name="inputStream">The stream to read the relocatablle file from.</param>
    /// <returns>An array with the items that compose the relocatable file, in the order in which they are found in the file.</returns>
    public static IRelocatableFilePart[] Parse(Stream inputStream)
    {
        var ms = new MemoryStream();
        inputStream.CopyTo(ms);
        input = ms.ToArray();

        rawBytes.Clear();
        result.Clear();

        extendedFormat = false;
        if(input.Length > ExtendedRelocatableFileHeader.Bytes.Length && Enumerable.SequenceEqual(input.Take(ExtendedRelocatableFileHeader.Bytes.Length), ExtendedRelocatableFileHeader.Bytes)) {
            result.Add(ExtendedRelocatableFileHeader.Instance);
            input = input.Skip(ExtendedRelocatableFileHeader.Bytes.Length).ToArray();
            extendedFormat = true;
        }

        bsr = new BitStreamReader(input);

        while(!bsr.EndOfStream) {
            var nextItemIsRelocatable = bsr.ReadBit();
            if(!nextItemIsRelocatable) {
                var nextAbsoluteByte = bsr.ReadByte(8);
                rawBytes.Add(nextAbsoluteByte);
                continue;
            }

            if(rawBytes.Count > 0) {
                result.Add(new RawBytes() { Bytes = rawBytes.ToArray() });
                rawBytes.Clear();
            }

            var relocatableItemType = bsr.ReadByte(2);
            if(relocatableItemType != 0) {
                var addressValue = bsr.ReadUInt16(16);
                result.Add(new RelocatableAddress() { Type = (AddressType)relocatableItemType, Value = addressValue });
                continue;
            }

            var linkItem = ExtractLinkItem();
            result.Add(linkItem);

            if(linkItem.Type == LinkItemType.EndProgram) {
                bsr.ForceByteBoundary();
            }
            else if(linkItem.Type == LinkItemType.EndFile) {
                break;
            }
        }

        if(rawBytes.Count > 0) {
            // Should never happen (an "End of file" link item would have been found) but just in case.
            result.Add(new RawBytes() { Bytes = rawBytes.ToArray() });
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

            if(symbolBytes[0] == 0xFF && extendedFormat && symbolLength > 1) {
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
