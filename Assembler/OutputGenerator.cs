using Konamiman.Nestor80.Assembler.Output;
using Konamiman.Nestor80.Assembler.Relocatable;
using System.Text;

namespace Konamiman.Nestor80.Assembler
{
    /// <summary>
    /// This class contains methods to convert the result of processing a source unit
    /// (an <see cref="AssemblyResult"/>) into an absolute or relocatable binary file.
    /// </summary>
    public static class OutputGenerator
    {
        /// <summary>
        /// Generate an absolute binary file from an <see cref="AssemblyResult"/>.
        /// 
        /// How to output is composed depends on the directFileWrite parameter:
        /// 
        /// - True: Just write the output sequentially to the output file, which doesn't have a maximum length.
        /// - False: Create a 64K memory map and fill it with the output bytes according to the ORG statements found;
        ///          then dump the filled memory between the minimum and the maximum memory addresses used,
        ///          with a maximum of 64 KBytes.
        /// </summary>
        /// <param name="assemblyResult">Assembly result ro use for the file generation.</param>
        /// <param name="outputStream">The stream to write the result to.</param>
        /// <param name="directFileWrite">If true, treat ORG statements as equivalent to PHASE statements and don't limit the output size to 64KBytes.</param>
        /// <returns>How many bytes have been written to the stream.</returns>
        /// <exception cref="ArgumentException"></exception>
        public static int GenerateAbsolute(AssemblyResult assemblyResult, Stream outputStream, bool directFileWrite = false)
        {
            var memory = new byte[65536];
            var firstAddress = 0;
            var lastAddressPlusOne = 0;
            var currentAddress = 0;

            if(assemblyResult.BuildType != BuildType.Absolute) {
                throw new ArgumentException("Absolute output can be genereated only for assembly results with a built type of 'Absolute'");
            }

            var lines = FlattenLinesList(assemblyResult.ProcessedLines);
            if(!directFileWrite) {
                var addressDecidingLine = lines.FirstOrDefault(l => l is ChangeOriginLine or IProducesOutput);
                if(addressDecidingLine is ChangeOriginLine first_chol) {
                    firstAddress = first_chol.NewLocationCounter;
                    currentAddress = firstAddress;
                }
            }

            //We do a deferred location counter update to prevent an ORG at the end of the file
            //(not followed by more output) from affecting the final output size
            int newLocationCounter = -1;

            foreach(var line in lines) {
                if(!directFileWrite && newLocationCounter != -1 && line is IProducesOutput or DefineSpaceLine) {
                    firstAddress = Math.Min(firstAddress, newLocationCounter);
                    lastAddressPlusOne = Math.Max(lastAddressPlusOne, newLocationCounter);
                    currentAddress = newLocationCounter;
                    newLocationCounter = -1;
                }

                if(line is ChangeOriginLine chol) {
                    if(!directFileWrite) {
                        newLocationCounter = chol.NewLocationCounter;
                    }
                }
                else if(line is IProducesOutput ipo) {
                    var length = ipo.OutputBytes.Length;
                    if(directFileWrite) {
                        outputStream.Write(ipo.OutputBytes);
                        lastAddressPlusOne += length;
                    }
                    else if(currentAddress + length <= 65536) {
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
                    if(directFileWrite) {
                        var bytes = Enumerable.Repeat(outputByte, length).ToArray();
                        outputStream.Write(bytes);
                        lastAddressPlusOne += length;
                    }
                    else if(currentAddress + length <= 65536) {
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
            if(!directFileWrite && outputSize > 0) {
                outputStream.Write(memory, firstAddress, outputSize);
            }

            return outputSize;
        }

        private static ProcessedSourceLine[] FlattenLinesList(ProcessedSourceLine[] lines)
        {
            if(!lines.Any(l => l is LinesContainerLine)) {
                return lines;
            }

            var result = new List<ProcessedSourceLine>();

            foreach(var line in lines) {
                if(line is LinesContainerLine lcl) {
                    result.Add(line);
                    result.AddRange(FlattenLinesList(lcl.Lines));
                }
                else {
                    result.Add(line);
                }
            }

            return result.ToArray();
        }

        static readonly byte[] extendedFileFormatHeader = { 0x85, 0xD3, 0x13, 0x92, 0xD4, 0xD5, 0x13, 0xD4, 0xA5, 0x00, 0x00, 0x13, 0x8F, 0xFF, 0xF0, 0x9E };

        static BitStreamWriter bitWriter;
        static Dictionary<string, Address> externalChains;
        static AddressType currentLocationArea;
        static Dictionary<AddressType, ushort> locationCounters;
        static List<string> referencedExternals;
        static bool extendedFormat;
        static Encoding encoding;
        static string currentCommonBlockName;

        /// <summary>
        /// Generate a LINK-80 compatible relative file from an <see cref="AssemblyResult"/>.
        /// </summary>
        /// <param name="assemblyResult">Assembly result ro use for the file generation.</param>
        /// <param name="outputStream">The stream to write the result to.</param>
        /// <param name="initDefs">If true, intialize DEFS blocks with no value with zeros; if false, treat these blocks as equivalent to ORG $+size.</param>
        /// <returns></returns>
        public static int GenerateRelocatable(AssemblyResult assemblyResult, Stream outputStream, bool initDefs, bool extendedFormat)
        {
            var output = new List<byte>();
            OutputGenerator.extendedFormat = extendedFormat;
            encoding = extendedFormat ? Encoding.UTF8 : Encoding.ASCII;
            bitWriter = new BitStreamWriter(output);
            externalChains = new Dictionary<string, Address>(StringComparer.OrdinalIgnoreCase);
            referencedExternals = new List<string>();
            ushort endAddress = 0;
            bool changedToAseg = false;
            currentCommonBlockName = null;
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
                    Name = EffectiveNameOf(s.Name),
                    s.ValueArea,
                    s.Value,
                    s.CommonName})
                .ToArray();

            if(extendedFormat) {
                outputStream.Write(extendedFileFormatHeader);
            }

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

            var lines = FlattenLinesList(assemblyResult.ProcessedLines);
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
                    //For compatibility with MACRO-80 (and apparently taken in account by LINK-80):
                    //When ASEG is followed by ORG, generate only one "set location counter" item;
                    //e.g. "ASEG - org 100h" generates just "set location to 100h" instead of
                    //"set location to 0 - set location to 100h" as is the case of CSEG and DSEG.
                    //Failure to do so can lead to LINK-80 failing with "Intersecting Data area"!
                    changedToAseg = cal.NewLocationArea is AddressType.ASEG;
                    if(!changedToAseg) {
                        if(cal.NewLocationArea is AddressType.COMMON) {
                            //Setting currentCommonBlockName to the full block name (as specified in code)
                            //instead of the effective name is on purpose for compatibility with MACRO-80,
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
                        WriteLinkItem(LinkItemType.RequestLibrarySearch, extendedFormat ? filename : filename.ToUpper());
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
                if(symbol.ValueArea is AddressType.COMMON && symbol.CommonName != currentCommonBlockName) {
                    WriteLinkItem(LinkItemType.SelectCommonBlock, symbol.CommonName);
                    currentCommonBlockName = symbol.CommonName;
                }
                WriteLinkItem(LinkItemType.DefineEntryPoint, symbol.ValueArea, symbol.Value, symbol.Name);
            }

            foreach(var external in externalChains) {
                WriteLinkItem(LinkItemType.ChainExternal, external.Value.Type, external.Value.Value, extendedFormat ? external.Key : external.Key.ToUpper());
            }

            var externalsWithoutChain = referencedExternals.Except(externalChains.Keys, StringComparer.OrdinalIgnoreCase);
            foreach(var item in externalsWithoutChain) {
                WriteLinkItem(LinkItemType.ChainExternal, Address.AbsoluteZero, encoding.GetBytes(extendedFormat ? item : item.ToUpper()));
            }

            WriteLinkItem(LinkItemType.EndProgram, AddressType.ASEG, endAddress);

            bitWriter.ForceByteBoundary();
            WriteLinkItem(LinkItemType.EndFile);

            outputStream.Write(output.ToArray());
            return output.Count;
        }

        private static string EffectiveNameOf(string name)
        {
            if(extendedFormat) return name;

            return (name.Length > AssemblySourceProcessor.MaxEffectiveExternalNameLength ?
                   name[..AssemblySourceProcessor.MaxEffectiveExternalNameLength] :
                   name).ToUpper();
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
                if(currentRelocatablePart is RelocatableValue rad) {
                    if(rad.IsByte) {
                        WriteExtensionLinkItem(ExtensionLinkItemType.Address, (byte)rad.Type, (byte)(rad.Value & 0xFF), (byte)((rad.Value >> 8) & 0xFF));
                        WriteExtensionLinkItem(ExtensionLinkItemType.ArithmeticOperator, (byte)ArithmeticOperatorCode.StoreAsByte);
                        WriteByte(0);
                        locationCounters[currentLocationArea]++;
                    }
                    else {
                        if(rad.Type is AddressType.COMMON && rad.CommonName != currentCommonBlockName) {
                            WriteLinkItem(LinkItemType.SelectCommonBlock, rad.CommonName);
                            currentCommonBlockName = rad.CommonName;
                        }
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
                    var symbolName = extendedFormat ? item.GetSymbolName() : item.GetSymbolName().ToUpper();
                    WriteExtensionLinkItem(ExtensionLinkItemType.ReferenceExternal, encoding.GetBytes(symbolName));
                    referencedExternals.Add(item.GetSymbolName());
                }
                else if(item.IsAddressReference) {
                    WriteExtensionLinkItem(ExtensionLinkItemType.Address, item.SymbolBytes[1], item.SymbolBytes[2], item.SymbolBytes[3]);
                }
                else {
                    var op = item.ArithmeticOperator ?? throw new Exception($"Unexpected type of link item found in group: {item}");
                    WriteExtensionLinkItem(ExtensionLinkItemType.ArithmeticOperator, (byte)op);
                }
            }

            WriteExtensionLinkItem(ExtensionLinkItemType.ArithmeticOperator, group.IsByte ? (byte)ArithmeticOperatorCode.StoreAsByte : (byte)ArithmeticOperatorCode.StoreAsWord);
            WriteByte(0);
            if(!group.IsByte) {
                WriteByte(0);
            }

            locationCounters[currentLocationArea] += (ushort)(group.IsByte ? 1 : 2);
        }

        /// <summary>
        /// Given a group of link items that represents a postfix expression
        /// and contains exactly one external symbol reference, try to optimize it
        /// to an "external + offset" item. This is done by trying to evaluate
        /// the expression considering the external symbol reference as a 0; 
        /// if the evaluation succeeds and produces a number, the optimization is possible.
        /// </summary>
        /// <param name="group"></param>
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

        private static void WriteExtensionLinkItem(ExtensionLinkItemType type, params byte[] symbolBytes)
        {
            WriteLinkItem(LinkItemType.ExtensionLinkItem, symbolBytes: new byte[] { (byte)type }.Concat(symbolBytes).ToArray());
        }

        private static void WriteLinkItem(LinkItemType type, string symbol)
        {
            WriteLinkItem(type, symbolBytes: encoding.GetBytes(symbol));
        }

        private static void WriteLinkItem(LinkItemType type, Address address = null, byte[] symbolBytes = null)
        {
            WriteLinkItem(type, address?.Type, address?.Value ?? 0, symbolBytes);
        }

        private static void WriteLinkItem(LinkItemType type, AddressType? addressType, ushort addressValue, string symbolBytes)
        {
            WriteLinkItem(type, addressType, addressValue, encoding.GetBytes(symbolBytes));
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

            if(symbolBytes is null) {
                return;
            }

            if(!extendedFormat) {
                if(symbolBytes.Length > 7) {
                    throw new InvalidOperationException("Symbol fields over 7 bytes are only allowed in the extended relocatable format");
                }
                WriteSymbolField(symbolBytes);
                return;
            }

            if(symbolBytes.LongLength >= 0x1000000) {
                throw new InvalidOperationException("The maximum supported size of symbol fields is " + uint.MaxValue.ToString());
            }

            if(symbolBytes.Length > 7 || (symbolBytes.Length > 1 && symbolBytes[0] == 0xFF)) {
                var legacyContents = symbolBytes.Length switch {
                    < 0x100 => new byte[] { 0xFF, (byte)symbolBytes.Length },
                    < 0x10000 => new byte[] { 0xFF, (byte)(symbolBytes.Length & 0xFF), (byte)(symbolBytes.Length >> 8) },
                    < 0x1000000 => new byte[] { 0xFF, (byte)(symbolBytes.Length & 0xFF), (byte)(symbolBytes.Length >> 8), (byte)(symbolBytes.Length >> 16) },
                    _ => new byte[] { 0xFF, (byte)(symbolBytes.LongLength & 0xFF), (byte)(symbolBytes.LongLength >> 8), (byte)(symbolBytes.LongLength >> 24) },
                };
                WriteSymbolField(legacyContents);
                if(symbolBytes.Length > 255) {
                    bitWriter.WriteDirect(symbolBytes);
                }
                else {
                    foreach(var b in symbolBytes) {
                        bitWriter.Write(b, 8);
                    }
                }
            }
            else {
                WriteSymbolField(symbolBytes);
            }
        }

        private static void WriteSymbolField(byte[] symbolBytes)
        {
            bitWriter.Write((byte)symbolBytes.Length, 3);
            foreach(var b in symbolBytes) {
                bitWriter.Write(b, 8);
            }
        }
    }
}
