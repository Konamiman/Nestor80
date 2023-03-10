using Konamiman.Nestor80.Linker.Infrastructure;
using Konamiman.Nestor80.Linker.Parsing;
using System.Text;

namespace Konamiman.Nestor80.LB80
{
    /// <summary>
    /// Dumper for Microsoft Z80 relocatable files.
    /// </summary>
    static public class RelFileDumper
    {
        static readonly byte[] extendedFileFormatHeader = { 0x85, 0xD3, 0x13, 0x92, 0xD4, 0xD5, 0x13, 0xD4, 0xA5, 0x00, 0x00, 0x13, 0x8F, 0xFF, 0xF0, 0x9E };

        const byte LINK_ITEM_EXTENSION = 4;
        const byte LINK_ITEM_PROGRAM_END = 14;
        const byte LINK_ITEM_FILE_END = 15;

        static private string[] addressTypes = {
            "", //Absolute
            "Code ",
            "Data ",
            "Common "
        };

        static private bool extendedFormat = false;

        private static List<IRelocatableFilePart> extendedHeaderItems = new();

        static private string[] linkItemTypes = {
            "Entry symbol",
            "Select COMMON block",
            "Program name",
            "Request library search",
            "Extension link item",
            "Define COMMON size",
            "Chain external",
            "Define entry point",
            "External - offset",
            "External + offset",
            "Define size of Data area",
            "Set loading location counter",
            "Chain address",
            "Define program size",
            "End of program",
            "End of file"
        };

        static private string[] arithmeticOperators = {
            "Store as byte",    //1
            "Store as word",    //2
            "HIGH",             //3
            "LOW",              //4
            "NOT",              //5
            "Unary -",          //6
            "-",                //7
            "+",                //8
            "*",                //9
            "/",                //10
            "MOD",              //11
            "? (12)",           //12
            "? (13)",           //13
            "? (14)",           //14
            "? (15)",           //15
            "SHR",              //16
            "SHL",              //17
            "EQ",               //18
            "NEQ",              //19
            "LT",               //20
            "LTE",              //21
            "GT",               //22
            "GTE",              //23
            "AND",              //24
            "OR",               //25
            "XOR"               //26
        };

        static private byte[] fileContents;

        static private List<byte> cummulatedAbsoluteBytes = new List<byte>();

        /// <summary>
        /// Parse and dump the contents of the file to the console.
        /// </summary>
        static public void DumpFile(byte[] fileBytes)
        {
            fileContents = fileBytes;
            cummulatedAbsoluteBytes.Clear();
            extendedHeaderItems.Clear();
            extendedFormat = false;
            var beginningOfProgram = true;

            var bsr = new BitStreamReader(fileContents);
            cummulatedAbsoluteBytes.Clear();

            while(!bsr.EndOfStream) {
                if(beginningOfProgram) {
                    if(bsr.PeekBytes(1)[0] == 0x9E) {
                        //"End of file" item
                        break;
                    }

                    Console.WriteLine("--- Beginning of progam ---");
                    Console.WriteLine();

                    var maybeHeader = bsr.PeekBytes(extendedFileFormatHeader.Length);
                    if(Enumerable.SequenceEqual(maybeHeader, extendedFileFormatHeader)) {
                        Console.WriteLine("Extended relocatable file format header");
                        Console.WriteLine();
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
                    cummulatedAbsoluteBytes.Add(nextAbsoluteByte);
                    continue;
                }

                if(cummulatedAbsoluteBytes.Count > 0) {
                    Console.WriteLine();
                    foreach(var item in cummulatedAbsoluteBytes) {
                        Console.Write($"{item:X2} ");
                    }
                    Console.WriteLine();
                    Console.WriteLine();
                    cummulatedAbsoluteBytes.Clear();
                }

                var relocatableItemType = bsr.ReadByte(2);
                if(relocatableItemType != 0) {
                    var relocatableItem = bsr.ReadUInt16(16);
                    Console.WriteLine($"{addressTypes[relocatableItemType]}{relocatableItem:X4}");
                    continue;
                }

                var linkItemType = bsr.ReadByte(4);
                if(linkItemType == LINK_ITEM_FILE_END) {
                    Console.WriteLine(linkItemTypes[LINK_ITEM_FILE_END]);
                    return;
                }

                if(linkItemType == LINK_ITEM_EXTENSION) {
                    ExtractExtensionLinkItem(bsr);
                    continue;
                }

                Console.Write($"{linkItemTypes[linkItemType]}");
                if(linkItemType >= 5) {
                    ExtractAItem(bsr);
                }
                if(linkItemType <= 7) {
                    ExtractBItem(bsr);
                }

                if(linkItemType == LINK_ITEM_PROGRAM_END) {
                    beginningOfProgram = true;
                    bsr.ForceByteBoundary();
                    if(!bsr.EndOfStream) {
                        Console.WriteLine();
                    }
                }

                Console.WriteLine();
            }
        }

        static private void ExtractExtensionLinkItem(BitStreamReader bsr)
        {
            var specialItemBytes = ExtractBItem(bsr, false);

            Console.Write($"{linkItemTypes[4]}, ");
            var specialLintItemType = specialItemBytes[0];
            specialItemBytes = specialItemBytes.Skip(1).ToArray();
            switch(specialLintItemType) {
                case 0x41:
                    var operatorType = specialItemBytes[0];
                    var operatorTypeString = operatorType < 1 || operatorType > arithmeticOperators.Length ? $"{operatorType:X2}" : $"{arithmeticOperators[operatorType - 1]}";
                    Console.WriteLine($"Arith Operator, {operatorTypeString}");
                    break;
                case 0x42:
                    Console.WriteLine($"Ref external, {ToAsciiString(specialItemBytes)}");
                    break;
                case 0x43:
                    var value = specialItemBytes[1] + (specialItemBytes[2] << 8);
                    Console.WriteLine($"Value, {addressTypes[specialItemBytes[0]]}{value:X4}");
                    break;
                case 0x48:
                    Console.WriteLine($"Common runtime header, file = {ToAsciiString(specialItemBytes)}");
                    break;
                default:
                    var specialItemBytesHex = specialItemBytes.Select(b => $"{b:X2}").ToArray();
                    var toprint = $"{specialLintItemType:X2}, {ToAsciiString(specialItemBytes)} ({string.Join(' ', specialItemBytesHex)})";
                    Console.WriteLine(toprint);
                    break;
            }
        }

        static private string ToAsciiString(byte[] data)
        {
            var chars = Encoding.UTF8.GetChars(data);
            chars = chars.Select(ch => char.IsControl(ch) ? '.' : ch).ToArray();
            return new string(chars);
        }

        static private void ExtractAItem(BitStreamReader bsr)
        {
            var addressType = bsr.ReadByte(2);
            var address = bsr.ReadUInt16(16);

            Console.Write($", {addressTypes[addressType]}{address:X4}");
        }

        static private byte[] ExtractBItem(BitStreamReader bsr, bool doPrint = true)
        {
            var symbolLength = bsr.ReadByte(3);
            var symbolBytes = new byte[symbolLength];
            for(int i = 0; i < symbolLength; i++) {
                symbolBytes[i] = bsr.ReadByte(8);
            }

            if(extendedFormat && symbolLength > 1 && symbolBytes[0] == 0xFF) {
                int extendedSymbolLength = symbolLength switch {
                    2 => symbolBytes[1],
                    3 => symbolBytes[1] + symbolBytes[2] << 8,
                    4 => symbolBytes[1] + symbolBytes[2] << 8 + symbolBytes[3] << 16,
                    5 => symbolBytes[1] + symbolBytes[2] << 8 + symbolBytes[3] << 16 + symbolBytes[4] << 24,
                    _ => throw new Exception($"Found B field whose first byte is FFh and field length is {symbolLength} (illegal in the extended file format)")
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

            if(doPrint) Console.Write($", {ToAsciiString(symbolBytes)}");
            return symbolBytes;
        }
    }
}
