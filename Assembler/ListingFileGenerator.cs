using Konamiman.Nestor80.Assembler.Output;

namespace Konamiman.Nestor80.Assembler
{
    public static class ListingFileGenerator
    {
        const int bytesPerRow = 4;
        const int bytesAreaSize = 22;
        static string emptyBytesArea = new string(' ', bytesAreaSize);

        private static Dictionary<AddressType, string> addressSuffixes = new() {
            { AddressType.CSEG, "'" },
            { AddressType.DSEG, "\"" },
            { AddressType.ASEG, " " },
            { AddressType.COMMON, "!" },
        };

        private static StreamWriter writer;
        static AddressType currentLocationArea;
        static Dictionary<AddressType, ushort> locationCounters;
        static bool isAbsoluteBuild;

        static IProducesOutput currentOutputProducerLine;
        static int totalOutputBytesRemaining;
        static int nextRelocatableIndex;
        static int outputIndex;

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

            foreach(var resultItem in assemblyResult.ProcessedLines) {
                ProcessLine(resultItem);

                while(currentOutputProducerLine is not null) {
                    ProcessCurrentOutputLine();
                    writer.WriteLine(emptyBytesArea);
                }
            }

            writer.Flush();
            return (int)writer.BaseStream.Position;
        }

        private static void ProcessLine(ProcessedSourceLine processedLine)
        {
            var mustPrintAddress = processedLine.Label is not null;
            ushort increaseLocationCounterBy = 0;
            AddressType? changeAreaTo = null;
            //WIP: handle .phase
            if(processedLine is ChangeAreaLine cal) {
                if(!isAbsoluteBuild) {
                    changeAreaTo = cal.NewLocationArea;
                }
                mustPrintAddress = cal.NewLocationArea is not AddressType.COMMON;
            }
            else if(processedLine is IProducesOutput ipo) {
                if(ipo.OutputBytes.Length > 0) {
                    currentOutputProducerLine = ipo;
                    totalOutputBytesRemaining = ipo.OutputBytes.Length;
                    nextRelocatableIndex = 0;
                    outputIndex = 0;
                    ProcessCurrentOutputLine();
                    writer.WriteLine(processedLine.Line);
                    return;
                }
                else {
                    mustPrintAddress = true;
                }
            }
            else if(processedLine is DefineSpaceLine dsl) {
                mustPrintAddress = true;
                increaseLocationCounterBy = dsl.Size;
            }
            else if(processedLine is ChangeOriginLine col) {
                locationCounters[currentLocationArea] = col.NewLocationCounter;
            }

            PrintLineAddress(mustPrintAddress);

            if(changeAreaTo.HasValue) {
                currentLocationArea = changeAreaTo.Value;
            }
            if(increaseLocationCounterBy > 0) {
                locationCounters[currentLocationArea] += increaseLocationCounterBy;
            }

            writer.Write(emptyBytesArea);
            writer.WriteLine(processedLine.Line);
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
                    locationCounters[currentLocationArea] += (ushort)relocatableSize;

                    nextRelocatableIndex++;
                }
                else {
                    var byteToPrint = currentOutputProducerLine.OutputBytes[outputIndex];
                    writer.Write($"{byteToPrint:X2} ");
                    bytesRemainingInRow--;
                    totalOutputBytesRemaining--;
                    outputIndex++;
                    printedChars += 3;
                    locationCounters[currentLocationArea]++;
                }
            }

            writer.Write(new string(' ', bytesAreaSize - printedChars));

            if(totalOutputBytesRemaining == 0) {
                currentOutputProducerLine = null;
            }
        }

        private static void PrintLineAddress(bool printAddress)
        {
            if(printAddress) {
                writer.Write($"  {locationCounters[currentLocationArea]:X4}{addressSuffixes[currentLocationArea]}");
            }
            else {
                writer.Write("       ");
            }

            writer.Write("   ");
        }
    }
}
