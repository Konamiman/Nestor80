using Konamiman.Nestor80.Assembler;
using Konamiman.Nestor80.Assembler.Output;
using System.Diagnostics;
using System.Text;
using static System.Console;

namespace Konamiman.Nestor80.N80
{
    internal partial class Program
    {
        const int ERR_SUCCESS = 0;
        const int ERR_BAD_ARGUMENTS = 1;
        const int ERR_CANT_OPEN_INPUT_FILE = 2;
        const int ERR_CANT_CREATE_OUTPUT_FILE = 3;
        const int ERR_ASSEMBLY_ERROR = 4;
        const int ERR_ASSEMBLY_FATAL = 5;

        const int DEFAULT_MAX_ERRORS = 34;

        static string inputFilePath = null;
        static string inputFileDirectory = null;
        static string inputFileName = null;
        static string outputFilePath = null;
        static bool generateOutputFile = true;
        static Encoding inputFileEncoding;
        static bool mustChangeOutputFileExtension;
        static bool colorPrint;
        static bool showBanner;
        static readonly List<string> includeDirectories = new();
        static bool orgAsPhase;
        static readonly List<(string, ushort)> symbolDefinitions = new();
        static int maxErrors;
        static AssemblyErrorCode[] skippedWarnings;
        static bool silenceStatus;
        static bool silenceAssemblyPrints;

        static bool printInstructionExecuted = false;
        static readonly ConsoleColor defaultForegroundColor = Console.ForegroundColor;
        static readonly ConsoleColor defaultBackgroundColor = Console.BackgroundColor;
        static readonly Stopwatch assemblyTimeMeasurer = new();
        static readonly Stopwatch totalTimeMeasurer = new();

        static int Main(string[] args)
        {
            totalTimeMeasurer.Start();

            if(args.Length == 0) {
                WriteLine(bannerText);
                WriteLine(simpleHelpText);
                return ERR_SUCCESS;
            }

            var cmdLineArgs = args;
            args = MaybeMergeArgsWithEnv(args);
            SetShowBannerFlag(args);

            if(cmdLineArgs[0] is "-v" or "--version") {
                Write(versionText);
                return ERR_SUCCESS;
            }

            if(cmdLineArgs[0] is "-h" or "--help") {
                if(showBanner) WriteLine(bannerText);
                WriteLine(simpleHelpText);
                WriteLine(extendedHelpText);
                return ERR_SUCCESS;
            }

            if(args[0][0] is '-') {
                if(showBanner) WriteLine(bannerText);
                Console.Error.WriteLine("Invalid arguments: the input file is mandatory unless the first argument is --version or --help");
                return ERR_BAD_ARGUMENTS;
            }

            if(showBanner) WriteLine(bannerText);

            string errorMessage;

            ResetConfig();
            try {
                errorMessage = ProcessInputFileArgument(args[0]);
            }
            catch(Exception ex) {
                errorMessage = ex.Message;
            }

            if(errorMessage is not null) {
                PrintFatal($"Can't open input file: {errorMessage}");
                return ERR_CANT_OPEN_INPUT_FILE;
            }

            string outputFileArgument;
            if(args.Length == 1 || args[1][0] == '-') {
                outputFileArgument = "";
                args = args.Skip(1).ToArray();
            }
            else {
                outputFileArgument = args[1];
                args = args.Skip(2).ToArray();
            }

            try {
                errorMessage = ProcessOutputFileArgument(outputFileArgument);
            }
            catch(Exception ex) {
                errorMessage = ex.Message;
            }

            if(errorMessage is not null) {
                PrintFatal($"Can't create output file ({outputFilePath}): {errorMessage}");
                return ERR_CANT_CREATE_OUTPUT_FILE;
            }

            includeDirectories.Add(inputFileDirectory);

            errorMessage = ProcessArguments(args);
            if(errorMessage is not null) {
                Console.Error.WriteLine($"Invalid arguments: {errorMessage}");
                return ERR_BAD_ARGUMENTS;
            }

            if(mustChangeOutputFileExtension) {
                outputFilePath = Path.ChangeExtension(outputFilePath, ".BIN");
            }

            PrintProgress($"Input file: {inputFilePath}");

            var errCode = DoAssembly(out int writtenBytes);
            if(errCode != ERR_SUCCESS) {
                generateOutputFile = false;
            }

            totalTimeMeasurer.Stop();
            if(errCode == ERR_SUCCESS) {
                PrintProgress("\r\nAssembly completed!");
                PrintProgress($"Assembly time: {FormatTimespan(assemblyTimeMeasurer.Elapsed)}");
                PrintProgress($"Total time: {FormatTimespan(totalTimeMeasurer.Elapsed)}");
            }
            else {
                PrintProgress("\r\nAssembly failed");
            }

            if(generateOutputFile) {
                PrintProgress($"\r\nOutput file: {outputFilePath}");
                PrintProgress($"{writtenBytes} bytes written");
            } else {
                PrintProgress("\r\nNo output file generated");
            }

            return errCode;
        }

        private static string[] MaybeMergeArgsWithEnv(string[] commandLineArgs)
        {
            if(commandLineArgs.Any(a => a is "-nea" or "--no-env-args")) {
                return commandLineArgs;
            }

            var envVariable = Environment.GetEnvironmentVariable("N80_ARGS");
            if(envVariable is null) {
                return commandLineArgs;
            }

            string inputFile = null, outputFile = null;
            if(!commandLineArgs[0].StartsWith('-')) {
                inputFile = commandLineArgs[0];
                commandLineArgs = commandLineArgs[1..].ToArray();
            }
            if(commandLineArgs.Length > 1 && !commandLineArgs[0].StartsWith('-')) {
                outputFile = commandLineArgs[0];
                commandLineArgs = commandLineArgs[1..].ToArray();
            }

            envVariable = envVariable.Replace(@"\ ", "\u0001");
            var envVariableParts = envVariable.Split(" ", StringSplitOptions.RemoveEmptyEntries);
            envVariableParts = envVariableParts.Select(p => p.Replace("\u0001", " ")).ToArray();
            var allArgs = envVariableParts.Concat(commandLineArgs).ToArray();

            if(inputFile is null) {
                return allArgs;
            }
            else if(outputFile is null) {
                return new[] { inputFile }.Concat(allArgs).ToArray();
            }
            else {
                return new[] { inputFile, outputFile }.Concat(allArgs).ToArray();
            }
        }

        static void ResetConfig()
        {
            inputFileEncoding = Encoding.UTF8;
            mustChangeOutputFileExtension = false;
            colorPrint = true;
            showBanner = true;
            includeDirectories.Clear();
            orgAsPhase = false;
            symbolDefinitions.Clear();
            maxErrors = DEFAULT_MAX_ERRORS;
            skippedWarnings = Array.Empty<AssemblyErrorCode>();
            silenceStatus = false;
            silenceAssemblyPrints = false;
        }

        private static string FormatTimespan(TimeSpan ts)
        {
            if(ts.TotalSeconds < 1) {
                return $"{ts.TotalMilliseconds:0} ms";
            }

            return $"{(int)ts.TotalMinutes}:{ts.Seconds:00}.{ts.Milliseconds:0}";
        }

        private static void SetShowBannerFlag(string[] args)
        {
            var indexOfLastShowBanner = args.Select((arg, index) => new { arg, index }).LastOrDefault(x => x.arg is "-sb" or "--show-banner")?.index ?? -1;
            var indexOfLastNoShowBanner = args.Select((arg, index) => new { arg, index }).LastOrDefault(x => x.arg is "-nsb" or "--no-show-banner")?.index ?? -1;
            showBanner = indexOfLastNoShowBanner == -1 || indexOfLastShowBanner > indexOfLastNoShowBanner;
        }

        private static string? ProcessInputFileArgument(string fileSpecification)
        {
            if(!Path.IsPathRooted(fileSpecification)) {
                fileSpecification = Path.Combine(Directory.GetCurrentDirectory(), fileSpecification);
            }

            if(!File.Exists(fileSpecification)) {
                return "File not found";
            }

            inputFilePath = Path.GetFullPath(fileSpecification);
            inputFileDirectory = Path.GetDirectoryName(Path.GetFullPath(fileSpecification));
            inputFileName = Path.GetFileName(fileSpecification);

            return null;
        }

        private static string? ProcessOutputFileArgument(string fileSpecification)
        {
            if(fileSpecification is "") {
                outputFilePath = Path.Combine(Directory.GetCurrentDirectory(), inputFileName);
                mustChangeOutputFileExtension = true;
                return null;
            }
            
            if(fileSpecification is "$") {
                fileSpecification = Path.Combine(inputFileDirectory, inputFileName);
                mustChangeOutputFileExtension = true;
            }
            else if(fileSpecification.StartsWith("$/")) {
                fileSpecification = Path.Combine(inputFileDirectory, fileSpecification[2..]);
            }

            fileSpecification = Path.GetFullPath(fileSpecification);

            if(Directory.Exists(fileSpecification)) {
                outputFilePath = Path.Combine(fileSpecification, inputFileName);
                mustChangeOutputFileExtension = true;
            }
            else if(Directory.Exists(Path.GetDirectoryName(fileSpecification))) {
                outputFilePath = fileSpecification;
            }
            else {
                return "Directory not found";
            }

            return null;
        }

        private static string? ProcessArguments(string[] args)
        {
            for(int i=0; i<args.Length; i++) {
                var arg = args[i];
                if(arg is "-no" or "--no-output") {
                    generateOutputFile = false;
                }
                else if(arg is "-ie" or "--input-encoding") {
                    if(i == args.Length - 1 || args[i + 1][0] == '-') {
                        return $"The {arg} argument needs to be followed by an encoding page or name";
                    }
                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                    var inputFileEncodingName = args[i + 1];
                    try {
                        if(int.TryParse(inputFileEncodingName, out int encodingPage)) {
                            inputFileEncoding = Encoding.GetEncoding(encodingPage);
                        }
                        else {
                            inputFileEncoding = Encoding.GetEncoding(inputFileEncodingName);
                        }
                    }
                    catch {
                        return $"Unknown source file encoding '{inputFileEncodingName}'";
                    }
                    i++;
                }
                else if(arg is "-co" or "--color-output") {
                    colorPrint = true;
                }
                else if(arg is "-nco" or "--no-color-output") {
                    colorPrint = false;
                }
                else if(arg is "-sb" or "--show-banner" or "-nsb" or "--no-show-banner" or "-nea" or "--no-env-args") {
                    //already handled
                }
                else if(arg is "-v" or "--version" or "-h" or "--help") {
                    return $"The {arg} argument must be the first one";
                }
                else if(arg is "-id" or "--include-directory") {
                    if(i == args.Length - 1 || args[i + 1][0] == '-') {
                        return $"The {arg} argument needs to be followed by a directory path";
                    }
                    i++;
                    var dirName = args[i];
                    if(dirName is "$") {
                        dirName = inputFileDirectory;
                    }
                    else if(dirName.StartsWith("$/")) {
                        dirName = Path.Combine(inputFileDirectory, dirName[2..]);
                    }

                    dirName = Path.GetFullPath(dirName);

                    if(!Directory.Exists(dirName)) {
                        return "Directory not found";
                    }

                    if(!includeDirectories.Contains(dirName)) {
                        includeDirectories.Add(dirName);
                    }
                }
                else if(arg is "-cid" or "--clear-include-directories") {
                    includeDirectories.Clear();
                }
                else if(arg is "-oap" or "--org-as-phase") {
                    orgAsPhase = true;
                }
                else if(arg is "-noap" or "--no-org-as-phase") {
                    orgAsPhase = false;
                }
                else if(arg is "-ds" or "--define-symbols") {
                    if(i == args.Length - 1 || args[i + 1][0] == '-') {
                        return $"The {arg} argument needs to be followed by a list of symbol definitions";
                    }
                    else {
                        i++;
                        var symbolDefinitionStrings = args[i].Split(',', StringSplitOptions.RemoveEmptyEntries);
                        foreach(var symbolDefinitionString in symbolDefinitionStrings) {
                            var parts = symbolDefinitionString.Split('=');
                            if(parts.Length > 2) {
                                return $"{arg}: two '=' found in the same symbol definition";
                            }
                            if(parts.Any(p => p is "")) {
                                return $"{arg}: found symbol definition with missing name or value";
                            }
                            ushort value;
                            var name = parts[0];
                            if(parts.Length == 1) {
                                value = 0xFFFF;
                            }
                            else {
                                try {
                                    if(parts[1].EndsWith("h", StringComparison.OrdinalIgnoreCase)) {
                                        value = Convert.ToUInt16(parts[1][..^1], 16);
                                    }
                                    else {
                                        value = ushort.Parse(parts[1]);
                                    }
                                }
                                catch {
                                    return $"{arg}: Invalid value for symbol '{name}', values must decimal numbers or hexadecimal numbers with the 'h' suffix, and be in the range 0-65535/FFFFh";
                                }
                            }

                            symbolDefinitions.Add((name, value));
                        }
                    }
                }
                else if(arg is "-nds" or "--no-define-symbols") {
                    symbolDefinitions.Clear();
                }
                else if(arg is "-me" or "--max-errors") {
                    if(i == args.Length - 1 || args[i + 1][0] == '-') {
                        return $"The {arg} argument needs to be followed by an errors count";
                    }
                    i++;
                    try {
                        maxErrors = int.Parse(args[i]);
                    }
                    catch {
                        return $"Invalid number following the {arg} argument";
                    }
                }
                else if(arg is "-sw" or "--silence-warnings" or "-nsw" or "--no-silence-warnings") {
                    AssemblyErrorCode[] warningCodes;
                    if(i == args.Length - 1 || args[i + 1][0] == '-') {
                        warningCodes = Enum
                            .GetValues<AssemblyErrorCode>()
                            .Cast<AssemblyErrorCode>()
                            .Where(c => c < AssemblyErrorCode.FirstError)
                            .Distinct()
                            .ToArray();
                    }
                    else {
                        i++;
                        var warningCodesString = args[i];
                        try {
                            warningCodes =
                                warningCodesString
                                .Split(",", StringSplitOptions.RemoveEmptyEntries)
                                .Select(c => (AssemblyErrorCode)int.Parse(c))
                                .Where(c => c < AssemblyErrorCode.FirstError)
                                .ToArray();
                        }
                        catch {
                            return $"The {arg} argument needs to be followed by a comma-separated list of valid warning codes";
                        }
                    }

                    if(arg is "-sw" or "--silence-warnings") {
                        skippedWarnings = skippedWarnings.Concat(warningCodes).Distinct().ToArray();
                    }
                    else {
                        skippedWarnings = skippedWarnings.Except(warningCodes).ToArray();
                    }
                }
                else if(arg is "-ss" or "--silence-status") {
                    silenceStatus = true;
                }
                else if(arg is "-nss" or "--no-silence-status") {
                    silenceStatus = false;
                }
                else if(arg is "-sap" or "--silence-assembly-print") {
                    silenceAssemblyPrints = true;
                }
                else if(arg is "-nsap" or "--no-silence-assembly-print") {
                    silenceAssemblyPrints = false;
                }
                else if(arg is "-rc" or "--reset-config") {
                    ResetConfig();
                }
                else {
                    return $"Unknwon argument '{arg}'";
                }
            }

            return null;
        }

        private static int DoAssembly(out int writtenBytes)
        {
            Stream inputStream;
            writtenBytes = 0;

            try {
                inputStream = File.OpenRead(inputFilePath);
            }
            catch(Exception ex) {
                PrintFatal($"Can't open input file: {ex.Message}");
                return ERR_CANT_OPEN_INPUT_FILE;
            }

            AssemblySourceProcessor.AssemblyErrorGenerated += AssemblySourceProcessor_AssemblyErrorGenerated1;
            AssemblySourceProcessor.BuildTypeAutomaticallySelected += AssemblySourceProcessor_BuildTypeAutomaticallySelected1;
            AssemblySourceProcessor.Pass2Started += AssemblySourceProcessor_Pass2Started;

            if(!silenceAssemblyPrints) {
                AssemblySourceProcessor.PrintMessage += AssemblySourceProcessor_PrintMessage1;
            }

            var config = new AssemblyConfiguration() {
                GetStreamForInclude = GetStreamForInclude,
                PredefinedSymbols = symbolDefinitions.ToArray(),
                MaxErrors = maxErrors
            };
            assemblyTimeMeasurer.Start();
            var result = AssemblySourceProcessor.Assemble(inputStream, inputFileEncoding, config);
            assemblyTimeMeasurer.Stop();
            if(result.HasFatals) {
                return ERR_ASSEMBLY_FATAL;
            }
            else if(result.HasErrors) {
                return ERR_ASSEMBLY_ERROR;
            }

            Stream outputStream;

            try {
                outputStream = File.Create(outputFilePath);
            }
            catch(Exception ex) {
                PrintFatal($"Can't create output file: {ex.Message}");
                return ERR_CANT_CREATE_OUTPUT_FILE;
            }

            try {
                writtenBytes = OutputGenerator.GenerateAbsolute(result, outputStream, orgAsPhase);
            }
            catch(Exception ex) {
                PrintFatal($"Can't write to output file ({outputFilePath}): {ex.Message}");
                return ERR_CANT_CREATE_OUTPUT_FILE;
            }

            outputStream.Close();

            return ERR_SUCCESS;
        }

        private static void AssemblySourceProcessor_Pass2Started(object? sender, EventArgs e)
        {
            PrintProgress("\r\nPass 2 started");
            printInstructionExecuted = false;
        }

        private static void AssemblySourceProcessor_BuildTypeAutomaticallySelected1(object? sender, (string, int, BuildType) e)
        {
            var fileName = e.Item1 is null ? "" : $"[{e.Item1}]: ";
            PrintProgress($"\r\n{fileName}Line {e.Item2}: Output type automatically selected: {e.Item3}");
        }

        private static string FormatAssemblyError(AssemblyError error, string prefix)
        {
            var fileName = error.IncludeFileName is null ? "" : $"[{error.IncludeFileName}] ";
            var lineNumber = error.LineNumber is null ? "" : $"in line {error.LineNumber}: ";

            return $"{prefix}: {fileName}{lineNumber}{error.Message}";
        }

        private static void AssemblySourceProcessor_PrintMessage1(object? sender, string e)
        {
            PrintAssemblyPrint(e);
        }

        private static void AssemblySourceProcessor_AssemblyErrorGenerated1(object? sender, AssemblyError error)
        {
            if(error.IsWarning) {
                PrintWarning(error);
            }
            else if(error.IsFatal) {
                PrintFatal(error);
            }
            else {
                PrintError(error);
            }
        }

        private static void PrintWarning(AssemblyError error)
        {
            if(skippedWarnings.Contains(error.Code)) {
                return;
            }

            Console.Error.WriteLine();
            var text = FormatAssemblyError(error, "WARN");
            if(colorPrint) {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.BackgroundColor = defaultBackgroundColor;
                Console.Error.WriteLine(text);
                Console.ForegroundColor = defaultForegroundColor;
            }
            else {
                Console.Error.WriteLine(text);
            }
        }

        private static void PrintError(AssemblyError error)
        {
            Console.Error.WriteLine();
            var text = FormatAssemblyError(error, "ERROR");
            if(colorPrint) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.BackgroundColor = defaultBackgroundColor;
                Console.Error.WriteLine(text);
                Console.ForegroundColor = defaultForegroundColor;
            }
            else {
                Console.Error.WriteLine(text);
            }
        }

        private static void PrintFatal(AssemblyError error)
        {
            if(error.Code is AssemblyErrorCode.MaxErrorsReached) {
                PrintProgress("");
                if(printInstructionExecuted) {
                    PrintProgress("");
                    printInstructionExecuted = true;
                }
                PrintProgress(error.Message);
            }
            else {
                PrintFatal(FormatAssemblyError(error, "FATAL"));
            }
        }

        private static void PrintFatal(string text)
        {
            Console.Error.WriteLine();
            if(colorPrint) {
                Console.ForegroundColor = ConsoleColor.White;
                Console.BackgroundColor = ConsoleColor.Red;
                Console.Error.WriteLine(text);
                Console.ForegroundColor = defaultForegroundColor;
                Console.BackgroundColor = defaultBackgroundColor;
            }
            else {
                Console.Error.WriteLine(text);
            }
        }

        private static void PrintAssemblyPrint(string text)
        {
            if(!printInstructionExecuted) {
                WriteLine();
                printInstructionExecuted = true;
            }

            if(colorPrint) {
                Console.ForegroundColor = ConsoleColor.White;
                Console.BackgroundColor = defaultBackgroundColor;
                Console.WriteLine(text);
                Console.ForegroundColor = defaultForegroundColor;
            }
            else {
                Console.WriteLine(text);
            }
        }

        private static void PrintProgress(string text)
        {
            if(silenceStatus) {
                return;
            }

            if(colorPrint) {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.BackgroundColor = defaultBackgroundColor;
                Console.WriteLine(text);
                Console.ForegroundColor = defaultForegroundColor;
            }
            else {
                Console.WriteLine(text);
            }
        }

        private static Stream GetStreamForInclude(string includeFilePath)
        {
            foreach(var directory in includeDirectories) {
                var filePath = Path.Combine(directory, includeFilePath);
                if(!File.Exists(filePath)) {
                    return null;
                }

                return File.OpenRead(filePath);
            }

            return null;
        }

        static void Main_old(string[] args)
        {
            /*
            var sourceFileName = Path.Combine(Assembly.GetExecutingAssembly().Location, @"../../../../../HELLO.ASM");
            var sourceStream = new FileStream(sourceFileName, FileMode.Open, FileAccess.Read);
            var rx = AssemblySourceProcessor.Assemble(sourceStream, Encoding.UTF8);
            var msx = new MemoryStream();
            OutputGenerator.GenerateAbsolute(rx, msx);
            var theBytexz = msx.ToArray();
            File.WriteAllBytes(@"c:\bin\NestorHello.rom", theBytexz);
            return;

            var abscode = @"
org 0fffeh
ds 3,34
ds 2,12
end

org 0FFFEh
db 1,2,3
db 4
end

org 4000h
db 1,2,3,4
org 4100h
db 5,6,7,8
org 4010h
db 9,10,11,12
org 3F00h
db 13,14,15,16
";

            var r = AssemblySourceProcessor.Assemble(abscode, new AssemblyConfiguration() { BuildType = BuildType.Absolute });
            var ms = new MemoryStream();
            OutputGenerator.GenerateAbsolute(r, ms);
            var theBytez = ms.ToArray();
            var x = 0;
                                    */


            //Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            //var x = Encoding.GetEncodings().Select(e => new { e.CodePage, e.Name }).OrderBy(x=>x.CodePage).ToArray();

            //var sourceFileName = Path.Combine(Assembly.GetExecutingAssembly().Location, @"../../../../../SOURCE.MAC");
            //var sourceFileName = @"L:\home\konamiman\Nestor80\COMMENTS.MAC";
            //var sourceStream = new FileStream(sourceFileName, FileMode.Open, FileAccess.Read);

            var code =
@"
FOO: equ 2
RESETE: equ 18h
rst RESETE
rst FOO
bit BAR,(ix+FOO)
bit 7,b
bit 1,(ix+3)
bit BAR,c
bit BAR,(ix+4)
BAR: equ 5
end


ld a,FOO
FOO defl BAR
BAR defl FOO+1
end

if1
FOO defl 1
endif
ld a,FOO
FOO defl FOO+1
ld a,FOO
end

BAR equ 2
FIZZ: set 1,a
BUZZ set 2,b
JEJE: ld a,3
end

jr $+1
end

foo equ 99h
ld a,(ix+90h)
ld a,(iy-foo)
here:
jr here
aseg
org 100h
horo:
jr horo
jr 105h
end

db 0
if1
include foo/bar.asm
endif
.warn Warn in main file
db 1
end

foo:
ld a,foo
end

x:
dseg
y:
cseg
ld e,type x+y
ld a,foo##+34
ld b,type foo##
ld c,type 34+foo##
ld d,type x
end

ld a,foobarcios1##
ld b,foobarcios2##
db 0
foobarcios3:
db 1
foobarcios4::
end

db 0
if1
db 1
foo:
else
foo:
endif
jp foo
end

FOO equ 1
db 34
db FOO
db BAR
db FIZZ##
BAR equ 2
end

ds 16
jp fizz
foo:
.phase 100h
jp bar
bar:
db 0
jp fizz
.dephase
fizz:
end

dw 1+(BAR## SHL 8)
end

db 1+FOO-BAR*FIZZ##
FOO equ 1
BAR:
end

ld a,12h
jp 1234h

FOO:
ld a,FOO
jp FOO
end

db FOO+BAR
end

foo equ 12h
db foo,bar,fizz+1
fizz equ 5566h
bar equ 77h
end

db 0
db 0
include foo/bar.asm
db 1
foocios equ 1
ld a,barcios
.warn Warn in main code
end

.print {2+2}
.warn ;oo {1+1}
.error {3+3}
.fatal {4+4}

.print hello!
.warn Oops, some warning
.error Yikes, some error
.fatal Aaargh, fatal!!

db 0
  if2
    db 1
    if1
      db 2
    else
      db 3
    endif
    db 4
  else
    db 5
  endif
db 6
end

ifdif <a>,<A>
db 0
endif

ifdif <a>,<a>
db 0
endif
end

ifidn <a>,<a>
db 0
endif

ifidn <a>,<>
db 1
endif

ifidn <a>,<A>
db 1
endif

ifidn <a>
endif

ifidn <a>,
endif

ifidn <a>,<a
endif

end

ifb <>
db 0
endif

ifb < >
db 1
endif

ifnb <>
db 2
endif

ifnb < >
db 2
endif

ifb
endif

ifb <
endif

end

iff 0
db 34
endif
foo equ 1
bar:
extrn fizz
ifdef foo
db 0
endif
ifdef bar
db 1
endif
ifdef fizz
db 2
endif
ifndef NOPE
db 3
endif
end

db 0
  if 0
    db 1
    if 1
      db 2
    else
      db 3
    endif
    db 4
  else
    db 5
  endif
db 6
end

if 1
db 1
bar:
foo: else
fizz: db 2
buzz: endif
end

CSEG
end

;public FOO
;end

;extrn FOO
;end

;ld a,(FOO##)
;end

FOO::
end

ds 1
end

db 0
end

dseg
foo:
end

org 100h
foo:
end

.cpu r800
mulub
.z80
mulub a,b
ex af,af
ex af,af'
end

ds 0F0h
foo:
db 0
foo2:
resete equ 18h
jp (hl)
jp 34
jp foo
jp bar
ld a,34
ld a,foo
ld a,bar
rst 18h
rst resete
rst bar
ld a,(ix+34)
ld a,(ix+foo)
ld a,(ix+bar)
ld (ix+34),a
ld (ix+foo),a
ld (ix+bar),a
ld (ix+34),89
ld (ix+34),foo
ld (ix+34),bar
ld (ix+foo),34
ld (ix+foo),bar
ld (ix+bar),34
ld (ix+bar),foo
ld (ix+bar),fizz
ld (ix+foo),foo2
bar:
fizz::
end


ld (ix+1),bar
end


bit 7,a
end

ld a,(34)
end

jr c,34
end


inc (ix+255)
end

jp FOO
FOO:
jp FOO
end

jp (hl)
;inc hl
hl equ 1
jp (hl)

ret

aseg
;ds 16
;jr 0
foo:
;jr foo
jr bar
bar:
end

;inc a
;inc hl
;dec (hl)
;rst 18h
;inc (ix+34)
;dec (ix+130)
jp 1234h



BC:
a equ 1

end
nop foo
ret
end

FOO equ 12abh
BAR equ 7
.print ola ke ase
.print1 Esto es FOO: {fizz} {foo+1:b0}, {foo:b-1} {foo+1:d8}, {foo+1:b}, {foo+1:B20}, {foo+1:h}, {foo+1:H}, {foo+1:h7} en default
.print2 Esto es BAR: {bar+1} en default

end

.LIST
.XLIST
.TFCOND
.SFCOND
.LFCOND
end

.request foo,bar,,@fizz,á,
end

dz ""ABC""
.strenc utf-16
defz ""ABC""
.strenc ascii
defz ""ABC""
end


db 1
FOO:
BAR equ 2
extrn FIZZ
end

.comment \
title defl 1
title Hola ke ase
foo: title defl title+1
bar: title Hola ke ase mas
page 50
fizz: page 70
page equ 80
buzz: page equ 90
end


FOO equ 1234h
BAR equ 7
.print1 Esto es FOO: {foo+1} en default
.print1 Esto es BAR: {bar+1} en default
.print1 Esto es FOO: {d:foo+1} en dec
.print1 Esto es FOO: {D:foo+1} en DEC
.print1 Esto es BAR: {d:bar+1} en dec
.print1 Esto es BAR: {D:bar+1} en DEC
.print1 Esto es FOO: {h:foo+1} en hex
.print1 Esto es FOO: {H:foo+1} en HEX
.print1 Esto es BAR: {h:bar+1} en hex
.print1 Esto es BAR: {H:bar+1} en HEX
.print1 Esto es FOO: {b:foo+1} en bin
.print1 Esto es FOO: {B:foo+1} en BIN
.print1 Esto es BAR: {b:bar+1} en bin
.print1 Esto es BAR: {B:bar+1} en BIN
end

.printx
.printx Ola ke ase
.printx /mola/
.printx /bueno, me delimito/ y tal
  .printx /a/
  .printx //
end

\
title
title ;
title Foo, bar; fizz, buzz
subttl
subttl ;
subttl eso mismo; digo yo
$title
$title('
$title('')
$title ('')
$title('mola mucho') ; y tanto
$title ('mola mucho') ; y tanto
$title('molamil')
page
page break
page foo
page 9
page 1234h
end


db +
db 2+
db 2 eq
db 2+(
end

name(
name('foo')
name('bar') ;bar!
 name ('fizz')
 name ('buzz') ;buzz!
name
name('')
name('1')

end

.z80
.cpu z80
;.8080
.cpu unknownx

db 0
end

db 0,0,0
foo:
.radix foo
.radix bar
.radix 1
.radix 17
.radix 1+1
db 1010
.radix 17-1
db 80
bar equ 3
end

ds
ds foo
ds 34
ds 89,bar
ds 12,99
ds 44,1234
ds ""AB""
end


dc
dc 1
dc 'ABC'+1
dc 'ABC',
dc 'ABC'
.strenc 850
dc 'áBC'
dc )(

end

db
dw
dw 'AB', ""CD"", 1, 1234h, '', ""\r\n""
end

db ""\r\n""
db '\r\n'

.stresc
.stresc pepe
.stresc off
db ""\r\n""
.stresc on
db ""\r\n""


end

.strenc

.strenc 28591
db 'á'
.strenc iso-8859-1
db 'á'
.strenc 850
db 'á'
.strenc ibm850
db 'á'
.strenc default
db 'á'
end

public foo
extrn foo
end

db foo

FOO defl 2
db foo
foo defl foo+1
db foo
foo aset foo+1
db foo
foo set foo+1
db foo

end

db 1

FOO: .COMMENT abc

db 2
xxx
;Mooola
ddd
xxxaxxx

db 3
end

db foo
public foo
foo:

db bar
extrn bar

end

    org 1
foo::
foo:

dseg ;1
  dseg;1
  dseg ,1

    org 1

ñokis::

    public çaço

    BAR:
    BAR:
    db EXT##


   ; Foo
  BLANK_NO_LABEL:  
  COMMENT_LABEL:  ;Bar
  PUBLIC::
DEBE: defb 34
    INVA-LID:

EXTRN EXT2

  db 1, 2+2 ,,FOO*5, 'Hola', EXT##, BAR+2, FOO*7, EXT2

    org

    dseg ,TAL
DSEG1: db 0
    ;org 10 , cual
DSEG2: db 1
    org 1
    org DSEG3
DSEG3:
";
            var config = new AssemblyConfiguration() {
                DefaultProgramName = "SOURCE",
                Print = (s) => Debug.WriteLine(s),
                OutputStringEncoding = "ascii",
                AllowEscapesInStrings = true,
                //BuildType = BuildType.Absolute,
                GetStreamForInclude = (name) => {
                    string code;
                    if(name == "foo/bar.asm") {
                        code = "bars: db 34\r\n.warn Warn in include 1\r\ninclude bar/fizz.asm\r\ndb 89\r\n";
                        //code = "include foo/bar.asm\r\n";
                    }
                    else {
                        code = "fizzs: .warn Warn in include 2\r\ndw 2324h\r\n";
                    }
                    return new MemoryStream(Encoding.ASCII.GetBytes(code));
                }
            };

            AssemblySourceProcessor.PrintMessage += AssemblySourceProcessor_PrintMessage;
            AssemblySourceProcessor.AssemblyErrorGenerated += AssemblySourceProcessor_AssemblyErrorGenerated;
            AssemblySourceProcessor.BuildTypeAutomaticallySelected += AssemblySourceProcessor_BuildTypeAutomaticallySelected;

            var result = AssemblySourceProcessor.Assemble(code, config);
            //var result = AssemblySourceProcessor.Assemble(sourceStream, Encoding.GetEncoding("iso-8859-1"), config);
        }

        private static void AssemblySourceProcessor_BuildTypeAutomaticallySelected(object? sender, (string, int, BuildType) e)
        {
            WriteLine($"In line {e.Item1} build type was automatically selected as {e.Item2.ToString().ToUpper()}");
        }

        private static void AssemblySourceProcessor_AssemblyErrorGenerated(object? sender, Assembler.Output.AssemblyError e)
        {
            WriteLine(e.ToString());
        }

        private static void AssemblySourceProcessor_PrintMessage(object? sender, string e)
        {
 
            WriteLine(e);
        }
    }
}