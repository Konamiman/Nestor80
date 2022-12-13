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
            if(!lines.Any(l => l is LinesContainerLine)) {
                return lines;
            }

            var result = new List<ProcessedSourceLine>();

            foreach(var line in lines) {
                if(line is LinesContainerLine lcl) {
                    result.Add(line);
                    result.AddRange(FlatLinesList(lcl.Lines));
                    result.Add(new ContainedLinesEnd());
                }
                else {
                    result.Add(line);
                }
            }

            return result.ToArray();
        }

        static BitStreamWriter bitWriter;
        static Dictionary<string, Address> externalChains;
        static AddressType currentLocationArea;
        static Dictionary<AddressType, ushort> locationCounters;
        static List<string> referencedExternals;

        public static int GenerateRelocatable(AssemblyResult assemblyResult, Stream outputStream, bool initDefs)
        {
            var output = new List<byte>();
            bitWriter = new BitStreamWriter(output);
            externalChains = new Dictionary<string, Address>(StringComparer.OrdinalIgnoreCase);
            referencedExternals = new List<string>();
            ushort endAddress = 0;
            bool changedToAseg = false;
            string currentCommonBlockName = null;
            currentLocationArea = AddressType.CSEG;
            locationCounters = new Dictionary<AddressType, ushort>() {
                { AddressType.CSEG, 0 },
                { AddressType.DSEG, 0 },
                { AddressType.ASEG, 0 },
                { AddressType.COMMON, 0 }
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

            foreach(var commonBlockSize in assemblyResult.CommonAreaSizes) {
                var name = assemblyResult.EffectiveRelocatableSymbolLength(commonBlockSize.Key);
                WriteLinkItem(LinkItemType.DefineCommonSize, AddressType.ASEG, (ushort)commonBlockSize.Value, name);
            }

            WriteLinkItem(LinkItemType.DataAreaSize, AddressType.ASEG, (ushort)assemblyResult.DataAreaSize);
            if(assemblyResult.ProgramAreaSize > 0) {
                WriteLinkItem(LinkItemType.ProgramAreaSize, AddressType.CSEG, (ushort)assemblyResult.ProgramAreaSize);
            }

            var lines = FlatLinesList(assemblyResult.ProcessedLines);
            foreach(var line in lines) {
                if(line is DefineSpaceLine dsl) {
                    if(changedToAseg) {
                        WriteLinkItem(LinkItemType.SetLocationCounter, AddressType.ASEG, locationCounters[AddressType.ASEG]);
                        changedToAseg = false;
                    }

                    locationCounters[currentLocationArea] += dsl.Size;
                    if(dsl.Value.HasValue || initDefs) {
                        var byteToWrite = dsl.Value.GetValueOrDefault(0);
                        for(int i = 0; i < dsl.Size; i++) {
                            WriteByte(byteToWrite);
                        }
                    }
                    else {
                        WriteLinkItem(LinkItemType.SetLocationCounter, currentLocationArea, locationCounters[currentLocationArea]);
                    }
                }
                else if(line is ChangeAreaLine cal) {
                    //For compatibility with Macro80 (and apparently taken in account by Link80):
                    //When ASEG is followed by ORG, generate only one "set location counter" item;
                    //e.g. "ASEG - org 100h" generates just "set location to 100h" instead of
                    //"set location to 0 - set location to 100h" as is the case of CSEG and DSEG.
                    //Failure to do so can lead to Link80 failing with "Intersecting Data area"!
                    changedToAseg = cal.NewLocationArea is AddressType.ASEG;
                    if(!changedToAseg) {
                        if(cal.NewLocationArea is AddressType.COMMON) {
                            //Setting currentCommonBlockName to the full block name (as specified in code)
                            //instead of the effective name is on purpose for compatibility with Macro80,
                            //so consecutive blocks "ABCDEFXXX" and "ABCDEFZZZ" will generate two
                            //"select common block ABCDEF" link items even though only one would be needed.
                            if(cal.CommonBlockName != currentCommonBlockName) { 
                                var name = assemblyResult.EffectiveRelocatableSymbolLength(cal.CommonBlockName);
                                WriteLinkItem(LinkItemType.SelectCommonBlock, name);
                            }
                            currentCommonBlockName = cal.CommonBlockName;
                            locationCounters[AddressType.COMMON] = 0;
                        }
                        WriteLinkItem(LinkItemType.SetLocationCounter, cal.NewLocationArea, cal.NewLocationCounter);
                    }
                    currentLocationArea = cal.NewLocationArea;
                }
                else if(line is ChangeOriginLine col) {
                    changedToAseg = false;
                    locationCounters[currentLocationArea] = col.NewLocationCounter;
                    WriteLinkItem(LinkItemType.SetLocationCounter, currentLocationArea, col.NewLocationCounter);
                }
                else if(line is IProducesOutput ipo) {
                    if(changedToAseg) {
                        WriteLinkItem(LinkItemType.SetLocationCounter, AddressType.ASEG, locationCounters[AddressType.ASEG]);
                        changedToAseg = false;
                    }

                    if(ipo.RelocatableParts.Length == 0) {
                        WriteBytes(ipo.OutputBytes);
                        locationCounters[currentLocationArea] += (ushort)ipo.OutputBytes.Length;
                    } 
                    else {
                        WriteInstructionWithRelocatables(ipo);
                    }
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

            foreach(var external in externalChains) {
                WriteLinkItem(LinkItemType.ChainExternal, external.Value.Type, external.Value.Value, external.Key.ToUpper());
            }

            var externalsWithoutChain = referencedExternals.Except(externalChains.Keys, StringComparer.OrdinalIgnoreCase);
            foreach(var item in externalsWithoutChain) {
                WriteLinkItem(LinkItemType.ChainExternal, Address.AbsoluteZero, Encoding.ASCII.GetBytes(item.ToUpper()));
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
                    locationCounters[currentLocationArea]++;
                    continue;
                }
                if(currentRelocatablePart is RelocatableAddress rad) {
                    if(rad.IsByte) {
                        WriteExtensionLinkItem(SpecialLinkItemType.Address, (byte)rad.Type, (byte)(rad.Value & 0xFF), (byte)((rad.Value >> 8) & 0xFF));
                        WriteExtensionLinkItem(SpecialLinkItemType.ArithmeticOperator, (byte)ArithmeticOperatorCode.StoreAsByte);
                        WriteByte(0);
                        locationCounters[currentLocationArea]++;
                    }
                    else {
                        WriteAddress(rad.Type, rad.Value);
                        locationCounters[currentLocationArea] += 2;
                    }
                }
                else {
                    WriteLinkItemsGroup((LinkItemsGroup)currentRelocatablePart);
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
                        locationCounters[currentLocationArea]++;
                    }
                    break;
                }
            }
        }

        private static void WriteLinkItemsGroup(LinkItemsGroup group)
        {
            MaybeConvertToExtPlusOffset(group);

            if(!group.IsByte && group.LinkItems.Length == 1) {
                var item = group.LinkItems[0];
                if(!item.IsExternalReference) {
                    throw new Exception($"Single item in link items group is not an external reference, it's: {item}");
                }
                WriteExternalChainItem(item.GetSymbolName());
                locationCounters[currentLocationArea] += 2;
                return;
            }

            //EXT+n, EXT-n, n+EXT, with absolute n that store two bytes, generate "External + offset"
            if(!group.IsByte && group.LinkItems.Length == 3 &&
                ((group.LinkItems[0].IsExternalReference && group.LinkItems[1].IsAddressReference && group.LinkItems[2].IsPlusOrMinus) ||
                (group.LinkItems[1].IsExternalReference && group.LinkItems[0].IsAddressReference && group.LinkItems[2].ArithmeticOperator is ArithmeticOperatorCode.Plus))
                ) {
                var (addressType, value) = group.LinkItems[0].IsAddressReference ? group.LinkItems[0].GetReferencedAddress() : group.LinkItems[1].GetReferencedAddress();
                if(addressType == AddressType.ASEG) {
                    var symbolName = group.LinkItems[0].IsExternalReference ? group.LinkItems[0].GetSymbolName() : group.LinkItems[1].GetSymbolName();
                    if(group.LinkItems[2].ArithmeticOperator is ArithmeticOperatorCode.Minus) {
                        value = (ushort)-value;
                    }

                    WriteLinkItem(LinkItemType.ExternalPlusOffset, AddressType.ASEG, value);
                    WriteExternalChainItem(symbolName);
                    locationCounters[currentLocationArea] += 2;
                    return;
                }
            }

            foreach(var item in group.LinkItems) {
                if(item.IsExternalReference) {
                    WriteExtensionLinkItem(SpecialLinkItemType.ReferenceExternal, Encoding.ASCII.GetBytes(item.GetSymbolName().ToUpper()));
                    referencedExternals.Add(item.GetSymbolName());
                }
                else if(item.IsAddressReference) {
                    WriteExtensionLinkItem(SpecialLinkItemType.Address, item.SymbolBytes[1], item.SymbolBytes[2], item.SymbolBytes[3]);
                }
                else {
                    var op = item.ArithmeticOperator;
                    if(op is null) {
                        throw new Exception($"Unexpected type of link item found in group: {item}");
                    }
                    WriteExtensionLinkItem(SpecialLinkItemType.ArithmeticOperator, (byte)op);
                }
            }

            WriteExtensionLinkItem(SpecialLinkItemType.ArithmeticOperator, group.IsByte ? (byte)ArithmeticOperatorCode.StoreAsByte : (byte)ArithmeticOperatorCode.StoreAsWord);
            WriteByte(0);
            if(!group.IsByte) {
                WriteByte(0);
            }

            locationCounters[currentLocationArea] += (ushort)(group.IsByte ? 1 : 2);
        }

        private static void MaybeConvertToExtPlusOffset(LinkItemsGroup group)
        {
            var items = group.LinkItems;

            if(group.IsByte || items.Count(i => i.IsExternalReference) != 1) {
                return;
            }

            var value = MaybeEvaluate(items);
            if(value is not null) {
                if(value is 0) {
                    group.LinkItems = new[] {
                        LinkItem.ForExternalReference(items.Single(i => i.IsExternalReference).GetSymbolName())
                    };
                }
                else {
                    group.LinkItems = new[] {
                        LinkItem.ForExternalReference(items.Single(i => i.IsExternalReference).GetSymbolName()),
                        LinkItem.ForAddressReference(AddressType.ASEG, value.Value),
                        LinkItem.ForArithmeticOperator(ArithmeticOperatorCode.Plus)
                    };
                };
            }
        }

        static ushort? MaybeEvaluate(LinkItem[] items)
        {
            var stack = new Stack<ushort>();

            bool lastWasExternal = false;

            foreach(var item in items) {
                if(item.IsExternalReference) {
                    stack.Push(0);
                    lastWasExternal = true;
                    continue;
                }

                if(item.IsAddressReference) {
                    if(item.ReferencedAddressType != AddressType.ASEG) {
                        return null;
                    }

                    var value = item.ReferencedAddressValue;
                    stack.Push(value);
                    lastWasExternal = false;
                    continue;
                }

                var op = item.ArithmeticOperator;

                if(op is ArithmeticOperatorCode.UnaryMinus) {
                    if(lastWasExternal) {
                        return null;
                    }
                    var poppedItem = stack.Pop();
                    var operationResult = (ushort)-poppedItem;
                    stack.Push(operationResult);
                }
                else if(op is ArithmeticOperatorCode.Plus) {
                    var poppedItem2 = stack.Pop();
                    var poppedItem1 = stack.Pop();
                    var operationResult = (ushort)(poppedItem1 + poppedItem2);
                    stack.Push(operationResult);
                }
                else if(op is ArithmeticOperatorCode.Minus) {
                    if(lastWasExternal) {
                        return null;
                    }
                    var poppedItem2 = stack.Pop();
                    var poppedItem1 = stack.Pop();
                    var operationResult = (ushort)(poppedItem1 - poppedItem2);
                    stack.Push(operationResult);
                }
                else {
                    return null;
                }

                lastWasExternal = false;
            }

            return stack.Count == 1 ? stack.Pop() : null;
        }

        private static void WriteExternalChainItem(string symbolName)
        {
            if(externalChains.ContainsKey(symbolName)) {
                WriteAddress(externalChains[symbolName]);
                externalChains[symbolName] = new Address(currentLocationArea, (ushort)(locationCounters[currentLocationArea]));
            }
            else {
                externalChains.Add(symbolName, new Address(currentLocationArea, (ushort)(locationCounters[currentLocationArea])));
                WriteByte(0);
                WriteByte(0);
            }
        }

        private static void WriteByte(byte b)
        {
            bitWriter.Write(0, 1);
            bitWriter.Write(b, 8);
        }

        private static void WriteAddress(Address address)
        {
            WriteAddress(address.Type, address.Value);
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
