using Konamiman.Nestor80.Assembler;
using Konamiman.Nestor80.Assembler.Relocatable;
using Konamiman.Nestor80.Linker.Parsing;

namespace Konamiman.Nestor80.Linker
{
    public static class RelocatableFilesProcessor
    {
        private static ushort startAddress;
        private static ushort endAddress;
        private static ushort minimumCodeSegmentStart;
        private static ushort maximumCodeSegmentEnd;
        private static ushort minimumDataSegmentStart;
        private static ushort maximumDataSegmentEnd;
        private static ushort absoluteSegmentStart;
        private static ushort absoluteSegmentEnd;
        private static AddressType currentAddressType;
        private static ushort currentProgramAddress;
        private static ushort currentProgramCodeSegmentStart;
        private static ushort currentProgramDataSegmentStart;
        private static ushort currentProgramAbsoluteSegmentStart;
        private static ushort currentProgramCodeSegmentEnd;
        private static ushort currentProgramDataSegmentEnd;
        private static ushort currentProgramAbsoluteSegmentEnd;
        private static Dictionary<string, ushort> currentProgramCommonBlocksStart;
        private static Dictionary<string, ushort> currentProgramCommonBlocksSizes;
        private static ILinkingSequenceItem[] linkItems;
        private static byte fillByte;
        private static Func<string, Stream> OpenFile;
        private static readonly Dictionary<string, Tuple<ushort, ushort>> commonBlocks = new();
        private static readonly List<string> errors = new();
        private static readonly List<string> warnings = new();
        private static readonly Dictionary<string, ushort> symbols = new();
        private static Stream outputStream;
        private static string currentProgramName;
        private static byte[] currentProgramContents;
        private static List<ProgramInfo> programInfos = new();
        private static bool codeSegmentAddressSpecified;
        private static bool dataSegmentAddressSpecified;
        private static byte[] resultingMemory;

        public static event EventHandler<string> FileProcessingStart;

        public static LinkingResult Link(LinkingConfiguration configuration, Stream outputStream)
        {
            commonBlocks.Clear();
            errors.Clear();
            warnings.Clear();
            symbols.Clear();
            programInfos.Clear();

            RelocatableFilesProcessor.outputStream = outputStream ?? throw new ArgumentNullException(nameof(outputStream));
            startAddress = configuration.StartAddress;
            endAddress = configuration.EndAddress;
            linkItems = configuration.LinkingSequenceItems;
            fillByte = configuration.FillingByte;
            OpenFile  = configuration.OpenFile;
            currentProgramContents = null;
            resultingMemory = Enumerable.Repeat(configuration.FillingByte, 65536).ToArray();

            DoLinking();

            if(startAddress <= endAddress) {
                outputStream.Write(resultingMemory.Skip(startAddress - 1).Take(endAddress - startAddress - 1).ToArray());
            }

            var areas = new List<AddressRange> {
                new AddressRange(minimumCodeSegmentStart, maximumCodeSegmentEnd, AddressType.CSEG),
                new AddressRange(minimumDataSegmentStart, maximumDataSegmentEnd, AddressType.DSEG),
                new AddressRange(absoluteSegmentStart, absoluteSegmentEnd, AddressType.ASEG)
            };
            foreach(var commonBlock in commonBlocks) {
                areas.Add(new AddressRange(commonBlock.Value.Item1, commonBlock.Value.Item2, AddressType.COMMON, commonBlock.Key));
            }

            return new LinkingResult() {
                StartAddress = startAddress,
                EndAddress = endAddress,
                Errors = errors.ToArray(),
                Warnings = warnings.ToArray(),
                Symbols = symbols.ToDictionary(x => x.Key, x => x.Value),
                Areas = areas.ToArray()
            };
        }

        private static RelocatableFileReference currentFile;
        private static ushort? codeSegmentAddressFromInput;
        private static ushort? dataSegmentAddressFromInput;

        private static void DoLinking()
        {
            codeSegmentAddressFromInput = null;
            dataSegmentAddressFromInput = null;

            foreach(var linkItem in linkItems) {
                if(linkItem is SetCodeSegmentAddress scsa) {
                    codeSegmentAddressFromInput = scsa.Address;
                    //TODO: Move the following
                    /*
                    minimumCodeSegmentStart = Math.Min(minimumCodeSegmentStart, scsa.Address);
                    currentProgramCodeSegmentStart = scsa.Address;
                    startAddress = Math.Min(startAddress, currentProgramCodeSegmentStart);
                    startAddress = Math.Min(startAddress, currentProgramDataSegmentStart);
                    */
                }
                else if(linkItem is SetDataSegmentAddress sdsa) {
                    dataSegmentAddressFromInput = sdsa.Address;
                    //TODO: Move the following
                    /*
                    minimumDataSegmentStart = Math.Min(minimumCodeSegmentStart, sdsa.Address);
                    currentProgramDataSegmentStart = sdsa.Address;
                    startAddress = Math.Min(startAddress, currentProgramCodeSegmentStart);
                    startAddress = Math.Min(startAddress, currentProgramDataSegmentStart);
                    */
                }
                else if(linkItem is RelocatableFileReference rfr) {
                    var stream = OpenFile(rfr.FullName);
                    if(stream == null) {
                        errors.Add($"Could not open file {rfr.FullName} for processing");
                        return;
                    }
                    currentFile = rfr;
                    ProcessFile(stream);
                }
                else {
                    throw new Exception($"Unexpected type of linking sequence item found in {nameof(DoLinking)}: {linkItem.GetType().Name}");
                }
            }
        }

        private static void ProcessFile(Stream stream)
        {
            var parsedFileItems = RelocatableFileParser.Parse(stream);

            while(true) {
                var programItems = parsedFileItems.TakeWhile(item => item is not LinkItem || ((LinkItem)item).Type is not LinkItemType.EndProgram and not LinkItemType.EndFile).ToArray();
                
                ProcessProgram(programItems);
                CleanupAfterProcessingProgram();

                parsedFileItems = parsedFileItems.Skip(programItems.Length+1).ToArray();
                if(parsedFileItems.Length == 0 || (parsedFileItems[0] as LinkItem)?.Type is LinkItemType.EndFile) {
                    return;
                }
            }
        }

        private static void ProcessProgram(IRelocatableFilePart[] programItems)
        {
            var programNameItem = programItems.FirstOrDefault(x => x is LinkItem li && li.Type is LinkItemType.ProgramName);
            currentProgramName = (programNameItem as LinkItem)?.Symbol ?? currentFile.DisplayName;

            var programSizeItem = programItems.FirstOrDefault(x => x is LinkItem li && li.Type is LinkItemType.ProgramAreaSize);
            ushort programSize = (programSizeItem as LinkItem)?.Address.Value ?? 0;

            var dataSizeItem = programItems.FirstOrDefault(x => x is LinkItem li && li.Type is LinkItemType.DataAreaSize);
            ushort dataSize = (dataSizeItem as LinkItem)?.Address.Value ?? 0;

            var previousProgram = programInfos.LastOrDefault();

            if(codeSegmentAddressFromInput is null && dataSegmentAddressFromInput is null) {
                // Neither code nor data segment addresses specified:
                // - Data segment starts at the default address
                //   (0103h, or after the code segment of the previous program)
                // - Code segment starts right after the end of data segment
                currentProgramDataSegmentStart = (ushort)(
                    previousProgram is null ?
                    0x103 : previousProgram.DataSegmentEnd + 1
                    );
                currentProgramCodeSegmentStart = (ushort)(currentProgramDataSegmentStart + dataSize);
                if(currentProgramCodeSegmentStart < currentProgramDataSegmentStart) {
                    warnings.Add($"{currentProgramName}: data segment set to start at {currentProgramDataSegmentStart:X4}h, code segment set to start after data segment but will actually start at {currentProgramCodeSegmentStart:X4}h, ");
                }
            }
            else if(codeSegmentAddressFromInput is null) {
                // Only data segment address specified:
                // - Data segment starts at the specified data segment start address
                // - Code segment starts at the default address
                //   (0103h, or after the code segment of the previous program)
                currentProgramCodeSegmentStart = (ushort)(
                    previousProgram is null ?
                    0x103 : previousProgram.CodeSegmentEnd + 1
                    );
                currentProgramDataSegmentStart = dataSegmentAddressFromInput.Value;
            }
            else if(dataSegmentAddressFromInput is null) {
                // Only code segment address specified:
                // - Data segment starts at the specified code segment start address
                //   (weird, but that's how LINK-80 behaves)
                // - Code segment starts right after the end of data segment
                currentProgramDataSegmentStart = codeSegmentAddressFromInput.Value;
                currentProgramCodeSegmentStart = (ushort)(currentProgramDataSegmentStart + dataSize);
                if(currentProgramDataSegmentStart > currentProgramCodeSegmentStart) {
                    warnings.Add($"{currentProgramName}: data segment set to start at {currentProgramDataSegmentStart:X4}h, code segment set to start before data segment but will actually start at {currentProgramCodeSegmentStart:X4}h, ");
                }
            }

            //TODO: Check intersections with previous programs

            currentProgramCodeSegmentEnd = programSize == 0 ? currentProgramCodeSegmentStart : (ushort)(currentProgramCodeSegmentStart + programSize - 1);
            currentProgramDataSegmentEnd = dataSize == 0 ? currentProgramDataSegmentStart : (ushort)(currentProgramDataSegmentStart + dataSize - 1);

            startAddress = Math.Min(startAddress, currentProgramCodeSegmentStart);
            startAddress = Math.Min(startAddress, currentProgramDataSegmentStart);
            endAddress = Math.Max(endAddress, currentProgramCodeSegmentEnd);
            endAddress = Math.Max(endAddress, currentProgramDataSegmentEnd);

            currentAddressType = AddressType.CSEG;
            currentProgramAddress = currentProgramCodeSegmentStart;

            foreach(var fileItem in programItems) {
                var oldCurrentProgramAddress = currentProgramAddress;
                if(fileItem is RawBytes rawBytes) {
                    Array.Copy(rawBytes.Bytes, 0, resultingMemory, currentProgramAddress, rawBytes.Bytes.Length);
                    currentProgramAddress += (ushort)rawBytes.Bytes.Length;
                    continue;
                }
                else if(fileItem is RelocatableAddress relAdd) {
                    var effectiveAddress = relAdd.Type is AddressType.CSEG ? currentProgramCodeSegmentStart + relAdd.Value : currentProgramDataSegmentStart + relAdd.Value;
                    resultingMemory[currentProgramAddress++] = (byte)(effectiveAddress & 0xFF);
                    resultingMemory[currentProgramAddress++] = (byte)(effectiveAddress >> 8);
                    continue;
                }
                else if(fileItem is not LinkItem) {
                    throw new Exception($"Unexpected type of linking sequence item found in {nameof(ProcessFile)}: {fileItem.GetType().Name}");
                }

                var linkItem = (LinkItem)fileItem;

                if(linkItem.Type is LinkItemType.SetLocationCounter) {
                    currentAddressType = linkItem.Address.Type;
                    currentProgramAddress =(ushort)(
                        currentAddressType is AddressType.CSEG ? currentProgramCodeSegmentStart + linkItem.Address.Value : currentProgramDataSegmentStart + linkItem.Address.Value
                    );
                }
                else if(linkItem.Type is LinkItemType.DefineEntryPoint) {
                    var effectiveAddress = linkItem.Address.Type is AddressType.CSEG ? currentProgramCodeSegmentStart + linkItem.Address.Value : currentProgramDataSegmentStart + linkItem.Address.Value;
                    //TODO: Handle duplicates
                    symbols[linkItem.Symbol] = (ushort)effectiveAddress;
                }
                else if(linkItem.Type is LinkItemType.ProgramName or LinkItemType.EntrySymbol or LinkItemType.ProgramAreaSize or LinkItemType.DataAreaSize) {
                    continue;
                }
                else {
                    throw new NotImplementedException();
                }

                //TODO: check wrap (old program address > current program address) whenever address is increased
            }

            var progInfo = new ProgramInfo() {
                CodeSegmentStart = currentProgramCodeSegmentStart,
                CodeSegmentEnd = currentProgramCodeSegmentEnd,
                DataSegmentStart = currentProgramDataSegmentStart,
                DataSegmentEnd = currentProgramDataSegmentEnd,
                ProgramName = currentProgramName
                //TODO: Commno blocks, public symbols, absolute segment
            };
            progInfo.RebuildRanges();
            programInfos.Add(progInfo);
        }

        private static void CleanupAfterProcessingProgram()
        {
            codeSegmentAddressFromInput = null;
            dataSegmentAddressFromInput = null;
        }
    }
}
