using Konamiman.Nestor80.Assembler;
using Konamiman.Nestor80.Assembler.Output;
using System.Diagnostics;
using System.Reflection;
using System.Text;

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
        static bool silenceAssemblyPrints;
        static bool showAssemblyDuration;
        static int verbosityLevel;
        static string stringEncoding;
        static BuildType buildType;
        static bool stringEscapes;
        static string defaultCpu;
        static string outputFileExtension = null;
        static bool allowBareExpressions;
        static bool initDefs;
        static bool sourceInErrorMessage;

        static readonly ConsoleColor defaultForegroundColor = Console.ForegroundColor;
        static readonly ConsoleColor defaultBackgroundColor = Console.BackgroundColor;
        static readonly Stopwatch assemblyTimeMeasurer = new();
        static readonly Stopwatch totalTimeMeasurer = new();
        static readonly List<(string, string[])> argsByFile = new();
        static bool n80FileUsed = false;
        static readonly List<AssemblyError> warningsFromPass1 = new();
        static bool inPass2 = false;
        static string[] envArgs = null;
        static string[] commandLineArgs;
        static string currentFileDirectory;
        static int printedWarningsCount = 0;

        static int Main(string[] args)
        {
            totalTimeMeasurer.Start();

            if(args.Length == 0) {
                WriteLine(bannerText);
                WriteLine(simpleHelpText);
                return ERR_SUCCESS;
            }

            if(args[0] is "-v" or "--version") {
                //Yeah I know, not very clean/performant...
                //but how often do you check the version number of the programs you use?
                var version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                while(version.EndsWith(".0") && version.Count(ch => ch is '.') > 1) {
                    version = version[..^2];
                }
                Console.Write(version);
                return ERR_SUCCESS;
            }

            if(args[0] is "--list-encodings") {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                var encodings = Encoding.GetEncodings().OrderBy(e => e.Name);
                var longestNameLength = encodings.Max(e => e.Name.Length);
                var formatString = $"{{0,-{longestNameLength}}} | {{1}}";
                Console.WriteLine("Available encodings for strings:");
                Console.WriteLine();
                Console.WriteLine(string.Format(formatString, "Name", "Code page"));
                Console.WriteLine(new string('-', longestNameLength + 12));
                foreach(var encoding in encodings) {
                    Console.WriteLine(string.Format(formatString, encoding.Name, encoding.CodePage));
                }
                return ERR_SUCCESS;
            }

            if(args[0] is "-h" or "--help") {
                WriteLine(bannerText);
                WriteLine(simpleHelpText);
                WriteLine(extendedHelpText);
                return ERR_SUCCESS;
            }

            string inputFile = null, outputFile = null;
            if(!args[0].StartsWith('-')) {
                inputFile = args[0];
                args = args[1..].ToArray();
            }
            if(args.Length > 0 && !args[0].StartsWith('-')) {
                outputFile = args[0];
                args = args[1..].ToArray();
            }

            if(inputFile is null) {
                ErrorWriteLine("Invalid arguments: the input file is mandatory unless the first argument is --version, --help or --list-encodings");
                return ERR_BAD_ARGUMENTS;
            }

            string inputFileErrorMessage = null;

            try {
                inputFileErrorMessage = ProcessInputFileArgument(inputFile);
            }
            catch(Exception ex) {
                inputFileErrorMessage = ex.Message;
            }

            ResetConfig();

            commandLineArgs = args;
            args = MaybeMergeArgsWithEnvAndFile(args);
            SetShowBannerFlag(args);

            if(showBanner) WriteLine(bannerText);

            var argsErrorMessage = ProcessArguments(args);
            if(argsErrorMessage is not null) {
                ErrorWriteLine($"Invalid arguments: {argsErrorMessage}");
                return ERR_BAD_ARGUMENTS;
            }

            if(inputFileErrorMessage is not null) {
                PrintFatal($"Can't open input file: {inputFileErrorMessage}");
                return ERR_CANT_OPEN_INPUT_FILE;
            }

            string outputFileErrorMessage;
            try {
                outputFileErrorMessage = ProcessOutputFileArgument(outputFile);
            }
            catch(Exception ex) {
                outputFileErrorMessage = ex.Message;
            }

            if(outputFileErrorMessage is not null) {
                PrintFatal($"Can't create output file ({outputFilePath}): {outputFileErrorMessage}");
                return ERR_CANT_CREATE_OUTPUT_FILE;
            }

            PrintProgress($"Input file: {inputFilePath}\r\n", 1);

            PrintArgumentsAndIncludeDirs();

            var errCode = DoAssembly(out int writtenBytes, out int warnCount, out int errCount, out int fatalCount);
            if(errCode != ERR_SUCCESS) {
                generateOutputFile = false;
            }

            totalTimeMeasurer.Stop();

            if(errCode == ERR_SUCCESS) {
                if(warnCount == 0) {
                    PrintProgress("\r\nAssembly completed!", 1);
                }
                else if(warnCount == 1) {
                    PrintProgress("\r\nAssembly completed with 1 warning", 1);
                }
                else {
                    PrintProgress($"\r\nAssembly completed with {warnCount} warnings", 1);
                }

                if(showAssemblyDuration) {
                    PrintDuration($"Assembly time: {FormatTimespan(assemblyTimeMeasurer.Elapsed)}");
                    PrintDuration($"Total time:    {FormatTimespan(totalTimeMeasurer.Elapsed)}");
                }
            }
            else {
                List<string> errorCounts = new();
                if(warnCount > 0) {
                    errorCounts.Add(warnCount == 1 ? "1 warning" : $"{warnCount} warnings");
                }
                if(errCount > 0) {
                    errorCounts.Add(errCount == 1 ? "1 error" : $"{errCount} errors");
                }
                if(fatalCount > 0) {
                    errorCounts.Add(fatalCount == 1 ? "1 fatal" : $"{fatalCount} fatals");
                }

                var failedWithString = warnCount + errCount + fatalCount == 0 ? "" : $" with {string.Join(", ", errorCounts.ToArray())}";

                PrintProgress($"\r\nAssembly failed{failedWithString}", 1);
            }

            if(generateOutputFile) {
                PrintProgress($"\r\nOutput file: {outputFilePath}", 1);
                PrintProgress($"{writtenBytes} bytes written", 1);
            } else {
                PrintProgress("\r\nNo output file generated", 1);
            }

            return errCode;
        }

        static bool blankLinePrinted = false;
        static void WriteLine(string text = "")
        {
            if(blankLinePrinted) {
                if(text.StartsWith("\r\n")) {
                    text = text[2..];
                }
                if(text != "") {
                    Console.WriteLine(text);
                }
            }
            else {
                Console.WriteLine(text);
            }

            blankLinePrinted = text == "" || text.EndsWith("\r\n");
        }

        static void ErrorWriteLine(string text = "")
        {
            if(Console.IsErrorRedirected) {
                Console.Error.WriteLine(text.Trim('\r', '\n'));
            }
            else {
                WriteLine(text);
            }
        }

        private static string[] MaybeMergeArgsWithEnvAndFile(string[] commandLineArgs)
        {
            string[] fileArgs = null;

            if(!commandLineArgs.Any(a => a is "-nea" or "--no-env-args")) {
                var envVariable = Environment.GetEnvironmentVariable("N80_ARGS");
                if(envVariable is not null) {
                    envArgs = SplitWithEscapedSpaces(envVariable);
                }
                else {
                    envArgs = Array.Empty<string>();
                }
            }

            var cmdAndEnvArgs = envArgs.Concat(commandLineArgs).ToArray();

            var indexOfLastFileArgs = cmdAndEnvArgs.Select((arg, index) => new { arg, index }).LastOrDefault(x => x.arg is "-dfa" or "--default-file-args")?.index ?? -1;
            var indexOfLastNoFileArgs = cmdAndEnvArgs.Select((arg, index) => new { arg, index }).LastOrDefault(x => x.arg is "-ndfa" or "--no-default-file-args")?.index ?? -1;
            if(indexOfLastNoFileArgs != -1 && indexOfLastFileArgs < indexOfLastNoFileArgs) {
                return cmdAndEnvArgs;
            }

            if(inputFileDirectory is null) {
                //This condition will result in an error anyway
                return cmdAndEnvArgs;
            }

            var n80FilePath = Path.GetFullPath(Path.Combine(inputFileDirectory, ".N80"));
            if(!File.Exists(n80FilePath)) {
                return cmdAndEnvArgs;
            }

            try {
                var fileLines = File.ReadAllLines(n80FilePath).Select(l => l.Trim()).Where(l => l[0] is not ';' and not '#').ToArray();
                var fileArgsLine = string.Join(' ', fileLines);
                fileArgs = SplitWithEscapedSpaces(fileArgsLine);

                argsByFile.Add((n80FilePath, fileArgs));

                n80FileUsed = true;
                return envArgs.Concat(fileArgs).Concat(commandLineArgs).ToArray();
            }
            catch(Exception ex) {
                PrintFatal($"Can't read arguments from file {n80FilePath}: {ex.Message}. Use the -nfa / --no-file-args option to skip it.");
                return cmdAndEnvArgs;
            }
        }

        private static void PrintArgumentsAndIncludeDirs()
        {
            if(verbosityLevel < 3) {
                return;
            }

            var info = "";
            if(envArgs?.Length > 0) {
                info += $"Args from N80_ARGS: {string.Join(' ', envArgs)}\r\n";
            }

            var filesFirstIndex = 0;
            if(n80FileUsed) {
                info += $"Args from {argsByFile[0].Item1}: {string.Join(' ', argsByFile[0].Item2)}\r\n";
                filesFirstIndex++;
            }

            info += $"Args from command line: {string.Join(' ', commandLineArgs)}\r\n";

            for(int i = filesFirstIndex; i < argsByFile.Count; i++) {
                info += $"Args from {argsByFile[i].Item1}: {string.Join(' ', argsByFile[i].Item2)}\r\n";
            }

            if(includeDirectories.Count > 0) {
                info += "\r\nExtra directories for INCLUDE:\r\n";
                foreach(var id in includeDirectories) {
                    info += "  " + id + "\r\n";
                }
            }
            else {
                info += "\r\nNo extra directories for INCLUDE\r\n";
            }

            if(symbolDefinitions.Count > 0) {
                var symbolDefinitionsAsStrings = symbolDefinitions.OrderBy(d => d.Item1).Select(d => $"{d.Item1}={d.Item2:X4}h").ToArray();
                info += $"\r\nSymbol definitions: {string.Join(", ", symbolDefinitionsAsStrings)}\r\n";
            }

            info += $"\r\nInput file encoding: {inputFileEncoding.WebName}\r\n";
            info += $"Color output: {YesOrNo(colorPrint)}\r\n";
            info += $"Show program banner: {YesOrNo(showBanner)}\r\n";
            info += $"ORG as PHASE: {YesOrNo(orgAsPhase)}\r\n";
            info += $"Max errors: {(maxErrors == 0 ? "infinite" : maxErrors.ToString())}\r\n";
            if(skippedWarnings.Length > 0) {
                var silencedWarningsString = skippedWarnings.Length == (int)AssemblyErrorCode.LastWarning ? "all" : string.Join(", ", skippedWarnings.Select(w => (int)w).ToArray());
                info += $"Silenced warnings: {silencedWarningsString}\r\n";
            }
            info += $"Show assembly prints: {YesOrNo(!silenceAssemblyPrints)}\r\n";
            info += $"Show assembly duration: {YesOrNo(showAssemblyDuration)}\r\n";
            info += $"Status verbosity level: {verbosityLevel}\r\n";
            info += $"Encoding for strings in code: {stringEncoding}\r\n";
            info += $"Parse escape sequences in strings: {YesOrNo(stringEscapes)}\r\n";
            info += $"Build type: {buildType}\r\n";
            info += $"Default CPU: {defaultCpu.ToUpper()}\r\n";
            info += $"Allow bare expressions: {YesOrNo(allowBareExpressions)}\r\n";
            info += $"Expand DEFS instructions: {YesOrNo(initDefs)}\r\n";
            info += $"Show source in error messages: {YesOrNo(sourceInErrorMessage)}\r\n";

            if(mustChangeOutputFileExtension) {
                var outputExtension =
                    outputFileExtension ?? buildType switch {
                        BuildType.Absolute => ".BIN",
                        BuildType.Relocatable => ".REL",
                        _ => ".BIN or .REL"
                    };
                info += $"Output file extension: {outputExtension}\r\n";
            }

            PrintProgress(info, 3);
        }

        private static string YesOrNo(bool what)
        {
            return what ? "YES" : "NO";
        }

        private static string[] SplitWithEscapedSpaces(string argsString)
        {
            argsString = argsString.Replace(@"\ ", "\u0001");
            var parts = argsString.Split(" ", StringSplitOptions.RemoveEmptyEntries);
            return parts.Select(p => p.Replace("\u0001", " ")).ToArray();
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
            silenceAssemblyPrints = false;
            showAssemblyDuration = false;
            verbosityLevel = 1;
            stringEncoding = "ASCII";
            stringEscapes = true;
            buildType = BuildType.Automatic;
            defaultCpu = "Z80";
            outputFileExtension = null;
            allowBareExpressions = false;
            initDefs = false;
            sourceInErrorMessage = false;
            currentFileDirectory = inputFileDirectory;
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
            if(fileSpecification is null) {
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

        private static string? ProcessArguments(string[] args, bool fromFile = false)
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
                else if(arg is "-sb" or "--show-banner" or "-nsb" or "--no-show-banner" or "-nea" or "--no-env-args" or "-nfa" or "--no-file-args") {
                    //already handled
                }
                else if(arg is "-v" or "--version" or "-h" or "--help" or "--list-encodings") {
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
                    else {
                        dirName = Path.Combine(Environment.CurrentDirectory, dirName);
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
                    buildType = BuildType.Absolute;
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
                            .Where(c => c != AssemblyErrorCode.None && c < AssemblyErrorCode.FirstError)
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
                                .Where(c => c != AssemblyErrorCode.None && c < AssemblyErrorCode.FirstError)
                                .Distinct()
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
                else if(arg is "-sap" or "--silence-assembly-print") {
                    silenceAssemblyPrints = true;
                }
                else if(arg is "-nsap" or "--no-silence-assembly-print") {
                    silenceAssemblyPrints = false;
                }
                else if(arg is "-rc" or "--reset-config") {
                    ResetConfig();
                }
                else if(arg is "-sad" or "--show-assembly-duration") {
                    showAssemblyDuration = true;
                }
                else if(arg is "-nsad" or "--no-show-assembly-duration") {
                    showAssemblyDuration = false;
                }
                else if(arg is "-af" or "--args-file") {
                    if(fromFile) {
                        return $"{arg} argument can't be used from inside an arguments file";
                    }
                    if(i == args.Length - 1 || args[i + 1][0] == '-') {
                        return $"The {arg} argument needs to be followed by a file specification";
                    }

                    i++;
                    string errorMessage;
                    try {
                        errorMessage = ProcessArgsFromFile(args[i]);
                    }
                    catch(Exception ex) {
                        errorMessage = ex.Message;
                    }

                    if(errorMessage is not null) {
                        return $"Error when processing arguments file {args[i]}: {errorMessage}";
                    }
                }
                else if(arg is "-sv" or "--status-verbosity") {
                    if(i == args.Length - 1 || args[i + 1][0] == '-') {
                        return $"The {arg} argument needs to be followed by a verbosity level (a number between 0 and 3)";
                    }

                    i++;
                    if(!int.TryParse(args[i], out verbosityLevel)) {
                        return $"The {arg} argument needs to be followed by a verbosity level (a number between 0 and 3)";
                    }
                    verbosityLevel = Math.Clamp(verbosityLevel, 0, 3);
                }
                else if(arg is "-se" or "--string-encoding") {
                    if(i == args.Length - 1 || args[i + 1][0] == '-') {
                        return $"The {arg} argument needs to be followed by an encoding page or name";
                    }
                    i++;
                    stringEncoding = args[i];
                }
                else if(arg is "-bt" or "--build-type") {
                    if(i == args.Length - 1 || args[i + 1][0] == '-') {
                        return $"The {arg} argument needs to be followed by the build type (abs, rel or auto)";
                    }
                    i++;
                    var buildTypeString = args[i];

                    buildType = buildTypeString switch {
                        "abs" => BuildType.Absolute,
                        "rel" => BuildType.Relocatable,
                        "auto" => BuildType.Automatic,
                        _ => BuildType.Invalid
                    };

                    if(buildType is BuildType.Invalid) {
                        return $"{arg}: the build type must be one of: abs, rel, auto";
                    }
                }
                else if(arg is "-sx" or "--string-escapes") {
                    stringEscapes = true;
                }
                else if(arg is "-nsx" or "--no-string-escapes") {
                    stringEscapes = false;
                }
                else if(arg is "-cpu" or "--default-cpu") {
                    if(i == args.Length - 1 || args[i + 1][0] == '-') {
                        return $"The {arg} argument needs to be followed by a CPU name";
                    }
                    i++;
                    defaultCpu = args[i];
                    if(!AssemblySourceProcessor.IsValidCpu(defaultCpu)) {
                        return $"'{defaultCpu}' is not a supported CPU.";
                    }
                }
                else if(arg is "-ofe" or "--output-file-extension") {
                    if(i == args.Length - 1 || args[i + 1][0] == '-') {
                        return $"The {arg} argument needs to be followed by a file extension";
                    }
                    i++;
                    outputFileExtension = args[i];
                }
                else if(arg is "-abe" or "--allow-bare-expressions") {
                    allowBareExpressions = true;
                }
                else if(arg is "-nabe" or "--no-allow-bare-expressions") {
                    allowBareExpressions = false;
                }
                else if(arg is "-ids" or "--initialize-defs") {
                    initDefs = true;
                }
                else if(arg is "-nids" or "--no-initialize-defs") {
                    initDefs = false;
                }
                else if(arg is "-sie" or "--source-in-errors") {
                    sourceInErrorMessage = true;
                }
                else if(arg is "-nsie" or "--no-source-in-errors") {
                    sourceInErrorMessage = false;
                }
                else {
                    return $"Unknwon argument '{arg}'";
                }
            }

            return null;
        }

        private static string ProcessArgsFromFile(string fileName)
        {
            string filePath;

            if(fileName.StartsWith("$/")) {
                filePath = Path.GetFullPath(Path.Combine(inputFileDirectory, fileName[2..]));
            }
            else {
                filePath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, fileName));
            }

            if(!File.Exists(filePath)) {
                return $"File not found";
            }
   
            var fileLines = File.ReadLines(filePath).Select(l => l.Trim()).Where(l => l[0] is not ';' and not '#').ToArray();
            var fileArgsString = string.Join(' ', fileLines);
            var fileArgs = SplitWithEscapedSpaces(fileArgsString);

            argsByFile.Add((filePath, fileArgs));
            return ProcessArguments(fileArgs, true);
        }

        private static int DoAssembly(out int writtenBytes, out int warnCount, out int errCount, out int fatalCount)
        {
            Stream inputStream;
            writtenBytes = warnCount = errCount = fatalCount = 0;

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
            AssemblySourceProcessor.IncludedFileFinished += AssemblySourceProcessor_IncludedFileFinished;

            if(!silenceAssemblyPrints) {
                AssemblySourceProcessor.PrintMessage += AssemblySourceProcessor_PrintMessage1;
            }

            var config = new AssemblyConfiguration() {
                OutputStringEncoding = stringEncoding,
                AllowEscapesInStrings = stringEscapes,
                BuildType = buildType,
                GetStreamForInclude = GetStreamForInclude,
                PredefinedSymbols = symbolDefinitions.ToArray(),
                MaxErrors = maxErrors,
                CpuName = defaultCpu,
                AllowBareExpressions = allowBareExpressions
            };

            if(showAssemblyDuration) assemblyTimeMeasurer.Start();
            var result = AssemblySourceProcessor.Assemble(inputStream, inputFileEncoding, config);
            if(showAssemblyDuration) assemblyTimeMeasurer.Stop();

            warnCount = printedWarningsCount;
            errCount = result.Errors.Count(e => !e.IsWarning && !e.IsFatal);
            fatalCount = result.Errors.Count(e => e.IsFatal);

            if(result.HasFatals) {
                return ERR_ASSEMBLY_FATAL;
            }
            else if(result.HasErrors) {
                return ERR_ASSEMBLY_ERROR;
            }

            if(mustChangeOutputFileExtension) {
                outputFileExtension ??= result.BuildType is BuildType.Relocatable ? ".REL" : ".BIN";
                outputFilePath = Path.ChangeExtension(outputFilePath, outputFileExtension);
            }

            Stream outputStream;

            try {
                outputStream = File.Create(outputFilePath);
            }
            catch(Exception ex) {
                PrintFatal($"Can't create output file: {ex.Message}");
                return ERR_CANT_CREATE_OUTPUT_FILE;
            }

            if(result.ProgramName is null) {
                var inputFileNameNoExt = Path.GetFileNameWithoutExtension(inputFileName).ToUpper();
                result.ProgramName =
                    inputFileNameNoExt.Length > AssemblySourceProcessor.MaxEffectiveExternalNameLength ?
                    inputFileNameNoExt[..AssemblySourceProcessor.MaxEffectiveExternalNameLength] :
                    inputFileNameNoExt;
            }

            try {
                writtenBytes =
                    result.BuildType is BuildType.Relocatable ?
                    OutputGenerator.GenerateRelocatable(result, outputStream, initDefs):
                    OutputGenerator.GenerateAbsolute(result, outputStream, orgAsPhase);
            }
            catch(Exception ex) {
                PrintFatal($"Can't write to output file ({outputFilePath}): {ex.Message}");
                return ERR_CANT_CREATE_OUTPUT_FILE;
            }

            outputStream.Close();

            return ERR_SUCCESS;
        }

        private static void AssemblySourceProcessor_IncludedFileFinished(object? sender, EventArgs e)
        {
            currentFileDirectory = PreviousCurrentFileDirectories.Pop();
        }

        private static void AssemblySourceProcessor_Pass2Started(object? sender, EventArgs e)
        {
            inPass2 = true;
            PrintProgress($"\r\nPass 2 started\r\n", 2);
        }

        private static void AssemblySourceProcessor_BuildTypeAutomaticallySelected1(object? sender, (string, int, BuildType) e)
        {
            var fileName = e.Item1 is null ? "" : $"[{e.Item1}]: ";
            PrintProgress($"\r\n{fileName}Line {e.Item2}: Build type automatically selected: {e.Item3}", 2);
        }

        private static string FormatAssemblyError(AssemblyError error, string prefix)
        {
            var fileName = error.IncludeFileName is null ? "" : $"[{error.IncludeFileName}] ";
            var macroInfo = error.IsMacroLine ? $"<{string.Join(" --> ", error.MacroNamesAndLines.Select(nl => $"{nl.Item1}:{nl.Item2}").ToArray())}> " : "";
            var lineNumber = error.LineNumber is null ? "" : $"in line {error.LineNumber}: ";
            var errorCode = verbosityLevel >= 2 ? $"({(int)error.Code}) " : "";

            string lineText = null;
            if(sourceInErrorMessage && !string.IsNullOrWhiteSpace(prefix)) {
                lineText = $"{new string(' ', prefix.Length+2)}{error.SourceLineText}\r\n";
            }

            return $"\r\n{prefix}: {errorCode}{fileName}{macroInfo}{lineNumber}{error.Message}\r\n{lineText}";
        }

        private static void AssemblySourceProcessor_PrintMessage1(object? sender, string e)
        {
            PrintAssemblyPrint(e);
        }

        private static void AssemblySourceProcessor_AssemblyErrorGenerated1(object? sender, AssemblyError error)
        {
            if(error.IsWarning && skippedWarnings.Contains(error.Code)) {
                return;
            }

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
            var isDuplicateWarning = warningsFromPass1.Contains(error);
            if(inPass2) {
                if(verbosityLevel < 2 && isDuplicateWarning) {
                    return;
                }
            }
            else if(!isDuplicateWarning) {
                warningsFromPass1.Add(error);
            }

            var text = FormatAssemblyError(error, "WARN");
            if(colorPrint) {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.BackgroundColor = defaultBackgroundColor;
                ErrorWriteLine(text);
                Console.ForegroundColor = defaultForegroundColor;
            }
            else {
                ErrorWriteLine(text);
            }

            printedWarningsCount++;
        }

        private static void PrintError(AssemblyError error)
        {
            var text = FormatAssemblyError(error, "ERROR");
            if(colorPrint) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.BackgroundColor = defaultBackgroundColor;
                ErrorWriteLine(text);
                Console.ForegroundColor = defaultForegroundColor;
            }
            else {
                ErrorWriteLine(text);
            }
        }

        private static void PrintFatal(AssemblyError error)
        {
            if(error.Code is AssemblyErrorCode.MaxErrorsReached) {
                PrintProgress("\r\n" + error.Message, 1);
            }
            else {
                PrintFatal(FormatAssemblyError(error, "FATAL"));
            }
        }

        private static void PrintFatal(string text)
        {
            if(colorPrint) {
                Console.ForegroundColor = ConsoleColor.White;
                Console.BackgroundColor = ConsoleColor.Red;
                ErrorWriteLine(text);
                Console.ForegroundColor = defaultForegroundColor;
                Console.BackgroundColor = defaultBackgroundColor;
            }
            else {
                ErrorWriteLine(text);
            }
            ErrorWriteLine();
        }

        private static void PrintAssemblyPrint(string text)
        {
            if(colorPrint) {
                Console.ForegroundColor = ConsoleColor.White;
                Console.BackgroundColor = defaultBackgroundColor;
                WriteLine(text);
                Console.ForegroundColor = defaultForegroundColor;
            }
            else {
                WriteLine(text);
            }
        }

        private static void PrintProgress(string text, int requiredVerbosity)
        {
            if(verbosityLevel < requiredVerbosity) {
                return;
            }

            if(colorPrint) {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.BackgroundColor = defaultBackgroundColor;
                WriteLine(text);
                Console.ForegroundColor = defaultForegroundColor;
            }
            else {
                WriteLine(text);
            }
        }

        private static void PrintDuration(string text)
        {
            if(colorPrint) {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.BackgroundColor = defaultBackgroundColor;
                WriteLine(text);
                Console.ForegroundColor = defaultForegroundColor;
            }
            else {
                WriteLine(text);
            }
        }

        static readonly Stack<string> PreviousCurrentFileDirectories = new();

        private static Stream GetStreamForInclude(string includeFilePath)
        {
            static Stream Process(string filePath)
            {
                PreviousCurrentFileDirectories.Push(currentFileDirectory);
                currentFileDirectory = Path.GetDirectoryName(filePath);
                return File.OpenRead(filePath);
            }

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), includeFilePath);
            if(File.Exists(filePath)) {
                return Process(filePath);
            }

            filePath = Path.Combine(currentFileDirectory, includeFilePath);
            if(File.Exists(filePath)) {
                return Process(filePath);
            }

            foreach(var directory in includeDirectories) {
                filePath = Path.Combine(directory, includeFilePath);
                if(File.Exists(filePath)) {
                    return Process(filePath);
                }
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