# Nestor80 assembler library

[Nestor80](https://github.com/Konamiman/Nestor80) is a Z80, R800 and Z280 assembler almost fully compatible with [Microsoft's Macro80](https://en.wikipedia.org/wiki/Microsoft_MACRO-80) at the source code level. It can produce absolute and relocatable files; relocatable files can be linked using the Linkstor80 package.

Nestor80 [is published](https://github.com/konamiman/Nestor80/releases) as a standalone command line application, `N80` (or `N80.exe`). Additionally, you can use this NuGet package to assemble code programmatically. The process is as follows:

1. Create an instance of `AssemblyConfiguration` and set its properties as appropriate. The properties of this class mimic the command line arguments of the N80 application, you can get a reference by running `N80 --help` or by looking at [the help text in the N80 source code](https://github.com/Konamiman/Nestor80/blob/master/N80/Program.Help.cs).

2. Run `AssemblySourceProcessor.Assemble`, passing an open stream for the source code and the instance of `AssemblyConfiguration` created in the previous step. The output is an instance of `AssemblyResult` containing an abstraction of the assembled code and any errors that were found during the process.

3. If the `AssemblyResult`'s `HasErrors` property is true, the process failed. Show the errors (`Errors` property) and abort the process. Note that some errors can be just warnings (`IsWarning` property on each individual error object), but if all errors are warnings `HasErrors` will be false.

4. Run `OutputGenerator.GenerateAbsolute`, `GenerateRelocatable` or `GenerateSdccRelocatable`, passing the instance of `AssemblyResult` and an open writable stream for the resulting binary file.

5. Optionally, create a listing file by executing `ListingFileGenerator.GenerateListingFile`, passing the instance of `AssemblyResult`, an open writable stream for the listing file, and an instance of `ListingFileConfiguration`.

Below is a simple but fully funcional assembler, it takes a source code file as its only argument and generates an absolute binary file (same filename in same directory but with `.bin` extension) and a listing file (same filename in same directory but with `.lst` extension).

```C#
using Konamiman.Nestor80.Assembler;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace SimpleAssembler;

internal class Program
{
    static void Main(string[] args)
    {
        if(args.Length == 0) {
            Console.WriteLine("*** Usage: SimpleAssembler <source file>");
            return;
        }

        // Step 1: Generate an AssemblyResult instance from the source file

        var sourceFile = args[0];
        var sourceStream = File.OpenRead(sourceFile);
        var assemblyConfig = new AssemblyConfiguration() {
            BuildType = BuildType.Absolute,
            PredefinedSymbols = new (string, ushort)[] { ("DEBUGGING", 0xFFFF) }
        };
        var assemblyResult = AssemblySourceProcessor.Assemble(sourceStream, Encoding.UTF8, assemblyConfig);
        sourceStream.Close();

        if(assemblyResult.HasErrors) {
            Console.WriteLine("*** Errors!!");
            foreach(var error in assemblyResult.Errors) {
                Console.WriteLine($"Line {error.LineNumber} : {error.Message}");
            }
        }
        else {
            // If there are warnings, show them

            var warnings = assemblyResult.Errors.Where(e => e.IsWarning);
            foreach(var warning in warnings) {
                Console.WriteLine($"WARNING: Line {warning.LineNumber} : {warning.Message}");
            }

            // Step 2: Generate the binary file from the instance of AssemblyResult

            var outputFileName = Path.Combine(Path.GetDirectoryName(sourceFile) ?? "", Path.GetFileNameWithoutExtension(sourceFile) + ".bin");
            var outputStream = File.Create(outputFileName);
            OutputGenerator.GenerateAbsolute(assemblyResult, outputStream);
            outputStream.Close();

            // Step 3 (optional): Generate a listing file

            var listingFileName = Path.Combine(Path.GetDirectoryName(sourceFile) ?? "", Path.GetFileNameWithoutExtension(sourceFile) + ".lst");
            var listingStreamWriter = File.CreateText(listingFileName);
            var listingConfig = new ListingFileConfiguration() {
                TitleSignature = "My simple assembler",
                MaxSymbolLength = 255
            };
            ListingFileGenerator.GenerateListingFile(assemblyResult, listingStreamWriter, listingConfig);
            listingStreamWriter.Close();

            Console.WriteLine("Done! Generated file: " + outputFileName);
        }
    }
}
```

You can try it with the following Z80 source file, name it `test.asm`:

```
   org 100h

   ld a,34
   ret
```

Running `SimpleAssembler test.asm` will generate `test.bin` and `test.lst`.

If you want to force errors you can change it to:

```
   org 100h

   ld a,1000h
   ld b,2000h
   ret
```

...and if you want to force warnings:

```
   org 100h

   ld a,34
   if1 ;To prevent warnings from displaying twice (Nestor80 is a two pass assembler)
   .warn This is a warning!
   .warn Another one!
   endif
   ret
```

See also:

* [Nestor80 repository](https://github.com/Konamiman/Nestor80)
* [Language reference](https://github.com/Konamiman/Nestor80/blob/master/docs/LanguageReference.md)
* [Writing relocatable code](https://github.com/Konamiman/Nestor80/blob/master/docs/WritingRelocatableCode.md)
* [Relocatable file format reference](https://github.com/Konamiman/Nestor80/blob/master/docs/RelocatableFileFormat.md)
* [Z280 support](https://github.com/Konamiman/Nestor80/blob/master/docs/Z280Support.md)
* [SDCC relocatable file format support](https://github.com/Konamiman/Nestor80/blob/master/docs/SdccFileFormatSupport.md)