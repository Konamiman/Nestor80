# Nestor80 linker library

[Nestor80](https://github.com/Konamiman/Nestor80) is a Z80, R800 and Z280 assembler almost fully compatible with [Microsoft's Macro80](https://en.wikipedia.org/wiki/Microsoft_MACRO-80) at the source code level. It can produce absolute and relocatable files; relocatable files can be linked using the Linkstor80 package. Linkstor80 is the linker used to combine together multiple relocatable files into an absolute binary file.

Linkstor80 [is published](https://github.com/konamiman/Nestor80/releases) as a standalone command line application, `LK80` (or `LK80.exe`). Additionally, you can use this NuGet package to link relocatable programmatically. The process is as follows:

1. Use Nestor80 (the `N80` or `N80.exe` command line application, or the Nestor80 NuGet package) to generate one or more relocatable files.

2. Create a an array of instances of `ILinkingSequenceItem`. The classes tha implement this interface are:

   * `RelocatableFileReference` - refers to a relocatable file to process.
   * `SetCodeBeforeDataMode` - instructs the linker to place the code segment before the data segment.
   * `SetDataBeforeCodeMode` - instructs the linker to place the data segment before the code segment.
   * `SetCodeSegmentAddress` - instructs the linker to use the provided the supplied address as the next code segment address.
   * `SetDataSegmentAddress` - instructs the linker to use the provided the supplied address as the next data segment address.

These classes mimic the command line arguments of the LK80 application, you can get a reference by running `LK80 --help` or by looking at [the help text in the LK80 source code](https://github.com/Konamiman/Nestor80/blob/master/LK80/Program.Help.cs).

3. Create an instance of `LinkingConfiguration` and set its properties as appropriate. At the very least you should set `LinkingSequenceItems` (with the array created in the previous step) and `OpenFile` (a callback to open the involved relocatable files).

4. Run `RelocatableFilesProcessor.Link`, passing the instance of `LinkingConfiguration` and an open writable stream for the binary file to generate. The items in `LinkingSequenceItems` will be processed in the order in which they are found, and the output will be an instance of [`LinkingResult`](https://github.com/Konamiman/Nestor80/blob/master/Linker/LinkingResult.cs), where you can check if there was any error, and obtain information about the generated programs (as instances of [`ProgramData`](https://github.com/Konamiman/Nestor80/blob/master/Linker/ProgramData.cs)), for example the public symbols with their absolute addresses.

You can take a look at [the source of the LK80 tool](https://github.com/Konamiman/Nestor80/blob/master/LK80/Program.cs) for a real life example. See also:

* [Nestor80 repository](https://github.com/Konamiman/Nestor80)
* [Language reference](https://github.com/Konamiman/Nestor80/blob/master/docs/LanguageReference.md)
* [Writing relocatable code](https://github.com/Konamiman/Nestor80/blob/master/docs/WritingRelocatableCode.md)
* [Relocatable file format reference](https://github.com/Konamiman/Nestor80/blob/master/docs/RelocatableFileFormat.md)
* [Z280 support](https://github.com/Konamiman/Nestor80/blob/master/docs/Z280Support.md)
* [SDCC relocatable file format support](https://github.com/Konamiman/Nestor80/blob/master/docs/SdccFileFormatSupport.md)
