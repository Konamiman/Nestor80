using Konamiman.Nestor80.Assembler.Output;
using System.Text;

namespace Konamiman.Nestor80.Assembler
{
    public static class ListingFileGenerator
    {
        private static int bytesAreaSize;
        const int extraFlagsAreaSize = 7;
        static string emptyBytesArea;
        static readonly string emptyExtraFlagsArea = new(' ', extraFlagsAreaSize);
        static readonly string flagsAreaWithPlus = "+" + new string(' ', extraFlagsAreaSize-1);
        static readonly string flagsAreaWithC = " C" + new string(' ', extraFlagsAreaSize - 2);
        static readonly string flagsAreaWithCAndPlus = "+C" + new string(' ', extraFlagsAreaSize - 2);

        private static ListingFileConfiguration config;

        private static readonly Dictionary<AddressType, string> addressSuffixes = new() {
            { AddressType.CSEG, "'" },
            { AddressType.DSEG, "\"" },
            { AddressType.ASEG, " " },
            { AddressType.COMMON, "!" },
        };

        private static readonly StringBuilder sb = new();

        private static StreamWriter writer;
        static AddressType currentLocationArea;
        static Dictionary<AddressType, ushort> locationCounters;
        static bool isAbsoluteBuild;
        static AddressType currentPhasedLocationArea;
        static ushort currentPhasedLocationCounter;
        static bool isPhased = false;

        static IProducesOutput currentOutputProducerLine;
        static int totalOutputBytesRemaining;
        static int nextRelocatableIndex;
        static int outputIndex;

        private static bool listMacroExpansionProducingOutput;
        private static bool listMacroExpansionNotProducingOutput;
        private static int includeDepthLevel;
        private static int macroExpansionDepthLevel;
        private static MacroExpansionLine currentMacroLine;
        private static ConstantDefinitionLine currentConstantDefinitionLine;

        static bool currentlyListingFalseConditionals;
        static bool tfcondState;

        static int mainPageNumber;
        static int subPageNumber;
        static int linesPerPage;
        static int printedLines;
        static string title;
        static string subtitle;

        static bool printingSymbols;
        static bool listingActive;

        public static int GenerateListingFile(AssemblyResult assemblyResult, StreamWriter writer, ListingFileConfiguration config)
        {
            if(!config.ListCode && !config.ListSymbols) {
                // ¯\_(ツ)_/¯
                return 0;
            }

            ListingFileGenerator.writer = writer;
            ListingFileGenerator.config = config;
            isAbsoluteBuild = assemblyResult.BuildType == BuildType.Absolute;
            currentLocationArea = isAbsoluteBuild ? AddressType.ASEG : AddressType.CSEG;
            locationCounters = new Dictionary<AddressType, ushort>() {
                { AddressType.CSEG, 0 },
                { AddressType.DSEG, 0 },
                { AddressType.ASEG, 0 },
                { AddressType.COMMON, 0 }
            };

            bytesAreaSize = (config.BytesPerRow * 3) + 3;
            emptyBytesArea = new(' ', bytesAreaSize);

            includeDepthLevel = 0;
            macroExpansionDepthLevel = 0;
            currentMacroLine = null;
            listMacroExpansionProducingOutput = true;
            listMacroExpansionNotProducingOutput = false;

            tfcondState = config.ListFalseConditionals;
            currentlyListingFalseConditionals = tfcondState;

            mainPageNumber = 0;
            subPageNumber = 0;
            linesPerPage = 50;
            printedLines = 0;

            printingSymbols = false;
            listingActive = true;

            var titleLine = assemblyResult.ProcessedLines.FirstOrDefault(l => l is SetListingTitleLine);
            title = titleLine is null ? "" : ((SetListingTitleLine)titleLine).Title;
            var subtitleLine = assemblyResult.ProcessedLines.FirstOrDefault(l => l is SetListingSubtitleLine);
            subtitle = subtitleLine is null ? "" : ((SetListingSubtitleLine)subtitleLine).Subtitle;

            if(config.ListCode) {
                DoPageChange(1);
                ProcessLines(assemblyResult.ProcessedLines);
            }

            if(config.ListSymbols) {
                PrintSymbols(assemblyResult.BuildType, assemblyResult.Symbols, assemblyResult.MacroNames, assemblyResult.CommonAreaSizes.Keys.ToArray());
            }

            writer.Flush();
            return (int)writer.BaseStream.Position;
        }

        private static void ProcessLines(ProcessedSourceLine[] lines)
        {
            foreach(var resultItem in lines) {
                ProcessLine(resultItem);

                while(currentOutputProducerLine is not null) {
                    ProcessCurrentOutputLine();
                    WriteBufferedText();
                }
            }
        }

        private static void ProcessLine(ProcessedSourceLine processedLine)
        {
            void ProcessMacroExpansionLine(MacroExpansionLine mel) {
                if(listMacroExpansionNotProducingOutput || macroExpansionDepthLevel == 0) {
                    PrintLineAddress(false);
                    sb.Append(emptyBytesArea);
                    PrintExtraFlags();
                    PrintLineSource(processedLine);
                }

                if(listMacroExpansionProducingOutput || listMacroExpansionNotProducingOutput) {
                    macroExpansionDepthLevel++;
                    ProcessLines(mel.Lines);
                    macroExpansionDepthLevel--;
                }
            }

            var mustPrintAddress = processedLine.Label is not null;
            ushort increaseLocationCounterBy = 0;
            AddressType? changeAreaTo = null;
            var incrementSubpage = false;
            currentConstantDefinitionLine = null;

            void PrintLineAsIs()
            {
                PrintLineAddress(mustPrintAddress);
                sb.Append(emptyBytesArea);
                PrintExtraFlags();
                PrintLineSource(processedLine);
            }

            var mainPagesChange = processedLine.FormFeedsCount;
            if(processedLine is ChangeListingPageLine clpl && clpl.IsMainPageChange) {
                mainPagesChange++;
                //We want the "mainpage" instruction itself to be printed
                //before the actual page change happens
                PrintLineAsIs();
                DoPageChange(mainPagesChange);
                return;
            }

            if(processedLine is SkippedLine && !currentlyListingFalseConditionals) {
                return;
            }

            if(currentMacroLine is not null && processedLine is not MacroDefinitionBodyLine) {
                var mel = currentMacroLine;
                currentMacroLine = null;
                ProcessMacroExpansionLine(mel);
                return;
            }

            if(processedLine is ConstantDefinitionLine cdl) {
                currentConstantDefinitionLine = cdl;
            }
            else if(processedLine is IProducesOutput ipo) {
                if(macroExpansionDepthLevel > 0 && !listMacroExpansionProducingOutput) {
                    return;
                }
                if(ipo.OutputBytes.Length > 0) {
                    currentOutputProducerLine = ipo;
                    totalOutputBytesRemaining = ipo.OutputBytes.Length;
                    nextRelocatableIndex = 0;
                    outputIndex = 0;
                    ProcessCurrentOutputLine();
                    PrintLineSource(processedLine);
                    return;
                }
                else {
                    mustPrintAddress = true;
                }
            }
            else if(processedLine is DefineSpaceLine dsl) {
                if(macroExpansionDepthLevel > 0 && !listMacroExpansionProducingOutput) {
                    return;
                }
                mustPrintAddress = true;
                increaseLocationCounterBy = dsl.Size;
            }
            else if(processedLine is MacroExpansionLine mel) {
                if(mel.MacroType is MacroType.Named) {
                    ProcessMacroExpansionLine(mel);
                    return;
                }
                else {
                    currentMacroLine = mel;
                }
            }
            else if(processedLine is IncludeLine incl) {
                includeDepthLevel++;
                PrintLineAsIs();
                ProcessLines(incl.Lines);
                includeDepthLevel--;
                return;
            }
            else if(processedLine is ListingControlLine lcl) {
                if(lcl.Type is ListingControlType.Lall) {
                    listMacroExpansionProducingOutput = true;
                    listMacroExpansionNotProducingOutput = true;
                }
                else if(lcl.Type is ListingControlType.Sall) {
                    listMacroExpansionProducingOutput = false;
                    listMacroExpansionNotProducingOutput = false;
                }
                else if(lcl.Type is ListingControlType.Xall) {
                    listMacroExpansionProducingOutput = true;
                    listMacroExpansionNotProducingOutput = false;
                }
                else if(lcl.Type is ListingControlType.Lfcond) {
                    currentlyListingFalseConditionals = true;
                }
                else if(lcl.Type is ListingControlType.Sfcond) {
                    currentlyListingFalseConditionals = false;
                }
                else if(lcl.Type is ListingControlType.Tfcond) {
                    tfcondState = !tfcondState;
                    currentlyListingFalseConditionals = tfcondState;
                }
                else if(lcl.Type is ListingControlType.List) {
                    listingActive = true;
                }
                else if(lcl.Type is ListingControlType.XList) {
                    listingActive = false;
                }
            }
            else if(processedLine is ChangeListingPageLine clpl2 && !clpl2.IsMainPageChange) {
                incrementSubpage = true;
                if(clpl2.NewPageSize != 0) {
                    linesPerPage = clpl2.NewPageSize;
                }
            }
            else if(processedLine is SetListingSubtitleLine slsl) {
                subtitle = slsl.Subtitle;
            }

            if(macroExpansionDepthLevel > 0 && !listMacroExpansionNotProducingOutput) {
                if(mainPagesChange > 0) {
                    DoPageChange(mainPagesChange);
                }
                return;
            }

            if(processedLine is ChangeAreaLine cal) {
                if(!isAbsoluteBuild) {
                    changeAreaTo = cal.NewLocationArea;
                    if(cal.NewLocationArea is AddressType.COMMON) {
                        locationCounters[AddressType.COMMON] = 0;
                    }
                }
                mustPrintAddress = cal.NewLocationArea is not AddressType.COMMON;
            }
            else if(processedLine is ChangeOriginLine col) {
                locationCounters[currentLocationArea] = col.NewLocationCounter;
            }
            else if(processedLine is PhaseLine phl) {
                currentPhasedLocationArea = phl.NewLocationArea;
                currentPhasedLocationCounter = phl.NewLocationCounter;
                isPhased = true;
            }
            else if(processedLine is DephaseLine) {
                isPhased = false;
            }

            if(mainPagesChange > 0) {
                DoPageChange(mainPagesChange);
            }

            PrintLineAddress(mustPrintAddress);

            if(changeAreaTo.HasValue) {
                currentLocationArea = changeAreaTo.Value;
            }
            if(increaseLocationCounterBy > 0) {
                IncreaseLocationCounter(increaseLocationCounterBy);
            }

            sb.Append(emptyBytesArea);
            PrintExtraFlags();
            PrintLineSource(processedLine);

            if(incrementSubpage) {
                DoPageChange(0);
            }
        }

        private static void ProcessCurrentOutputLine()
        {
            var bytesRemainingInRow = Math.Min(totalOutputBytesRemaining, config.BytesPerRow);
            var printedChars = 0;

            PrintLineAddress(true);

            while(bytesRemainingInRow > 0) {
                if(nextRelocatableIndex < currentOutputProducerLine.RelocatableParts.Length && currentOutputProducerLine.RelocatableParts[nextRelocatableIndex].Index == outputIndex) {
                    var relocatable = currentOutputProducerLine.RelocatableParts[nextRelocatableIndex];
                    var relocatableSize = relocatable.IsByte ? 1 : 2;
                    if(relocatableSize > bytesRemainingInRow) {
                        break;
                    }

                    if(relocatable is LinkItemsGroup) {
                        sb.Append(relocatableSize == 1 ? "00*" : "0000*");
                        printedChars += relocatableSize == 1 ? 3 : 5;
                    }
                    else {
                        var relocatableAddress = (RelocatableAddress)relocatable;
                        var value = relocatableAddress.Value;
                        sb.Append(relocatableSize == 1 ? $"{value:X2}" : $"{value:X4}");
                        sb.Append(addressSuffixes[relocatableAddress.Type]);
                        if(relocatableSize is 2 && relocatableAddress.Type is not AddressType.ASEG) {
                            sb.Append(' ');
                            printedChars++;
                        }
                        printedChars += relocatableSize == 1 ? 3 : 5;
                    }

                    bytesRemainingInRow -= relocatableSize;
                    totalOutputBytesRemaining -= relocatableSize;
                    outputIndex += relocatableSize;
                    IncreaseLocationCounter((ushort)relocatableSize);

                    nextRelocatableIndex++;
                }
                else {
                    var byteToPrint = currentOutputProducerLine.OutputBytes[outputIndex];
                    sb.Append($"{byteToPrint:X2} ");
                    bytesRemainingInRow--;
                    totalOutputBytesRemaining--;
                    outputIndex++;
                    printedChars += 3;
                    IncreaseLocationCounter(1);
                }
            }

            sb.Append(new string(' ', bytesAreaSize - printedChars));
            PrintExtraFlags();

            if(totalOutputBytesRemaining == 0) {
                currentOutputProducerLine = null;
            }
        }

        private static void IncreaseLocationCounter(ushort value)
        {
            locationCounters[currentLocationArea] += value;
            if(isPhased) {
                currentPhasedLocationCounter += value;
            }
        }

        private static void PrintLineAddress(bool printAddress)
        {
            if(currentConstantDefinitionLine is not null) {
                sb.Append($"  {currentConstantDefinitionLine.Value:X4}{addressSuffixes[currentConstantDefinitionLine.ValueArea]}");
                currentConstantDefinitionLine = null;
            }
            else if(printAddress) {
                var area = isPhased ? currentPhasedLocationArea : currentLocationArea;
                var counter = isPhased ? currentPhasedLocationCounter : locationCounters[currentLocationArea];
                sb.Append($"  {counter:X4}{addressSuffixes[area]}");
            }
            else {
                sb.Append("       ");
            }

            sb.Append("   ");
        }

        private static void PrintExtraFlags()
        {
            if(macroExpansionDepthLevel > 0 && includeDepthLevel == 0) {
                sb.Append(flagsAreaWithPlus);
            }
            else if(macroExpansionDepthLevel == 0 && includeDepthLevel == 1) {
                sb.Append(flagsAreaWithC);
            }
            else if(macroExpansionDepthLevel > 0 && includeDepthLevel == 1) {
                sb.Append(flagsAreaWithCAndPlus);
            }
            else if(macroExpansionDepthLevel == 0 && includeDepthLevel > 1) {
                sb.Append($" C{includeDepthLevel}".PadRight(extraFlagsAreaSize));
            }
            else if(macroExpansionDepthLevel > 0 && includeDepthLevel > 1) {
                sb.Append($"+C{includeDepthLevel}".PadRight(extraFlagsAreaSize));
            }
            else {
                sb.Append(emptyExtraFlagsArea);
            }
        }

        private static void PrintLineSource(ProcessedSourceLine line)
        {
            if(macroExpansionDepthLevel == 0) {
                sb.Append(line.Line);
                WriteBufferedText();
                return;
            }
            try {
                if(line.Line.Length >= line.EffectiveLineLength + 2 && line.Line.Substring(line.EffectiveLineLength, 2) == ";;") {
                    var lineWithoutComment = line.Line[..line.EffectiveLineLength];
                    if(lineWithoutComment.Length > 0) {
                        sb.Append(lineWithoutComment);
                    }
                    else {
                        //Line is empty after removing the ";;" comment? We don't want to print anything at all then
                        sb.Clear();
                        return;
                    }
                }
                else {
                    sb.Append(line.Line);
                }
            }
            catch {
                sb.Append(line.Line);
            }

            WriteBufferedText();
        }

        private static void WriteBufferedText()
        {
            PrintLine(sb.ToString());
            sb.Clear();
        }

        private static void PrintLine(string value = "")
        {
            if(!listingActive) {
                return;
            }

            writer.WriteLine(value);
            printedLines++;
            if(printedLines == linesPerPage) {
                DoPageChange(0);
                printedLines = 0;
            }
        }

        private static void DoPageChange(int mainPagesIncrement)
        {
            if(mainPagesIncrement > 0) {
                mainPageNumber += mainPagesIncrement;
                subPageNumber = 0;
            }
            else {
                subPageNumber++;
            }

            var titleLine = $"\f{title}\t{config.TitleSignature}\tPAGE\t{(printingSymbols ? "S" : mainPageNumber)}{(subPageNumber == 0 ? "" : $"-{subPageNumber}")}";
            writer.WriteLine(titleLine);
            writer.WriteLine(subtitle);
            writer.WriteLine();
            if(mainPagesIncrement == 0) {
                writer.WriteLine();
                printedLines = 4;
            }
            else {
                printedLines = 3;
            }
        }

        private static void PrintSymbols(BuildType buildType, Symbol[] symbols, string[] macroNames, string[] commonAreaNames)
        {
            printingSymbols = true;
            DoPageChange(1);

            if(buildType is BuildType.Absolute) {
                PrintLine();
                if(symbols.Length > 0) {
                    PrintLine("Symbols:");
                    PrintSymbolsCollection(symbols);
                }
                else {
                    PrintLine("No symbols defined");
                }
            }
            else {
                PrintLine();
                var localSymbols = symbols.Where(s => !s.IsPublic && s.Type != SymbolType.External).ToArray();
                if(symbols.Length > 0) {
                    PrintLine("Local symbols:");
                    PrintSymbolsCollection(symbols.Where(s => !s.IsPublic && s.Type != SymbolType.External).ToArray());
                }
                else {
                    PrintLine("No local symbols defined");
                }

                PrintLine();
                var publicSymbols = symbols.Where(s => s.IsPublic).ToArray();
                if(publicSymbols.Length > 0) {
                    PrintLine("Public symbols:");
                    PrintSymbolsCollection(symbols.Where(s => s.IsPublic).ToArray());
                }
                else {
                    PrintLine("No public symbols defined");
                }

                PrintLine();
                var externalSymbols = symbols.Where(s => s.Type is SymbolType.External).ToArray();
                if(externalSymbols.Length > 0) {
                    PrintLine("External symbols:");
                    PrintSymbolsCollection(symbols.Where(s => s.Type is SymbolType.External).ToArray(), false);
                }
                else {
                    PrintLine("No external symbols defined");
                }
            }

            PrintLine();
            if(macroNames.Length > 0) {
                PrintLine("Named macros:");
                PrintSymbolsCollection(macroNames);
            }
            else {
                PrintLine("No named macros defined");
            }

            if(commonAreaNames.Length > 0) {
                PrintLine();
                PrintLine("Common areas:");
                PrintSymbolsCollection(commonAreaNames);
            }
        }

        private static void PrintSymbolsCollection(Symbol[] symbolObjects, bool printValue = true)
        {
            var symbols = symbolObjects.Select(s => new { Name = config.UppercaseSymbolNames ? s.Name.ToUpper() : s.Name, s.Value, s.ValueArea }).ToArray();

            var symbolsPrintedInRow = 0;
            sb.Clear();

            foreach(var symbol in symbols.OrderBy(s => s.Name).ToArray()) {
                if(printValue) {
                    sb.Append($"{symbol.Value:X4}{addressSuffixes[symbol.ValueArea]}\t");
                }
                var symbolName = symbol.Name.Length > config.MaxSymbolLength ? symbol.Name[..(config.MaxSymbolLength - 3)] + "..." : symbol.Name.PadRight(config.MaxSymbolLength, ' ');
                sb.Append(symbolName);
                sb.Append('\t');

                symbolsPrintedInRow++;
                if(symbolsPrintedInRow == config.SymbolsPerRow) {
                    WriteBufferedText();
                    symbolsPrintedInRow = 0;
                }
            }

            if(symbolsPrintedInRow > 0) {
                WriteBufferedText();
            }
        }

        private static void PrintSymbolsCollection(string[] symbols)
        {
            if(config.UppercaseSymbolNames) {
                symbols = symbols.Select(s => s.ToUpper()).ToArray();
            }

            var symbolsPrintedInRow = 0;
            sb.Clear();

            foreach(var symbol in symbols.OrderBy(s => s).ToArray()) {
                var symbolName = symbol.Length > config.MaxSymbolLength ? symbol[..(config.MaxSymbolLength - 3)] + "..." : symbol.PadRight(config.MaxSymbolLength, ' ');
                sb.Append(symbolName);
                sb.Append('\t');

                symbolsPrintedInRow++;
                if(symbolsPrintedInRow == config.SymbolsPerRow) {
                    WriteBufferedText();
                    symbolsPrintedInRow = 0;
                }
            }

            if(symbolsPrintedInRow > 0) {
                WriteBufferedText();
            }
        }
    }
}
