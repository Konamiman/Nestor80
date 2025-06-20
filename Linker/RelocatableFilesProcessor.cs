﻿using Konamiman.Nestor80.Assembler;
using Konamiman.Nestor80.Assembler.Relocatable;
using Konamiman.Nestor80.Linker.Parsing;
using System.Text;

namespace Konamiman.Nestor80.Linker;

/// <summary>
/// This class provides a <see cref="RelocatableFilesProcessor.Link(Konamiman.Nestor80.Linker.LinkingConfiguration, Stream)"/> method
/// that processes one or more relocatable files and produces an absolute binary or Intel HEX file.
/// A few events allow to monitor the linking process.
/// </summary>
public static class RelocatableFilesProcessor
{
    // For compatibility with LINK-80
    const ushort DEFAULT_CODE_SEGMENT_START_ADDRESS = 0x0103;

    private static ushort startAddress;
    private static ushort endAddress;
    private static AddressType currentAddressType;
    private static ushort currentProgramAddress;
    private static ushort currentProgramCodeSegmentStart;
    private static ushort currentProgramDataSegmentStart;
    private static ushort currentProgramAbsoluteSegmentStart;
    private static ushort currentProgramCodeSegmentEnd;
    private static ushort currentProgramDataSegmentEnd;
    private static ushort currentProgramAbsoluteSegmentEnd;
    private static ushort currentProgramCommonSegmentStart;
    private static string currentProgramCommonBlockName;
    private static ILinkingSequenceItem[] linkItems;
    private static byte fillByte;
    private static Func<string, Stream> OpenFile;
    private static Func<string, string> GetFullNameOfRequestedLibraryFile;
    private static readonly Dictionary<string, CommonBlock> commonBlocks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<string> errors = new();
    private static readonly List<string> warnings = new();
    private static readonly Dictionary<string, ushort> symbols = new(StringComparer.OrdinalIgnoreCase);
    private static Stream outputStream;
    private static string currentProgramName;
    private static readonly List<ProgramInfo> programInfos = new();
    private static byte[] resultingMemory;
    private static SegmentsSequencingMode segmentsSequencingMode;
    private static readonly List<ExternalReference> externalsPendingResolution = new();
    private static readonly List<RequestedLibFile> requestedLibFiles = new();
    private static readonly List<Expression> expressionsPendingEvaluation = new();
    private static int maxErrors;
    private static int generatedErrors;
    private static bool hexFormat;

    // Keys are symbol names, values are lists of program names
    private static readonly Dictionary<string, HashSet<string>> duplicatePublicSymbols = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Event fired when the processing of a relocatable file starts.
    /// </summary>
    public static event EventHandler<RelocatableFileReference> FileProcessingStart;

    /// <summary>
    /// Event fired when a linking error is generated.
    /// </summary>
    public static event EventHandler<string> LinkError;

    /// <summary>
    /// Event fired when a linking warning is generated.
    /// </summary>
    public static event EventHandler<string> LinkWarning;

    private static readonly AddressType[] addressTypes = new[] {
        AddressType.CSEG, AddressType.DSEG, AddressType.COMMON, AddressType.ASEG
    };

    /// <summary>
    /// Processes one or more relocatable files and produces an absolute binary file.
    /// </summary>
    /// <param name="configuration">A configuration object that defines the linking process to perform.</param>
    /// <param name="outputStream">The stream where the resulting binary or Intel HEX file will be written.</param>
    /// <returns>The result from the linking process, including any errors generated.</returns>
    /// <exception cref="ArgumentNullException"></exception>
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
        startAddress = (configuration ?? throw new ArgumentNullException(nameof(configuration))).StartAddress;
        endAddress = configuration.EndAddress;
        linkItems = configuration.LinkingSequenceItems;
        fillByte = configuration.FillingByte;
        OpenFile  = configuration.OpenFile;
        maxErrors = configuration.MaxErrors;
        hexFormat = configuration.OutputHexFormat;
        GetFullNameOfRequestedLibraryFile = configuration.GetFullNameOfRequestedLibraryFile;
        resultingMemory = Enumerable.Repeat(configuration.FillingByte, 65536).ToArray();
        generatedErrors = 0;

        var maxErrorsReached = false;
        try {
            DoLinking();
        }
        catch(MaxErrorsReachedException) {
            maxErrorsReached = true;
            AddError("Maximum errors count reached, process aborted", false);
        }

        if(errors.Count == 0) {
            WriteOutput();
        }

        return new LinkingResult() {
            StartAddress = startAddress,
            EndAddress = endAddress,
            Errors = errors.ToArray(),
            Warnings = warnings.ToArray(),
            ProgramsData = programInfos.Select(pi => pi.ToProgramData(symbols)).ToArray(),
            CommonBlocks = commonBlocks.Values.ToArray(),
            MaxErrorsReached = maxErrorsReached
        };
    }

    private static void WriteOutput()
    {
        if(!hexFormat) {
            outputStream.Write(resultingMemory.Skip(startAddress).Take(endAddress - startAddress + 1).ToArray());
            outputStream.Close();
            return;
        }

        var writer = new StreamWriter(outputStream, Encoding.ASCII);
        var address = startAddress;
        var remaining = endAddress - startAddress + 1;
        while(remaining > 0) {
            var lineLength = Math.Min(remaining, 32);
            writer.Write($":{lineLength:X2}{address:X4}00");
            byte sum = (byte)(lineLength + (address & 0xFF) + (address >> 8));
            for(var i=0; i<lineLength; i++) {
                var value = resultingMemory[address + i];
                writer.Write($"{value:X2}");
                sum += value;
            }
            var checksum = (byte)-sum;
            writer.Write($"{checksum:X2}\r\n");
            address += (ushort)lineLength;
            remaining -= lineLength;
        }
        writer.Close();
    }

    private static RelocatableFileReference currentFile;
    private static ushort? codeSegmentAddressFromInput;
    private static ushort? dataSegmentAddressFromInput;
    private static readonly HashSet<string> currentProgramSymbols = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<ushort, ushort> offsetsForExternals = new();

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
            else if(linkItem is AlignCodeSegmentAddress acsa) {
                if(acsa.Value == 0) {
                    AddError("The value for \"align code segment address\" can't be zero");
                }
                else {
                    if(codeSegmentAddressFromInput is null) {
                        if(programInfos.Count == 0) {
                            codeSegmentAddressFromInput = DEFAULT_CODE_SEGMENT_START_ADDRESS;
                        }
                        else {
                            var lastProgram = programInfos.Last(pi => pi.HasContent);
                            codeSegmentAddressFromInput =
                                segmentsSequencingMode is SegmentsSequencingMode.CombineSameSegment ?
                                (ushort)(lastProgram.CodeSegmentEnd + 1) :
                                (ushort)(lastProgram.MaxSegmentEnd + 1);
                        }
                    }
                    codeSegmentAddressFromInput ??= (ushort)((programInfos.LastOrDefault(pi => pi.HasContent)?.CodeSegmentEnd ?? DEFAULT_CODE_SEGMENT_START_ADDRESS - 1) + 1);
                    codeSegmentAddressFromInput = Align(codeSegmentAddressFromInput.Value, acsa.Value);
                }
            }
            else if(linkItem is SetDataSegmentAddress sdsa) {
                dataSegmentAddressFromInput = sdsa.Address;
                segmentsSequencingMode = SegmentsSequencingMode.CombineSameSegment;
            }
            else if (linkItem is AlignDataSegmentAddress adsa) {
                if(adsa.Value == 0) {
                    AddError("The value for \"align data segment address\" can't be zero");
                }
                else if(segmentsSequencingMode is not SegmentsSequencingMode.CombineSameSegment) {
                    AddError("Can't align the data segment address before the \"separate code and data\" mode has been set");
                }
                else {
                    dataSegmentAddressFromInput ??= (ushort)(programInfos.Last().DataSegmentEnd + 1);
                    dataSegmentAddressFromInput = Align(dataSegmentAddressFromInput.Value, adsa.Value);
                }
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
           ProcessProgram(null, libraryFile.Contents);
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
        var iterations = 0;
        while(iterations < 32768) {
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
            iterations++;
        }

        //The absolute maximum length of a 2 byte items chain for a 64K program is 32K
        if(iterations >= 32768) {
            throw new Exception($"Infinite loop when resolving external symbol chain, program: {externalPendingResolution.ProgramName}, symbol: {externalPendingResolution.SymbolName}");
        }
    }

    private static void ProcessFile(ParsedProgram[] parsedPrograms)
    {
        foreach(var parsedProgram in parsedPrograms) {
            ProcessProgram(parsedProgram.ProgramName, parsedProgram.Parts);
            CleanupAfterProcessingProgram();
        }
    }

    private static bool currentProgramHasAbsoluteSegment;

    private static void ProcessProgram(string programName, IRelocatableFilePart[] programItems)
    {
        currentProgramSymbols.Clear();
        currentProgramHasAbsoluteSegment = false;

        currentProgramAbsoluteSegmentStart = 0xFFFF;
        currentProgramAbsoluteSegmentEnd = 0;

        currentProgramCommonBlockName = null;

        if(programName == null) {
            programName = (programItems.FirstOrDefault(x => x is LinkItem li && li.Type is LinkItemType.ProgramName) as LinkItem)?.Symbol ?? "";
        }
        currentProgramName = programName;

        var programSizeItem = programItems.FirstOrDefault(x => x is LinkItem li && li.Type is LinkItemType.ProgramAreaSize);
        ushort programSize = (programSizeItem as LinkItem)?.Address.Value ?? 0;

        var dataSizeItem = programItems.FirstOrDefault(x => x is LinkItem li && li.Type is LinkItemType.DataAreaSize);
        ushort dataSize = (dataSizeItem as LinkItem)?.Address.Value ?? 0;

        var programCommonBlocks = programItems.Where(x => x is LinkItem li && li.Type is LinkItemType.DefineCommonSize).Cast<LinkItem>();
        ushort commonsSize = 0;
        var commonBlocksAddedInThisProgram = new List<CommonBlock>();
        foreach(var commonBlock in programCommonBlocks) {
            var blockSize = commonBlock.Address.Value;
            if(commonBlocks.ContainsKey(commonBlock.Symbol)) {
                if(commonBlocks[commonBlock.Symbol].Size < blockSize) {
                    errors.Add($"Common block '{commonBlock.Symbol}' is defined with a size of {blockSize}, but it had been defined previously with a size of {commonBlocks[commonBlock.Symbol].Size}");
                }
            }
            else {
                var block = new CommonBlock() { Name = commonBlock.Symbol, StartAddress = commonsSize, Size = commonBlock.Address.Value, DefinedInProgram = currentProgramName }; //Real start address will be set later
                commonBlocksAddedInThisProgram.Add(block);
                commonBlocks.Add(block.Name, block);
                commonsSize += commonBlock.Address.Value;
            }
        }

        var previousProgram = programInfos.LastOrDefault(pi => pi.HasContent);

        if(segmentsSequencingMode is SegmentsSequencingMode.CombineSameSegment) {
            currentProgramCodeSegmentStart =
                codeSegmentAddressFromInput ?? (ushort)((previousProgram?.CodeSegmentEnd ?? DEFAULT_CODE_SEGMENT_START_ADDRESS - 1) + 1);

            // The "CombineSameSegment" mode is entered only after an explicit data segment address is supplied.
            // Thus, either it was supplied right before this program (and thus dataSegmentAddressFromInput
            // is not null), or it was supplied before one of the previous programs
            // (and thus previousProgram is not null).

            currentProgramCommonSegmentStart =
                dataSegmentAddressFromInput ?? (ushort)(previousProgram.DataSegmentEnd + 1);
            currentProgramDataSegmentStart = (ushort)(currentProgramCommonSegmentStart + commonsSize);
        }

        // The other two modes can't be (re-)entered once an address is specified for the data segment,
        // thus we can safely ignore dataSegmentAddressFromInput for these.

        else if(segmentsSequencingMode is SegmentsSequencingMode.CodeBeforeData) {
            currentProgramCodeSegmentStart =
                codeSegmentAddressFromInput ?? (ushort)((previousProgram?.MaxSegmentEnd ?? DEFAULT_CODE_SEGMENT_START_ADDRESS - 1) + 1);

            currentProgramCommonSegmentStart = (ushort)(currentProgramCodeSegmentStart + programSize);
            currentProgramDataSegmentStart = (ushort)(currentProgramCommonSegmentStart + commonsSize);
        }
        else if(segmentsSequencingMode is SegmentsSequencingMode.DataBeforeCode) {
            // Not a bug: in this mode the data segment really starts at the address
            // specified for the code segment. This is also how LINK-80 works.
            currentProgramCommonSegmentStart =
                codeSegmentAddressFromInput ?? (ushort)((previousProgram?.MaxSegmentEnd ?? DEFAULT_CODE_SEGMENT_START_ADDRESS - 1) + 1);
            currentProgramDataSegmentStart = (ushort)(currentProgramCommonSegmentStart + commonsSize);

            currentProgramCodeSegmentStart = (ushort)(currentProgramDataSegmentStart + dataSize);
        }
        else {
            throw new Exception($"Unexpected segments sequencing mode: {segmentsSequencingMode}");
        }

        foreach(var block in commonBlocksAddedInThisProgram) {
            block.StartAddress += currentProgramCommonSegmentStart;
        }

        currentProgramCodeSegmentEnd = programSize == 0 ? currentProgramCodeSegmentStart : (ushort)(currentProgramCodeSegmentStart + programSize - 1);
        currentProgramDataSegmentEnd = dataSize == 0 ? currentProgramDataSegmentStart : (ushort)(currentProgramDataSegmentStart + dataSize - 1);

        if(programSize > 0) {
            startAddress = Math.Min(startAddress, currentProgramCodeSegmentStart);
            endAddress = Math.Max(endAddress, currentProgramCodeSegmentEnd);
        }
        if(dataSize > 0 || commonsSize > 0) {
            startAddress = Math.Min(startAddress, currentProgramCommonSegmentStart);
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

            if(fileItem is ExtendedRelocatableFileHeader) {
                //It's an extended relocatable file: good to know, but nothing special to do about it
                continue;
            }
            else if(fileItem is RawBytes rawBytes) {
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
                var effectiveAddress = EffectiveAddressOf(relAdd);
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
                currentProgramAddress = EffectiveAddressOf(linkItem.Address);

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

                var effectiveAddress = EffectiveAddressOf(linkItem.Address);

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
                if(linkItem.Address.Type is AddressType.ASEG && linkItem.Address.Value is 0) {
                    //External references that are only used in expressions
                    //generate a "Chain external" link item with absolute address 0
                    //that must be ignored.
                    continue;
                }

                var chainStartAddress = EffectiveAddressOf(linkItem.Address);
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

                var parsedFileItems = RelocatableFileParser.Parse(stream).SelectMany(p => p.Parts).ToArray();
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
            else if(linkItem.Type is LinkItemType.SelectCommonBlock) {
                var blockName = linkItem.Symbol;
                if(!commonBlocks.ContainsKey(blockName)) {
                    throw new Exception($"Common block '{blockName}' selected without having been defined");
                }
                currentProgramCommonBlockName = blockName;
                //currentProgramAddress = commonBlocks[blockName].StartAddress;
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
            else if(linkItem.Type is LinkItemType.ProgramName 
                or LinkItemType.EntrySymbol 
                or LinkItemType.ProgramAreaSize 
                or LinkItemType.DataAreaSize
                or LinkItemType.DefineCommonSize) {
                // We have no use for EntrySymbol.
                // As for the others, we've already dealt with them.
                continue;
            }
            else if(linkItem.Type is LinkItemType.EndProgram) {
                break;
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
            CommonSegmentStart = currentProgramCommonSegmentStart,
            CommonSegmentEnd = (ushort)(currentProgramCommonSegmentStart + commonsSize - 1),
            AbsoluteSegmentStart = currentProgramAbsoluteSegmentStart,
            AbsoluteSegmentEnd = currentProgramAbsoluteSegmentEnd,
            ProgramName = currentProgramName,
            PublicSymbols = currentProgramSymbols.ToArray(),
            HasCode = programSize > 0,
            HasData = dataSize > 0,
            HasCommons = commonsSize > 0,
            HasAbsolute = currentProgramHasAbsoluteSegment,
            CommonBlocks = commonBlocksAddedInThisProgram.ToArray()
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

    private static ushort EffectiveAddressOf(RelocatableAddress address)
    {
        return (ushort)(
            address.Type is AddressType.CSEG ? currentProgramCodeSegmentStart + address.Value :
            address.Type is AddressType.DSEG ? currentProgramDataSegmentStart + address.Value :
            address.Type is AddressType.COMMON ? commonBlocks[currentProgramCommonBlockName].StartAddress + address.Value :
            address.Value
        );
    }

    private static void MaybeAdjustAbsoluteSegmentEnd()
    {
        if(currentAddressType is AddressType.ASEG) {
            currentProgramAbsoluteSegmentEnd = Math.Max((ushort)(currentProgramAddress-1), currentProgramAbsoluteSegmentEnd);
        }
    }

    private static void CleanupAfterProcessingProgram()
    {
        if(programInfos.Last().HasCode) {
            codeSegmentAddressFromInput = null;
        }

        if(programInfos.Last().HasData) {
            dataSegmentAddressFromInput = null;
        }
    }

    private static void AddError(string message, bool checkMaxErrors = true) { 
        errors.Add(message);
        LinkError?.Invoke(null, message);

        if(maxErrors == 0 || !checkMaxErrors) {
            return;
        }

        generatedErrors++;
        if(generatedErrors >= maxErrors) {
            throw new MaxErrorsReachedException();
        }
    }

    private static void AddWarning(string message)
    {
        warnings.Add(message);
        LinkWarning?.Invoke(null, message);
    }

    private static ushort Align(ushort address, ushort alignmentValue)
    {
        if(alignmentValue == 0) {
            AddError("Alignment value must be greater than 0");
            return address;
        }

        int newAddress = address + alignmentValue - 1;
        newAddress -= (newAddress % alignmentValue);
        if(newAddress > ushort.MaxValue) {
            AddError($"Alignment value {alignmentValue} is too large, it would cause the location pointer to exceed 65535");
            return address;
        }

        return (ushort)newAddress;
    }

    class MaxErrorsReachedException : Exception { }
}
