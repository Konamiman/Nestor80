using Konamiman.Nestor80.Assembler.Output;
using System.Text;

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

        static BitStreamWriter bitWriter;

        public static int GenerateRelocatable(AssemblyResult assemblyResult, Stream outputStream, bool initDefs)
        {
            var output = new List<byte>();
            bitWriter = new BitStreamWriter(output);
            ushort endAddress = 0;
            AddressType currentLocationArea = AddressType.CSEG;
            var locationCounters = new Dictionary<AddressType, ushort>() {
                { AddressType.CSEG, 0 },
                { AddressType.DSEG, 0 },
                { AddressType.ASEG, 0 },
            };

            var publicSymbols = assemblyResult.Symbols
                .Where(s => s.IsPublic)
                .Select(s => new {
                    Name = s.Name.Length > AssemblySourceProcessor.MaxEffectiveExternalNameLength ?
                        s.Name[..AssemblySourceProcessor.MaxEffectiveExternalNameLength].ToUpper() :
                        s.Name.ToUpper(),
                    s.ValueArea,
                    s.Value})
                .ToArray();

            WriteLinkItem(LinkItemType.ProgramName, assemblyResult.ProgramName);
            foreach(var symbol in publicSymbols) {
                WriteLinkItem(LinkItemType.EntrySymbol, symbol.Name);
            }
            WriteLinkItem(LinkItemType.DataAreaSize, AddressType.ASEG, (ushort)assemblyResult.DataAreaSize);
            WriteLinkItem(LinkItemType.ProgramAreaSize, AddressType.CSEG, (ushort)assemblyResult.ProgramAreaSize);

            foreach(var line in assemblyResult.ProcessedLines) {
                if(line is ChangeAreaLine cal) {
                    WriteLinkItem(LinkItemType.SetLocationCounter, cal.NewLocationArea, cal.NewLocationCounter);
                    currentLocationArea = cal.NewLocationArea;
                }
                else if(line is ChangeOriginLine col) {
                    locationCounters[currentLocationArea] = col.NewLocationCounter;
                    WriteLinkItem(LinkItemType.SetLocationCounter, currentLocationArea, col.NewLocationCounter);
                }
                else if(line is DefineSpaceLine dsl) {
                    locationCounters[currentLocationArea] += dsl.Size;
                    if(dsl.Value.HasValue || initDefs) {
                        var byteToWrite = dsl.Value.GetValueOrDefault(0);
                        for(int i=0; i<dsl.Size; i++) {
                            WriteByte(byteToWrite);
                        }
                    }
                    else {
                        WriteLinkItem(LinkItemType.SetLocationCounter, currentLocationArea, locationCounters[currentLocationArea]);
                    }
                }
                else if(line is IProducesOutput ipo) {
                    if(ipo.RelocatableParts.Length == 0) {
                        WriteBytes(ipo.OutputBytes);
                    } 
                    else {
                        WriteInstructionWithRelocatables(ipo);
                    }
                    locationCounters[currentLocationArea] += (ushort)ipo.OutputBytes.Length;
                }
                else if(line is LinkerFileReadRequestLine lfr) {
                    foreach(var filename in lfr.Filenames) {
                        WriteLinkItem(LinkItemType.RequestLibrarySearch, filename.ToUpper());
                    }
                }
                else if(line is EndOutputLine) {
                    break;
                }
                else if(line is AssemblyEndLine ael) {
                    endAddress = ael.EndAddress;
                    break;
                }
            }

            foreach(var symbol in publicSymbols) {
                WriteLinkItem(LinkItemType.DefineEntryPoint, symbol.ValueArea, symbol.Value, symbol.Name);
            }

            WriteLinkItem(LinkItemType.EndProgram, AddressType.ASEG, endAddress);

            bitWriter.ForceByteBoundary();
            WriteLinkItem(LinkItemType.EndFile);

            outputStream.Write(output.ToArray());
            return output.Count;
        }

        private static void WriteInstructionWithRelocatables(IProducesOutput instruction)
        {
            var outputBytes = instruction.OutputBytes;
            var currentRelocatableItemIndex = 0;
            var currentRelocatablePart = instruction.RelocatableParts[0];
            var currentRelocatableByteIndex = currentRelocatablePart.Index;
            var outputByteIndex = 0;

            while(outputByteIndex < outputBytes.Length) {
                if(outputByteIndex < currentRelocatableByteIndex) {
                    WriteByte(outputBytes[outputByteIndex]);
                    outputByteIndex++;
                    continue;
                }

                if(currentRelocatablePart is RelocatableAddress rad) {
                    if(rad.IsByte) {
                        WriteExtensionLinkItem(SpecialLinkItemType.Address, (byte)rad.Type, (byte)(rad.Value & 0xFF), (byte)((rad.Value >> 8) & 0xFF));
                        WriteExtensionLinkItem(SpecialLinkItemType.ArithmeticOperator, (byte)ArithmeticOperatorCode.StoreAsByte);
                        WriteByte(0);
                    }
                    else {
                        WriteAddress(rad.Type, rad.Value);
                    }
                }
                else {
                    //WIP
                    throw new NotImplementedException("Soon...");
                }
                outputByteIndex += currentRelocatablePart.IsByte ? 1 : 2;

                currentRelocatableItemIndex++;
                if(currentRelocatableItemIndex < instruction.RelocatableParts.Length) {
                    currentRelocatablePart = instruction.RelocatableParts[currentRelocatableItemIndex];
                    currentRelocatableByteIndex = currentRelocatablePart.Index;
                }
                else {
                    for(int i=outputByteIndex; i<outputBytes.Length; i++) {
                        WriteByte(outputBytes[i]);
                    }
                    break;
                }
            }
        }

        private static void WriteByte(byte b)
        {
            bitWriter.Write(0, 1);
            bitWriter.Write(b, 8);
        }

        private static void WriteAddress(AddressType type, ushort value)
        {
            bitWriter.Write(1, 1);
            bitWriter.Write((byte)type, 2);
            bitWriter.Write((byte)(value & 0xFF), 8);
            bitWriter.Write((byte)((value >> 8) & 0xFF), 8);
        }

        private static void WriteBytes(byte[] bytes)
        {
            foreach(var b in bytes) {
                bitWriter.Write(0, 1);
                bitWriter.Write(b, 8);
            }
        }

        private static void WriteExtensionLinkItem(SpecialLinkItemType type, params byte[] symbolBytes)
        {
            bitWriter.Write(0b100, 3);
            bitWriter.Write((byte)LinkItemType.ExtensionLinkItem, 4);

            bitWriter.Write((byte)(symbolBytes.Length+1), 3);
            bitWriter.Write((byte)type, 8);
            foreach(var b in symbolBytes) {
                bitWriter.Write(b, 8);
            }
        }

        private static void WriteLinkItem(LinkItemType type, string symbol)
        {
            WriteLinkItem(type, symbolBytes: Encoding.ASCII.GetBytes(symbol));
        }

        private static void WriteLinkItem(LinkItemType type, Address address = null, byte[] symbolBytes = null)
        {
            WriteLinkItem(type, address?.Type, address?.Value ?? 0, symbolBytes);
        }

        private static void WriteLinkItem(LinkItemType type, AddressType? addressType, ushort addressValue, string symbolBytes)
        {
            WriteLinkItem(type, addressType, addressValue, Encoding.ASCII.GetBytes(symbolBytes));
        }

        private static void WriteLinkItem(LinkItemType type, AddressType? addressType, ushort addressValue, byte[] symbolBytes = null)
        {
            bitWriter.Write(0b100, 3);
            bitWriter.Write((byte)type, 4);
            if(addressType is not null) {
                bitWriter.Write((byte)addressType, 2);
                bitWriter.Write((byte)(addressValue & 0xFF), 8);
                bitWriter.Write((byte)((addressValue >> 8) & 0xFF), 8);
            }
            if(symbolBytes is not null) {
                bitWriter.Write((byte)symbolBytes.Length, 3);
                foreach(var b in symbolBytes) {
                    bitWriter.Write(b, 8);
                }
            }
        }
    }
}
