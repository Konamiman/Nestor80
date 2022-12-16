using Konamiman.Nestor80.Assembler.Output;
using System.Diagnostics;

namespace Konamiman.Nestor80.Assembler
{
    public static class ListingFileGenerator
    {
        const int bytesPerRow = 4;
        const int bytesAreaSize = 15;
        const int extraFlagsAreaSize = 7;
        static readonly string emptyBytesArea = new(' ', bytesAreaSize);
        static readonly string emptyExtraFlagsArea = new(' ', extraFlagsAreaSize);
        static readonly string flagsAreaWithPlus = "+" + new string(' ', extraFlagsAreaSize-1);
        static readonly string flagsAreaWithC = " C" + new string(' ', extraFlagsAreaSize - 2);
        static readonly string flagsAreaWithCAndPlus = "+C" + new string(' ', extraFlagsAreaSize - 2);

        private static readonly Dictionary<AddressType, string> addressSuffixes = new() {
            { AddressType.CSEG, "'" },
            { AddressType.DSEG, "\"" },
            { AddressType.ASEG, " " },
            { AddressType.COMMON, "!" },
        };

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

        static bool currentlyListingFalseConditionals;
        static bool tfcondState;

        public static int GenerateListingFile(AssemblyResult assemblyResult, StreamWriter writer)
        {
            ListingFileGenerator.writer = writer;
            isAbsoluteBuild = assemblyResult.BuildType == BuildType.Absolute;
            currentLocationArea = isAbsoluteBuild ? AddressType.ASEG : AddressType.CSEG;
            locationCounters = new Dictionary<AddressType, ushort>() {
                { AddressType.CSEG, 0 },
                { AddressType.DSEG, 0 },
                { AddressType.ASEG, 0 },
                { AddressType.COMMON, 0 }
            };

            includeDepthLevel = 0;
            macroExpansionDepthLevel = 0;
            currentMacroLine = null;
            listMacroExpansionProducingOutput = true;
            listMacroExpansionNotProducingOutput = false;

            tfcondState = true;
            currentlyListingFalseConditionals = tfcondState;

            ProcessLines(assemblyResult.ProcessedLines);

            writer.Flush();
            return (int)writer.BaseStream.Position;
        }

        private static void ProcessLines(ProcessedSourceLine[] lines)
        {
            foreach(var resultItem in lines) {
                ProcessLine(resultItem);

                while(currentOutputProducerLine is not null) {
                    ProcessCurrentOutputLine();
                    writer.WriteLine("");
                }
            }
        }

        private static void ProcessLine(ProcessedSourceLine processedLine)
        {
            void ProcessMacroExpansionLine(MacroExpansionLine mel) {
                PrintLineAddress(false);
                writer.Write(emptyBytesArea);
                PrintExtraFlags();
                PrintLine(processedLine);
                if(listMacroExpansionProducingOutput || listMacroExpansionNotProducingOutput) {
                    macroExpansionDepthLevel++;
                    ProcessLines(mel.Lines);
                    macroExpansionDepthLevel--;
                }
            }

            var mustPrintAddress = processedLine.Label is not null;
            ushort increaseLocationCounterBy = 0;
            AddressType? changeAreaTo = null;
            //WIP: handle (sub)titles, page breaks, ;; should not print blank line in macro expansions

            if(processedLine is SkippedLine && !currentlyListingFalseConditionals) {
                return;
            }

            if(currentMacroLine is not null && processedLine is not MacroDefinitionBodyLine) {
                var mel = currentMacroLine;
                currentMacroLine = null;
                ProcessMacroExpansionLine(mel);
                return;
            }

            if(processedLine is IProducesOutput ipo) {
                if(macroExpansionDepthLevel > 0 && !listMacroExpansionProducingOutput) {
                    return;
                }
                if(ipo.OutputBytes.Length > 0) {
                    currentOutputProducerLine = ipo;
                    totalOutputBytesRemaining = ipo.OutputBytes.Length;
                    nextRelocatableIndex = 0;
                    outputIndex = 0;
                    ProcessCurrentOutputLine();
                    PrintLine(processedLine);
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
                PrintLineAddress(false);
                writer.Write(emptyBytesArea);
                PrintExtraFlags();
                PrintLine(processedLine);
                includeDepthLevel++;
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
            }

            if(macroExpansionDepthLevel > 0 && !listMacroExpansionNotProducingOutput) {
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

            PrintLineAddress(mustPrintAddress);

            if(changeAreaTo.HasValue) {
                currentLocationArea = changeAreaTo.Value;
            }
            if(increaseLocationCounterBy > 0) {
                IncreaseLocationCounter(increaseLocationCounterBy);
            }

            writer.Write(emptyBytesArea);
            PrintExtraFlags();
            PrintLine(processedLine);
        }

        private static void ProcessCurrentOutputLine()
        {
            var bytesRemainingInRow = Math.Min(totalOutputBytesRemaining, bytesPerRow);
            var printedChars = 0;

            PrintLineAddress(true);

            while(bytesRemainingInRow > 0) {
                if(nextRelocatableIndex < currentOutputProducerLine.RelocatableParts.Length && currentOutputProducerLine.RelocatableParts[nextRelocatableIndex].Index == outputIndex) {
                    var relocatable = currentOutputProducerLine.RelocatableParts[nextRelocatableIndex];
                    var relocatableSize = relocatable.IsByte ? 1 : 2;
                    if(relocatableSize > bytesRemainingInRow) {
                        break;
                    }

                    if(relocatable is LinkItemsGroup lig) {
                        writer.Write(relocatableSize == 1 ? "00*" : "0000*");
                        printedChars += relocatableSize == 1 ? 3 : 5;
                    }
                    else {
                        var relocatableAddress = (RelocatableAddress)relocatable;
                        var value = relocatableAddress.Value;
                        writer.Write(relocatableSize == 1 ? $"{value:X2}" : $"{value:X4}");
                        writer.Write(addressSuffixes[relocatableAddress.Type]);
                        if(relocatableSize is 2 && relocatableAddress.Type is not AddressType.ASEG) {
                            writer.Write(" ");
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
                    writer.Write($"{byteToPrint:X2} ");
                    bytesRemainingInRow--;
                    totalOutputBytesRemaining--;
                    outputIndex++;
                    printedChars += 3;
                    IncreaseLocationCounter(1);
                }
            }

            writer.Write(new string(' ', bytesAreaSize - printedChars));
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
            if(printAddress) {
                var area = isPhased ? currentPhasedLocationArea : currentLocationArea;
                var counter = isPhased ? currentPhasedLocationCounter : locationCounters[currentLocationArea];
                writer.Write($"  {counter:X4}{addressSuffixes[area]}");
            }
            else {
                writer.Write("       ");
            }

            writer.Write("   ");
        }

        private static void PrintExtraFlags()
        {
            if(macroExpansionDepthLevel > 0 && includeDepthLevel == 0) {
                writer.Write(flagsAreaWithPlus);
            }
            else if(macroExpansionDepthLevel == 0 && includeDepthLevel == 1) {
                writer.Write(flagsAreaWithC);
            }
            else if(macroExpansionDepthLevel > 0 && includeDepthLevel == 1) {
                writer.Write(flagsAreaWithCAndPlus);
            }
            else if(macroExpansionDepthLevel == 0 && includeDepthLevel > 1) {
                writer.Write($" C{includeDepthLevel}".PadRight(extraFlagsAreaSize));
            }
            else if(macroExpansionDepthLevel > 0 && includeDepthLevel > 1) {
                writer.Write($"+C{includeDepthLevel}".PadRight(extraFlagsAreaSize));
            }
            else {
                writer.Write(emptyExtraFlagsArea);
            }
        }

        private static void PrintLine(ProcessedSourceLine line)
        {
            if(macroExpansionDepthLevel == 0) {
                writer.WriteLine(line.Line);
                return;
            }
            try {
                if(line.Line.Length >= line.EffectiveLineLength + 2 && line.Line.Substring(line.EffectiveLineLength, 2) == ";;") {
                    var lineWithoutComment = line.Line[..line.EffectiveLineLength];
                    if(lineWithoutComment.Length > 0) {
                        writer.WriteLine(lineWithoutComment);
                    }
                    else {
                        writer.WriteLine("");
                    }
                }
                else {
                    writer.WriteLine(line.Line);
                }
            }
            catch {
                writer.WriteLine(line.Line);
            }
        }
    }
}
