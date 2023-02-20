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
        private static Func<string, string> GetFullNameOfRequestedLibraryFile;
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
        private static SegmentsSequencingMode segmentsSequencingMode;
        private static readonly List<ExternalReference> externalsPendingResolution = new();
        private static readonly List<RequestedLibFile> requestedLibFiles = new();
        private static readonly List<Expression> expressionsPendingEvaluation = new();

        // Keys are symbol names, values are lists of program names
        private static Dictionary<string, HashSet<string>> duplicatePublicSymbols = new(StringComparer.OrdinalIgnoreCase);

        public static event EventHandler<RelocatableFileReference> FileProcessingStart;
        public static event EventHandler<string> LinkError;
        public static event EventHandler<string> LinkWarning;

        private static AddressType[] addressTypes = new[] {
            AddressType.CSEG, AddressType.DSEG, AddressType.ASEG
        };

        public static LinkingResult Link(LinkingConfiguration configuration, Stream outputStream)
        {
            commonBlocks.Clear();
            errors.Clear();
            warnings.Clear();
            symbols.Clear();
            programInfos.Clear();
            externalsPendingResolution.Clear();
            requestedLibFiles.Clear();
            expressionsPendingEvaluation.Clear();

            RelocatableFilesProcessor.outputStream = outputStream ?? throw new ArgumentNullException(nameof(outputStream));
            startAddress = configuration.StartAddress;
            endAddress = configuration.EndAddress;
            linkItems = configuration.LinkingSequenceItems;
            fillByte = configuration.FillingByte;
            OpenFile  = configuration.OpenFile;
            GetFullNameOfRequestedLibraryFile = configuration.GetFullNameOfRequestedLibraryFile;
            currentProgramContents = null;
            resultingMemory = Enumerable.Repeat(configuration.FillingByte, 65536).ToArray();

            DoLinking();

            if(startAddress <= endAddress) {
                outputStream.Write(resultingMemory.Skip(startAddress).Take(endAddress - startAddress + 1).ToArray());
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
                ProgramsData = programInfos.Select(pi => pi.ToProgramData(symbols)).ToArray()
            };
        }

        private static RelocatableFileReference currentFile;
        private static ushort? codeSegmentAddressFromInput;
        private static ushort? dataSegmentAddressFromInput;
        private static HashSet<string> currentProgramSymbols = new(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<ushort, ushort> offsetsForExternals = new();

        private static void DoLinking()
        {
            codeSegmentAddressFromInput = null;
            dataSegmentAddressFromInput = null;
            segmentsSequencingMode = SegmentsSequencingMode.DataBeforeCode;
            offsetsForExternals.Clear();

            foreach(var linkItem in linkItems) {
                if(linkItem is SetCodeSegmentAddress scsa) {
                    codeSegmentAddressFromInput = scsa.Address;
                }
                else if(linkItem is SetDataSegmentAddress sdsa) {
                    dataSegmentAddressFromInput = sdsa.Address;
                    segmentsSequencingMode = SegmentsSequencingMode.CombineSameSegment;
                }
                else if(linkItem is SetCodeBeforeDataMode) {
                    if(segmentsSequencingMode is SegmentsSequencingMode.CombineSameSegment) {
                        AddWarning("Can't set \"code before data\" mode after an explicit address for the data segment has been specified");
                    }
                    else {
                        segmentsSequencingMode = SegmentsSequencingMode.CodeBeforeData;
                    }
                }
                else if(linkItem is SetDataBeforeCodeMode) {
                    if(segmentsSequencingMode is SegmentsSequencingMode.CombineSameSegment) {
                        AddWarning("Can't set \"data before code\" mode after an explicit address for the data segment has been specified");
                    }
                    else {
                        segmentsSequencingMode = SegmentsSequencingMode.DataBeforeCode;
                    }
                }
                else if(linkItem is RelocatableFileReference rfr) {
                    var stream = OpenFile(rfr.FullName);
                    if(stream == null) {
                        AddError($"Could not open file {rfr.FullName} for processing");
                        return;
                    }
                    currentFile = rfr;
                    FileProcessingStart?.Invoke(null, rfr);
                    var parsedFileItems = RelocatableFileParser.Parse(stream);
                    ProcessFile(parsedFileItems);
                }
                else {
                    throw new Exception($"Unexpected type of linking sequence item found in {nameof(DoLinking)}: {linkItem.GetType().Name}");
                }
            }

            var unknownExternals = externalsPendingResolution
                .Where(e => !symbols.ContainsKey(e.SymbolName))
                .Select(e => e.SymbolName);

            var externalsInRequestedLibraries = requestedLibFiles
                .SelectMany(lf => lf.Contents)
                .Where(i => i is LinkItem li && li.Type is LinkItemType.ChainExternal)
                .Select(i => ((LinkItem)i).Symbol);

            var allExternalsToSearchInLibraries =
                unknownExternals
                .Concat(externalsInRequestedLibraries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach(var external in allExternalsToSearchInLibraries) {
                bool foundMatchingLibrary = false;
                foreach(var libraryFile in requestedLibFiles) {
                    if(foundMatchingLibrary) {
                        break;
                    }

                    if(libraryFile.PublicSymbols.Contains(external, StringComparer.OrdinalIgnoreCase)) {
                        libraryFile.MustLoad = true;
                        foundMatchingLibrary = true;
                    }
                }
            }

            foreach(var libraryFile in requestedLibFiles.Where(f => f.MustLoad)) {
               ProcessFile(libraryFile.Contents);
            }

            foreach(var externalPendingResolution in externalsPendingResolution) {
                if(!symbols.ContainsKey(externalPendingResolution.SymbolName)) {
                    AddError($"In program {externalPendingResolution.ProgramName}: can't resolve external symbol reference: {externalPendingResolution.SymbolName}");
                    continue;
                }

                ResolveExternalChain(externalPendingResolution);
            }

            Expression.Symbols = symbols;

            foreach(var expression in expressionsPendingEvaluation) {
                try {
                    var value = expression.Evaluate();
                    resultingMemory[expression.TargetAddress] = (byte)(value & 0xFF);
                    if(!expression.StoreAsByte) {
                        resultingMemory[expression.TargetAddress + 1] = (byte)(value >> 8);
                    }
                }
                catch(ExpressionEvaluationException ex) {
                    AddError(ex.Message);
                }
            }

            foreach(var symbolName in duplicatePublicSymbols.Keys) {
                var programNames = string.Join(", ", duplicatePublicSymbols[symbolName]);
                AddError($"Symbol '{symbolName}' is defined in multiple programs: {programNames}");
            }
        }

        private static void ResolveExternalChain(ExternalReference externalPendingResolution)
        {
            ushort address = externalPendingResolution.ChainStartAddress;
            var externalValue = symbols[externalPendingResolution.SymbolName];
            while(true) {
                var linkedAddress = (ushort)(resultingMemory[address] + ((resultingMemory[address + 1]) << 8));
                var effectiveValue =
                    offsetsForExternals.ContainsKey(address) ?
                    externalValue + offsetsForExternals[address] : externalValue;
                resultingMemory[address] = (byte)(effectiveValue & 0xFF);
                resultingMemory[address + 1] = (byte)(effectiveValue >> 8);
                if(linkedAddress == 0) {
                    break;
                }
                address = linkedAddress;
            }
        }

        private static void ProcessFile(IRelocatableFilePart[] parsedFileItems)
        {
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

        private static bool currentProgramHasAbsoluteSegment;

        private static void ProcessProgram(IRelocatableFilePart[] programItems)
        {
            currentProgramSymbols.Clear();
            currentProgramHasAbsoluteSegment = false;

            currentProgramAbsoluteSegmentStart = 0xFFFF;
            currentProgramAbsoluteSegmentEnd = 0;

            var programNameItem = programItems.FirstOrDefault(x => x is LinkItem li && li.Type is LinkItemType.ProgramName);
            currentProgramName = (programNameItem as LinkItem)?.Symbol ?? currentFile.DisplayName;

            var programSizeItem = programItems.FirstOrDefault(x => x is LinkItem li && li.Type is LinkItemType.ProgramAreaSize);
            ushort programSize = (programSizeItem as LinkItem)?.Address.Value ?? 0;

            var dataSizeItem = programItems.FirstOrDefault(x => x is LinkItem li && li.Type is LinkItemType.DataAreaSize);
            ushort dataSize = (dataSizeItem as LinkItem)?.Address.Value ?? 0;

            var previousProgram = programInfos.LastOrDefault(pi => pi.HasContent);

            if(segmentsSequencingMode is SegmentsSequencingMode.CombineSameSegment) {
                currentProgramCodeSegmentStart =
                    codeSegmentAddressFromInput ?? (ushort)((previousProgram?.CodeSegmentEnd ?? 0x102) + 1);

                // The "CombineSameSegment" mode is entered only after an explicit data segment address is supplied.
                // Thus, either it was supplied right before this program (and thus dataSegmentAddressFromInput
                // is not null), or it was supplied before one of the previous programs
                // (and thus previousProgram is not null).

                currentProgramDataSegmentStart =
                    dataSegmentAddressFromInput ?? (ushort)(previousProgram.DataSegmentEnd + 1);
            }

            // The other two modes can't be (re-)entered once an address is specified for the data segment,
            // thus we can safely ignore dataSegmentAddressFromInput for these.

            else if(segmentsSequencingMode is SegmentsSequencingMode.CodeBeforeData) {
                currentProgramCodeSegmentStart =
                    codeSegmentAddressFromInput ?? (ushort)((previousProgram?.MaxSegmentEnd ?? 0x102) + 1);

                currentProgramDataSegmentStart = (ushort)(currentProgramCodeSegmentStart + programSize);
            }
            else if(segmentsSequencingMode is SegmentsSequencingMode.DataBeforeCode) {
                // Not a bug: in this mode the data segment really starts at the address
                // specified for the code segment. This is also how LINK-80 works.
                currentProgramDataSegmentStart =
                    codeSegmentAddressFromInput ?? (ushort)((previousProgram?.MaxSegmentEnd ?? 0x102) + 1);

                currentProgramCodeSegmentStart = (ushort)(currentProgramDataSegmentStart + dataSize);
            }
            else {
                throw new Exception($"Unexpected segments sequencing mode: {segmentsSequencingMode}");
            }

            currentProgramCodeSegmentEnd = programSize == 0 ? currentProgramCodeSegmentStart : (ushort)(currentProgramCodeSegmentStart + programSize - 1);
            currentProgramDataSegmentEnd = dataSize == 0 ? currentProgramDataSegmentStart : (ushort)(currentProgramDataSegmentStart + dataSize - 1);

            if(programSize > 0) {
                startAddress = Math.Min(startAddress, currentProgramCodeSegmentStart);
                endAddress = Math.Max(endAddress, currentProgramCodeSegmentEnd);
            }
            if(dataSize > 0) {
                startAddress = Math.Min(startAddress, currentProgramDataSegmentStart);
                endAddress = Math.Max(endAddress, currentProgramDataSegmentEnd);
            }

            currentAddressType = AddressType.CSEG;
            currentProgramAddress = currentProgramCodeSegmentStart;

            var oldCurrentProgramAddress = currentProgramAddress;
            for(int fileItemIndex = 0; fileItemIndex < programItems.Length; fileItemIndex++) {
                if(oldCurrentProgramAddress > currentProgramAddress) {
                    AddWarning($"When processing {currentAddressType} of program '{currentProgramName}': program counter overflowed from FFFFh to 0");
                    startAddress = 0;
                    endAddress = 65535;
                }
                oldCurrentProgramAddress = currentProgramAddress;

                var fileItem = programItems[fileItemIndex];

                if(fileItem is RawBytes rawBytes) {
                    var excessBytes = currentProgramAddress + rawBytes.Bytes.Length - 65536;
                    if(excessBytes > 0) {
                        Array.Copy(rawBytes.Bytes, 0, resultingMemory, currentProgramAddress, rawBytes.Bytes.Length-excessBytes);
                        Array.Copy(rawBytes.Bytes, rawBytes.Bytes.Length-excessBytes, resultingMemory, 0, excessBytes);
                        currentProgramAddress = (ushort)excessBytes;
                    }
                    else {
                        Array.Copy(rawBytes.Bytes, 0, resultingMemory, currentProgramAddress, rawBytes.Bytes.Length);
                        currentProgramAddress += (ushort)rawBytes.Bytes.Length;
                    }
                    MaybeAdjustAbsoluteSegmentEnd();

                    continue;
                }
                else if(fileItem is RelocatableAddress relAdd) {
                    var effectiveAddress = relAdd.Type is AddressType.CSEG ? currentProgramCodeSegmentStart + relAdd.Value : currentProgramDataSegmentStart + relAdd.Value;
                    resultingMemory[currentProgramAddress++] = (byte)(effectiveAddress & 0xFF);
                    resultingMemory[currentProgramAddress++] = (byte)(effectiveAddress >> 8);
                    MaybeAdjustAbsoluteSegmentEnd();
                    continue;
                }
                else if(fileItem is not LinkItem) {
                    throw new Exception($"Unexpected type of linking sequence item found in {nameof(ProcessFile)}: {fileItem.GetType().Name}");
                }

                var linkItem = (LinkItem)fileItem;

                if(linkItem.Type is LinkItemType.SetLocationCounter) {
                    currentAddressType = linkItem.Address.Type;
                    currentProgramAddress = (ushort)(
                        currentAddressType is AddressType.CSEG ? currentProgramCodeSegmentStart + linkItem.Address.Value :
                        currentAddressType is AddressType.DSEG ? currentProgramDataSegmentStart + linkItem.Address.Value :
                        linkItem.Address.Value
                    );

                    if(currentAddressType is AddressType.ASEG) {
                        currentProgramHasAbsoluteSegment = true;
                        currentProgramAbsoluteSegmentStart = Math.Min(currentProgramAbsoluteSegmentStart, currentProgramAddress);
                        startAddress = Math.Min(startAddress, currentProgramAbsoluteSegmentStart);
                    }
                    MaybeAdjustAbsoluteSegmentEnd();

                    oldCurrentProgramAddress = currentProgramAddress;
                }
                else if(linkItem.Type is LinkItemType.DefineEntryPoint) {
                    if(currentProgramSymbols.Contains(linkItem.Symbol)) {
                        continue;
                    }

                    var effectiveAddress =
                        linkItem.Address.Type is AddressType.CSEG ? currentProgramCodeSegmentStart + linkItem.Address.Value :
                        linkItem.Address.Type is AddressType.DSEG ? currentProgramDataSegmentStart + linkItem.Address.Value :
                        linkItem.Address.Value;

                    var isDuplicate = false;

                    if(duplicatePublicSymbols.ContainsKey(linkItem.Symbol)) {
                        duplicatePublicSymbols[linkItem.Symbol].Add(currentProgramName);
                        isDuplicate = true;
                    }
                    else {
                        foreach(var program in programInfos) {
                            if(program.PublicSymbols.Contains(linkItem.Symbol, StringComparer.OrdinalIgnoreCase)) {
                                if(!duplicatePublicSymbols.ContainsKey(linkItem.Symbol)) {
                                    duplicatePublicSymbols.Add(linkItem.Symbol, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                                }
                                duplicatePublicSymbols[linkItem.Symbol].Add(program.ProgramName);
                                duplicatePublicSymbols[linkItem.Symbol].Add(currentProgramName);
                                isDuplicate = true;
                            }
                        }
                    }

                    if(!isDuplicate) {
                        symbols[linkItem.Symbol] = (ushort)effectiveAddress;
                    }

                    currentProgramSymbols.Add(linkItem.Symbol);

                }
                else if(linkItem.Type is LinkItemType.ChainExternal) {
                    var chainStartAddress = (
                        linkItem.Address.Type is AddressType.CSEG ? currentProgramCodeSegmentStart :
                        linkItem.Address.Type is AddressType.DSEG ? currentProgramDataSegmentStart :
                        0) + linkItem.Address.Value;
                    externalsPendingResolution.Add(new() { SymbolName = linkItem.Symbol, ProgramName = currentProgramName, ChainStartAddress = (ushort)chainStartAddress });
                }
                else if(linkItem.Type is LinkItemType.RequestLibrarySearch) {
                    var fileName = linkItem.Symbol;
                    if(requestedLibFiles.Any(f => f.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase))) {
                        continue;
                    };

                    var fullName = GetFullNameOfRequestedLibraryFile(fileName);

                    var rfr = new RelocatableFileReference() { DisplayName = fileName, FullName = fullName };
                    FileProcessingStart?.Invoke(null, rfr);

                    var stream = OpenFile(fullName);
                    if(stream == null) {
                        AddWarning($"Could not open .REQUEST file {fullName} for processing");
                        continue;
                    }

                    var parsedFileItems = RelocatableFileParser.Parse(stream);
                    var publicSymbols = parsedFileItems
                        .Where(i => i is LinkItem li && li.Type is LinkItemType.EntrySymbol)
                        .Select(i => ((LinkItem)i).Symbol)
                        .ToArray();

                    if(publicSymbols.Length == 0) {
                        AddWarning($"Requested library file {fileName} doesn't define any public symbol so it won't be used");
                    }
                    else {
                        requestedLibFiles.Add(new() { Name = fileName, Contents = parsedFileItems, PublicSymbols = publicSymbols, MustLoad = false });
                    }
                }
                else if(linkItem.Type is LinkItemType.ExternalPlusOffset) {
                    offsetsForExternals.Add(currentProgramAddress, linkItem.Address.Value);
                }
                else if(linkItem.Type is LinkItemType.ExternalMinusOffset) {
                    offsetsForExternals.Add(currentProgramAddress, (ushort)-linkItem.Address.Value);
                }
                else if(linkItem.Type is LinkItemType.ChainAddress) {
                    AddError($"Unsupported link item type found in program {currentProgramName}: 'Chain address'");
                }
                else if(linkItem.Type is LinkItemType.ExtensionLinkItem) {
                    var expressionItems = programItems
                        .Skip(fileItemIndex)
                        .TakeWhile(i => i is LinkItem li && ((LinkItem)i).Type is LinkItemType.ExtensionLinkItem)
                        .Cast<LinkItem>()
                        .ToArray();

                    var expression = new Expression(expressionItems, currentProgramAddress, currentProgramName, currentProgramCodeSegmentStart, currentProgramDataSegmentStart);
                    expressionsPendingEvaluation.Add(expression);

                    if(!expression.StoreAsByte && !expression.StoreAsWord) {
                        AddWarning($"In program {currentProgramName}: found an expression that is neither marked as 'store as byte' nor as 'store as word', will store as word");
                    }

                    // -1 because it will be incremented in the next step of the for loop
                    fileItemIndex += expressionItems.Length - 1;
                }
                else if(linkItem.Type is LinkItemType.ProgramName or LinkItemType.EntrySymbol or LinkItemType.ProgramAreaSize or LinkItemType.DataAreaSize) {
                    // We have no use for EntrySymbol.
                    // As for the others, we've already dealt with them.
                    continue;
                }
                else {
                    throw new NotImplementedException();
                }
            }

            if(oldCurrentProgramAddress > currentProgramAddress) {
                AddWarning($"When processing {currentAddressType} of program '{currentProgramName}': program counter overflowed from FFFFh to 0");
                startAddress = 0;
                endAddress = 65535;
            }

            if(currentProgramHasAbsoluteSegment) {
                endAddress = Math.Max(endAddress, currentProgramAbsoluteSegmentEnd);
            }

            var currentProgramInfo = new ProgramInfo() {
                CodeSegmentStart = currentProgramCodeSegmentStart,
                CodeSegmentEnd = currentProgramCodeSegmentEnd,
                DataSegmentStart = currentProgramDataSegmentStart,
                DataSegmentEnd = currentProgramDataSegmentEnd,
                AbsoluteSegmentStart = currentProgramAbsoluteSegmentStart,
                AbsoluteSegmentEnd = currentProgramAbsoluteSegmentEnd,
                ProgramName = currentProgramName,
                PublicSymbols = currentProgramSymbols.ToArray(),
                HasCode = programSize > 0,
                HasData = dataSize > 0,
                HasAbsolute = currentProgramHasAbsoluteSegment
                //TODO: Common blocks
            };
            currentProgramInfo.RebuildRanges();

            foreach(var programInfo in programInfos) {
                AddressRange intersection;

                foreach(var addressType1 in addressTypes) {
                    foreach(var addressType2 in addressTypes) {
                        if(!currentProgramInfo.Has(addressType1) || !programInfo.Has(addressType2)) {
                            continue;
                        }
                        intersection = AddressRange.Intersection(currentProgramInfo.RangeOf(addressType1), programInfo.RangeOf(addressType2));
                        if(intersection != null) {
                            AddError($"{addressType1} of program '{currentProgramInfo.ProgramName}' and {addressType2} of program '{programInfo.ProgramName}' intersect at addresses {intersection.Start:X4}h to {intersection.End:X4}h");
                        }
                    }
                }
            }

            programInfos.Add(currentProgramInfo);
        }

        private static void MaybeAdjustAbsoluteSegmentEnd()
        {
            if(currentAddressType is AddressType.ASEG) {
                currentProgramAbsoluteSegmentEnd = Math.Max((ushort)(currentProgramAddress-1), currentProgramAbsoluteSegmentEnd);
            }
        }

        private static void CleanupAfterProcessingProgram()
        {
            codeSegmentAddressFromInput = null;
            dataSegmentAddressFromInput = null;
        }

        private static void AddError(string message) { 
            errors.Add(message);
            LinkError?.Invoke(null, message);
        }

        private static void AddWarning(string message)
        {
            warnings.Add(message);
            LinkWarning?.Invoke(null, message);
        }
    }
}
