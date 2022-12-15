using Konamiman.Nestor80.Assembler.Output;

namespace Konamiman.Nestor80.Assembler
{
    public static class ListingFileGenerator
    {
        const int bytesPerRow = 4;

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
        static int outputBytesPrintedTotal;
        static int nextRelocatableIndex;

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
            var writtenOutputCharsCount = 0;

            if(processedLine is ChangeAreaLine cal) {
                if(!isAbsoluteBuild) {
                    changeAreaTo = cal.NewLocationArea;
                }
                mustPrintAddress = cal.NewLocationArea is not AddressType.COMMON;
            }
            else if(processedLine is IProducesOutput ipo) {
                if(ipo.OutputBytes.Length > 0) {
                    currentOutputProducerLine = ipo;
                    outputBytesPrintedTotal = 0;
                    nextRelocatableIndex = 0;
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

            writer.WriteLine(processedLine.Line);
        }

        private static void ProcessCurrentOutputLine()
        {
            var printedBytes = 0;
            var outputIndex = 0;
            var printedChars = 0;
            var mustPrintSource = outputBytesPrintedTotal == 0;

            PrintLineAddress(true);

            while(printedBytes < bytesPerRow) {
                if(nextRelocatableIndex < currentOutputProducerLine.RelocatableParts.Length && currentOutputProducerLine.RelocatableParts[nextRelocatableIndex].Index == outputIndex) {
                    var relocatable = currentOutputProducerLine.RelocatableParts[nextRelocatableIndex];
                    var relocatableSize = relocatable.IsByte ? 1 : 2;
                    if(printedBytes + relocatableSize > bytesPerRow) {
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
                        printedChars += relocatableSize == 1 ? 3 : 5;
                    }

                    printedBytes += relocatableSize;
                    outputBytesPrintedTotal += relocatableSize;
                    outputIndex += relocatableSize;
                }
                else {
                    var byteToPrint = currentOutputProducerLine.OutputBytes[outputIndex];
                    writer.Write($"{byteToPrint:X2} ");
                    outputIndex++;
                    printedChars++;
                }
            }

            locationCounters[currentLocationArea] += (ushort)printedBytes;

            if(mustPrintSource) {
                writer.WriteLine(((ProcessedSourceLine)currentOutputProducerLine).Line);
            }

            if(printedBytes >= bytesPerRow) {
                currentOutputProducerLine = null;
            }
        }

        private static void PrintLineAddress(bool printAddress)
        {
            if(printAddress) {
                writer.Write($"  {locationCounters[currentLocationArea]:X4}{addressSuffixes[currentLocationArea]} ");
            }
            else {
                writer.Write("        ");
            }

            writer.Write("   ");
        }
    }
}
