using Konamiman.Nestor80.Assembler.Output;

namespace Konamiman.Nestor80.Assembler
{
    public static class ListingFileGenerator
    {
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
            }

            writer.Flush();
            return (int)writer.BaseStream.Position;
        }

        private static void ProcessLine(ProcessedSourceLine processedLine)
        {
            var mustPrintAddress = processedLine.Label is not null;
            ushort increaseLocationCounterBy = 0;
            AddressType? changeAreaTo = null;
            

            if(processedLine is ChangeAreaLine cal) {
                if(!isAbsoluteBuild) {
                    changeAreaTo = cal.NewLocationArea;
                }
                mustPrintAddress = cal.NewLocationArea is not AddressType.COMMON;
            }
            else if(processedLine is IProducesOutput ipo) {
                mustPrintAddress = true;
                increaseLocationCounterBy = (ushort)ipo.OutputBytes.Length; 
            }
            else if(processedLine is DefineSpaceLine dsl) {
                mustPrintAddress = true;
                increaseLocationCounterBy = dsl.Size;
            }
            else if(processedLine is ChangeOriginLine col) {
                locationCounters[currentLocationArea] = col.NewLocationCounter;
            }
            
            if(mustPrintAddress) {
                writer.Write($"  {locationCounters[currentLocationArea]:X4}{addressSuffixes[currentLocationArea]} ");
            }
            else {
                writer.Write("        ");
            }

            if(changeAreaTo.HasValue) {
                currentLocationArea = changeAreaTo.Value;
            }
            if(increaseLocationCounterBy > 0) {
                locationCounters[currentLocationArea] += increaseLocationCounterBy;
            }

            writer.WriteLine(processedLine.Line);
        }
    }
}
