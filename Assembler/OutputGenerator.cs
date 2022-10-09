using Konamiman.Nestor80.Assembler.Output;

namespace Konamiman.Nestor80.Assembler
{
    public static class OutputGenerator
    {
        public static int GenerateAbsolute(AssemblyResult assemblyResult, Stream outputStream, bool orgAsPhase = false)
        {
            var memory = new byte[65536];
            var firstAddress = 0;
            var lastAddressPlusOne = 0;
            var currentAddress = 0;

            if(assemblyResult.BuildType != BuildType.Absolute) {
                throw new ArgumentException("Absolute output can be genereated only for assembly results with a built type of 'Absolute'");
            }

            var lines = FlatLinesList(assemblyResult.ProcessedLines);
            var addressDecidingLine = lines.FirstOrDefault(l => l is ChangeOriginLine or IProducesOutput);
            if(addressDecidingLine is ChangeOriginLine first_chol) {
                firstAddress = first_chol.NewLocationCounter;
                currentAddress = firstAddress;
            }

            //We do a deferred location counter update to prevent an ORG at the end of the file
            //(not followed by more output) from affecting the final output size
            int newLocationCounter = -1;

            foreach(var line in lines) {
                if(newLocationCounter != -1 && line is IProducesOutput or DefineSpaceLine) {
                    firstAddress = Math.Min(firstAddress, newLocationCounter);
                    lastAddressPlusOne = Math.Max(lastAddressPlusOne, newLocationCounter);
                    currentAddress = newLocationCounter;
                    newLocationCounter = -1;
                }

                if(line is ChangeOriginLine chol) {
                    if(!orgAsPhase) {
                        newLocationCounter = chol.NewLocationCounter;
                    }
                }
                else if(line is IProducesOutput ipo) {
                    var length = ipo.OutputBytes.Length;
                    if(currentAddress + length <= 65536) {
                        Array.Copy(ipo.OutputBytes, 0, memory, currentAddress, ipo.OutputBytes.Length);
                        currentAddress += length;
                    }
                    else {
                        var length1 = 65536 - currentAddress;
                        var length2 = length - length1;
                        Array.Copy(ipo.OutputBytes, 0, memory, currentAddress, length1);
                        Array.Copy(ipo.OutputBytes, length1, memory, 0, length2);
                        currentAddress = length2;
                        firstAddress = 0;
                        lastAddressPlusOne = 65536;
                    }
                }
                else if(line is DefineSpaceLine ds) {
                    var outputByte = ds.Value ?? 0;
                    var length = ds.Size;
                    if(currentAddress + length <= 65536) {
                        Array.Fill(memory, outputByte, currentAddress, length);
                        currentAddress += length;
                    }
                    else {
                        var length1 = 65536 - currentAddress;
                        var length2 = length - length1;
                        Array.Fill(memory, outputByte, currentAddress, length1);
                        Array.Fill(memory, outputByte, 0, length2);
                        currentAddress = length2;
                        firstAddress = 0;
                        lastAddressPlusOne = 65536;
                    }
                }
                else if(line is AssemblyEndLine or EndOutputLine) {
                    break;
                }

                if(currentAddress >= 65536) {
                    firstAddress = 0;
                    currentAddress = 0;
                    lastAddressPlusOne = 65536;
                }
                else {
                    lastAddressPlusOne = Math.Max(lastAddressPlusOne, currentAddress);
                }
            }

            var outputSize = lastAddressPlusOne-firstAddress;
            if(outputSize > 0) {
                outputStream.Write(memory, firstAddress, outputSize);
            }

            return outputSize;
        }

        private static ProcessedSourceLine[] FlatLinesList(ProcessedSourceLine[] lines)
        {
            if(!lines.Any(l => l is IncludeLine)) {
                return lines;
            }

            var result = new List<ProcessedSourceLine>();

            foreach(var line in lines) {
                if(line is IncludeLine il) {
                    result.Add(line);
                    result.AddRange(FlatLinesList(il.Lines));
                    result.Add(new IncludeLine());
                }
                else {
                    result.Add(line);
                }
            }

            return result.ToArray();
        }
    }
}
