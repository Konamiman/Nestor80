using Konamiman.Nestor80.Assembler;
using Konamiman.Nestor80.Assembler.Errors;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Konamiman.Nestor80.N80
{

    /// <summary>
    /// This is the class that glues all the pieces together to turn Nestor80 into an executable application.
    /// In a nutshell what it does is:
    /// 
    /// 1. Parse and verify the arguments from all sources (command line, N80_args environment item, argument files).
    /// 2. Use the <see cref="AssemblySourceProcessor"/> class to process the source code, passing an instance
    ///    of <see cref="AssemblyConfiguration"/> populated from the arguments.
    /// 3. Use the <see cref="OutputGenerator"/> class to generate an absolute or relocatable file from
    ///    the <see cref="AssemblyResult"/> instance that resulted from the source file processing.
    /// 4. If requested via --listing argument, use the <see cref="ListingFileGenerator"/> class to generate a listing file
    ///    from the result of the source code processing, passing an instance of <see cref="ListingFileConfiguration"/>
    ///    populated from the arguments.
    ///    
    /// The events produced by <see cref="AssemblySourceProcessor"/> are captured in order to display messages
    /// (generated with .PRINT and similar) and errors as they are generated, and to process INCLUDE instructions.
    /// </summary>
    internal partial class Program
    {
        const int PREVIEW_LEVEL = 0;

        const int ERR_SUCCESS = 0;
        const int ERR_BAD_ARGUMENTS = 1;
        const int ERR_CANT_OPEN_INPUT_FILE = 2;
        const int ERR_CANT_CREATE_OUTPUT_FILE = 3;
        const int ERR_CANT_CREATE_LISTING_FILE = 4;
        const int ERR_ASSEMBLY_ERROR = 5;
        const int ERR_ASSEMBLY_FATAL = 6;

        const int OF_CASE_ORIGINAL = 0;
        const int OF_CASE_LOWER = 1;
        const int OF_CASE_UPPER = 2;

        const int DEFAULT_MAX_ERRORS = 34;

        const int MAX_INCBIN_SIZE_DOW = 1024 * 1024;
        const int MAX_INCBIN_SIZE_MEMMAP = 64 * 1024;

        static string inputFilePath = null;
        static string inputFileDirectory = null;
        static string inputFileName = null;
        static string outputFilePath = null;
        static bool generateOutputFile = true;
        static Encoding inputFileEncoding;
        static bool mustProcessOutputFileName;
        static bool colorPrint;
        static bool showBanner;
        static readonly List<string> includeDirectories = new();
        static bool directOutputWrite;
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
        static string outputFileExtension;
        static bool allowBareExpressions;
        static bool initDefs;
        static bool sourceInErrorMessage;
        static int outputFileCase;
        static bool allowRelativeLabels;
        static string listingFileName = null;
        static string listingFilePath = null;
        static Encoding listingFileEncoding;
        static bool mustGenerateListingFile;
        static string listingFileExtension;
        static bool link80compatibility;
        static bool discardHashPrefix;
        static bool acceptDottedInstructionAliases;
        static bool treatUnknownSymbolsAsExternals;
        static string endOfLIne;

        static readonly ConsoleColor defaultForegroundColor = Console.ForegroundColor;
        static readonly ConsoleColor defaultBackgroundColor = Console.BackgroundColor;
        static readonly Stopwatch assemblyTimeMeasurer = new();
        static readonly Stopwatch totalTimeMeasurer = new();
        static readonly List<(string, string[])> argsByFile = [];
        static bool n80FileUsed = false;
        static readonly List<AssemblyError> warningsFromPass1 = [];
        static bool inPass2 = false;
        static string[] envArgs = null;
        static string[] commandLineArgs;
        static string currentFileDirectory;
        static int printedWarningsCount = 0;

        static readonly ListingFileConfiguration listingConfig = new();

        static readonly Dictionary<string, string> endOfLines = new(StringComparer.OrdinalIgnoreCase) {
            { "auto", Environment.NewLine },
            { "cr", "\r" },
            { "lf", "\n" },
            { "crlf", "\r\n" }
        };

        static int Main(string[] args)
        {
            totalTimeMeasurer.Start();

            if(args.Length == 0) {
                WriteLine(bannerText);
                WriteLine(simpleHelpText);
                return ERR_SUCCESS;
            }

            if(args[0] is "-v" or "--version") {
                Console.Write(GetProgramVersion());
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
                Console.WriteLine(new string('-', longestNameLength + 1) + "|" + new string('-', 10));
                foreach(var encoding in encodings) {
                    Console.WriteLine(string.Format(formatString, encoding.Name, encoding.CodePage));
                }
                return ERR_SUCCESS;
            }

            if(args[0] is "-h" or "--help") {
                WriteLine(bannerText);
                if(args.Length > 1) {
                    ShowArgumentHelp(args[1]);
                }
                else {
                    WriteLine(simpleHelpText);
                    WriteLine(extendedHelpText);
                }
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
            listingConfig.TitleSignature = $"Nestor80\t{GetProgramVersion()}";

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
                PrintFatal($"Can't create output file{(outputFilePath is null ? "" : $" ({outputFilePath})")}: {outputFileErrorMessage}");
                return ERR_CANT_CREATE_OUTPUT_FILE;
            }

            if(mustGenerateListingFile) {
                string listingFileErrorMessage;
                try {
                    listingFileErrorMessage = ProcessListingFileArgument(listingFileName);
                }
                catch(Exception ex) {
                    listingFileErrorMessage = ex.Message;
                }

                if(listingFileErrorMessage is not null) {
                    PrintFatal($"Can't create listing file{(listingFilePath is null ? "" : $" ({listingFilePath})")}: {listingFileErrorMessage}");
                    return ERR_CANT_CREATE_LISTING_FILE;
                }
            }

            PrintProgress($"Input file: {inputFilePath}\r\n", 1);

            PrintArgumentsAndIncludeDirs();

            var errCode = DoAssembly(out int writtenBytes, out int warnCount, out int errCount, out int fatalCount, out int listingWrittenBytes);
            if(errCode is not ERR_SUCCESS and not ERR_CANT_CREATE_LISTING_FILE) {
                generateOutputFile = false;
            }

            if(errCode is ERR_SUCCESS or ERR_CANT_CREATE_LISTING_FILE) {
                if(warnCount == 0) {
                    PrintProgress("\r\nAssembly completed!", 1);
                }
                else if(warnCount == 1) {
                    PrintProgress("\r\nAssembly completed with 1 warning", 1);
                }
                else {
                    PrintProgress($"\r\nAssembly completed with {warnCount} warnings", 1);
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

            if(mustGenerateListingFile && errCode is ERR_SUCCESS) {
                PrintProgress($"\r\nListing file: {listingFilePath}", 1);
                PrintProgress($"{listingWrittenBytes} bytes written", 1);
            }

            totalTimeMeasurer.Stop();

            if(showAssemblyDuration) {
                WriteLine();
                PrintDuration($"Assembly time: {FormatTimespan(assemblyTimeMeasurer.Elapsed)}");
                PrintDuration($"Total time:    {FormatTimespan(totalTimeMeasurer.Elapsed)}");
            }

            return errCode;
        }

        static bool blankLinePrinted = false;

        /// <summary>
        /// Write a line of text to the console, avoiding writing two consecutive blank lines.
        /// </summary>
        /// <param name="text">The text to write</param>
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

        /// <summary>
        /// Write a line to the error output:
        /// If the error output is redirected write it as is, otherwise resort to <see cref="WriteLine(string)"/>.
        /// </summary>
        /// <param name="text">The text to write</param>
        static void ErrorWriteLine(string text = "")
        {
            if(Console.IsErrorRedirected) {
                Console.Error.WriteLine(text.Trim('\r', '\n'));
            }
            else {
                WriteLine(text);
            }
        }

        /// <summary>
        /// Generate the program arguments to use by merging the command line arguments,
        /// the contents of the N80_ARGS variable, and the .N80 arguments file if they exist.
        /// </summary>
        /// <param name="commandLineArgs"></param>
        /// <returns>The actual program arguments to use.</returns>
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

            //The arguments list could contain both --default-file-args and --no-default-file-args arguments.
            //The last one specified wins, so we need to check whether they are present or not AND in which order.
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

        /// <summary>
        /// Print the entire set of program arguments and include directories when in full verbosity mode.
        /// </summary>
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
                info += "\r\nExtra directories for INCLUDE and INCBIN:\r\n";
                foreach(var id in includeDirectories) {
                    info += "  " + id + "\r\n";
                }
            }
            else {
                info += "\r\nNo extra directories for INCLUDE and INCBIN\r\n";
            }

            if(symbolDefinitions.Count > 0) {
                var symbolDefinitionsAsStrings = symbolDefinitions.OrderBy(d => d.Item1).Select(d => $"{d.Item1}={d.Item2:X4}h").ToArray();
                info += $"\r\nSymbol definitions: {string.Join(", ", symbolDefinitionsAsStrings)}\r\n";
            }

            info += $"\r\nInput file encoding: {inputFileEncoding.WebName}\r\n";
            info += $"Color output: {YesOrNo(colorPrint)}\r\n";
            info += $"Show program banner: {YesOrNo(showBanner)}\r\n";
            info += $"Direct output write: {YesOrNo(directOutputWrite)}\r\n";
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
            info += $"Allow relative labels: {YesOrNo(allowRelativeLabels)}\r\n";
            info += $"Discard '#' prefix in expressions: {YesOrNo(discardHashPrefix)}\r\n";
            info += $"Accept '.' prefix in instructions: {YesOrNo(acceptDottedInstructionAliases)}\r\n";
            info += $"End of line sequence for text files: {endOfLIne.ToUpper()}\r\n";
            if(buildType != BuildType.Absolute) {
                info += $"LINK-80 compatibility: {YesOrNo(link80compatibility)}\r\n";
                info += $"Treat unknown symbols as external references: {YesOrNo(treatUnknownSymbolsAsExternals)}\r\n";
            }

            if(mustProcessOutputFileName) {
                var outputExtension =
                    outputFileExtension ?? buildType switch {
                        BuildType.Absolute => ".BIN",
                        BuildType.Relocatable => ".REL",
                        BuildType.Sdcc => ".REL",
                        _ => ".BIN or .REL"
                    };

                if(outputFileExtension is null && outputFileCase is OF_CASE_LOWER) {
                    outputExtension = outputExtension.ToLower();
                }

                info += $"Output file extension: {outputExtension}\r\n";

                var outputFileCaseString =
                    outputFileCase switch {
                        OF_CASE_LOWER => "lower",
                        OF_CASE_UPPER => "UPPER",
                        _ => "Original"
                    };
                info += $"Output file case: {outputFileCaseString}\r\n";
            }

            if(mustGenerateListingFile) {
                info += $"Listing file: {listingFilePath}\r\n";
                info += $"Listing file encoding: {listingFileEncoding.WebName}\r\n";
            }

            PrintProgress(info, 3);
        }

        private static string YesOrNo(bool what)
        {
            return what ? "YES" : "NO";
        }

        /// <summary>
        /// Split a string by spaces, taking in account that a space escaped with "\ "
        /// needs to be kept as is.
        /// </summary>
        /// <param name="argsString">The string to split.</param>
        /// <returns>The parts the string has been split in.</returns>
        private static string[] SplitWithEscapedSpaces(string argsString)
        {
            argsString = argsString.Replace(@"\ ", "\u0001");
            var parts = argsString.Split(" ", StringSplitOptions.RemoveEmptyEntries);
            return parts.Select(p => p.Replace("\u0001", " ")).ToArray();
        }

        /// <summary>
        /// Reset the configuration to default values, effectively forgetting
        /// any previous command line arguments processed.
        /// </summary>
        static void ResetConfig()
        {
            inputFileEncoding = Encoding.UTF8;
            mustProcessOutputFileName = false;
            colorPrint = true;
            showBanner = true;
            includeDirectories.Clear();
            directOutputWrite = false;
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
            outputFileCase = OF_CASE_ORIGINAL;
            allowRelativeLabels = false;
            listingFileName = null;
            listingFileEncoding = Encoding.UTF8;
            mustGenerateListingFile = false;
            listingFileExtension = null;
            link80compatibility = false;
            discardHashPrefix = false;
            acceptDottedInstructionAliases = false;
            treatUnknownSymbolsAsExternals = false;
            endOfLIne = "auto";

            listingConfig.MaxSymbolLength = 16;
            listingConfig.ListFalseConditionals = true;
            listingConfig.ListCode = true;
            listingConfig.ListSymbols = true;
            listingConfig.BytesPerRow = 4;
            listingConfig.MaxBytesPerInstruction = 128;
            listingConfig.SymbolsPerRow = 4;
            listingConfig.UppercaseSymbolNames = false;
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
            //The arguments list could contain both --show-banner and --no-show-banner arguments.
            //The last one specified wins, so we need to check whether they are present or not AND in which order.
            var indexOfLastShowBanner = args.Select((arg, index) => new { arg, index }).LastOrDefault(x => x.arg is "-sb" or "--show-banner")?.index ?? -1;
            var indexOfLastNoShowBanner = args.Select((arg, index) => new { arg, index }).LastOrDefault(x => x.arg is "-nsb" or "--no-show-banner")?.index ?? -1;
            showBanner = indexOfLastNoShowBanner == -1 || indexOfLastShowBanner > indexOfLastNoShowBanner;
        }

        /// <summary>
        /// Process the input file argument, taking in account that a relative path
        /// refers to the current directory.
        /// </summary>
        /// <param name="fileSpecification"></param>
        /// <returns>An error message, or null if no error.</returns>
        private static string ProcessInputFileArgument(string fileSpecification)
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

        /// <summary>
        /// Process the output file argument, taking in account the rules for resolving
        /// a missing argument, a relative path, "$" and "$/", and a path being
        /// a directory rather than a file.
        /// </summary>
        /// <param name="fileSpecification">The argument as supplied in the command line.</param>
        /// <returns>An error message, or null if no error.</returns>
        private static string ProcessOutputFileArgument(string fileSpecification)
        {
            //mustProcessOutputFileName is set to true whenever the output file name
            //needs to be automatically generated from the input file name.

            if(fileSpecification is null) {
                //No argument: use current directory and auto file name.
                outputFilePath = Path.Combine(Directory.GetCurrentDirectory(), inputFileName);
                mustProcessOutputFileName = true;
                return null;
            }

            if(fileSpecification is "$") {
                //"$": Use the input file directory and auto file name.
                fileSpecification = Path.Combine(inputFileDirectory, inputFileName);
                mustProcessOutputFileName = true;
            }
            else if(fileSpecification.StartsWith("$/")) {
                //"$/": Make it relative to the directory of the input file.
                fileSpecification = Path.Combine(inputFileDirectory, fileSpecification[2..]);
            }

            fileSpecification = Path.GetFullPath(fileSpecification);

            if(Directory.Exists(fileSpecification)) {
                //It's a directory: use it with auto file name.
                outputFilePath = Path.Combine(fileSpecification, inputFileName);
                mustProcessOutputFileName = true;
            }
            else if(Directory.Exists(Path.GetDirectoryName(fileSpecification))) {
                //It's a file: use it as is.
                outputFilePath = fileSpecification;
            }
            else {
                return "Directory not found";
            }

            return null;
        }

        /// <summary>
        /// Process the listing file argument. The processing rules are the same
        /// as for <see cref="ProcessOutputFileArgument(string)"/>.
        /// </summary>
        /// <param name="fileSpecification">The argument as supplied in the command line.</param>
        /// <returns>An error message, or null if no error.</returns>
        private static string ProcessListingFileArgument(string fileSpecification)
        {
            var isAutoFilename = false;

            if(fileSpecification is null) {
                fileSpecification = Path.Combine(Directory.GetCurrentDirectory(), inputFileName);
                isAutoFilename = true;
            }
            else if(fileSpecification is "$") {
                fileSpecification = Path.Combine(inputFileDirectory, inputFileName);
                isAutoFilename = true;
            }
            else if(fileSpecification.StartsWith("$/")) {
                fileSpecification = Path.Combine(inputFileDirectory, fileSpecification[2..]);
            }

            fileSpecification = Path.GetFullPath(fileSpecification);

            if(Directory.Exists(fileSpecification)) {
                listingFilePath = Path.Combine(fileSpecification, inputFileName);
                isAutoFilename = true;
            }
            else if(Directory.Exists(Path.GetDirectoryName(fileSpecification))) {
                listingFilePath = fileSpecification;
            }
            else {
                return "Directory not found";
            }

            //File extension is processed here because contrary to the output file path,
            //there's no further processing of this argument.
            if(isAutoFilename) {
                if(outputFileCase is OF_CASE_ORIGINAL) {
                    listingFilePath = Path.ChangeExtension(listingFilePath, outputFileExtension ?? ".LST");
                }
                else {
                    if(listingFileExtension is null) {
                        listingFilePath = Path.ChangeExtension(listingFilePath, ".LST");
                    }

                    var directoryName = Path.GetDirectoryName(listingFilePath);
                    var fileName = Path.GetFileName(listingFilePath);
                    fileName = outputFileCase is OF_CASE_LOWER ? fileName.ToLower() : fileName.ToUpper();
                    listingFilePath = Path.Combine(directoryName ?? "", fileName);

                    if(listingFileExtension is not null) {
                        listingFilePath = Path.ChangeExtension(listingFilePath, listingFileExtension);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Process arguments coming from the command line or from an arguments file.
        /// </summary>
        /// <param name="args">The arguments to process.</param>
        /// <param name="fromFile">True if the arguments come from a file, false if they come from the command line.</param>
        /// <returns>An error message or null if no error.</returns>
        private static string ProcessArguments(string[] args, bool fromFile = false)
        {
            for(int i=0; i<args.Length; i++) {
                var arg = args[i];
                if(arg is "-no" or "--no-output") {
                    generateOutputFile = false;
                }
                else if(arg is "-do" or "--do-output") {
                    generateOutputFile = true;
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
                else if(arg is "-dow" or "--direct-output-write") {
                    directOutputWrite = true;
                    buildType = BuildType.Absolute;
                }
                else if(arg is "-ndow" or "--no-direct-output-write") {
                    directOutputWrite = false;
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
                else if(arg is "-vb" or "--verbosity") {
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
                        return $"The {arg} argument needs to be followed by the build type (abs, rel, sdcc or auto)";
                    }
                    i++;
                    var buildTypeString = args[i];

                    buildType = buildTypeString switch {
                        "abs" => BuildType.Absolute,
                        "rel" => BuildType.Relocatable,
                        "sdcc" => BuildType.Sdcc,
                        "auto" => BuildType.Automatic,
                        _ => BuildType.Invalid
                    };

                    if(buildType is BuildType.Invalid) {
                        return $"{arg}: the build type must be one of: abs, rel, sdcc, auto";
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
                else if(arg is "-nfe" or "--no-output-file-extension") {
                    outputFileExtension = null;
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
                else if(arg is "-ofc" or "--output-file-case") {
                    if(i == args.Length - 1 || args[i + 1][0] == '-') {
                        return $"The {arg} argument needs to be followed by the output file case type (lower, upper or orig)";
                    }
                    i++;
                    var outputFileCaseString = args[i];

                    outputFileCase = outputFileCaseString[0] switch {
                        'l' or 'L' => OF_CASE_LOWER,
                        'u' or 'U' => OF_CASE_UPPER,
                        'o' or 'O' => OF_CASE_ORIGINAL,
                        _ => -1
                    };

                    if(outputFileCase is -1) {
                        return $"{arg}: the output file case type must be one of: lower, upper, orig";
                    }
                }
                else if(arg is "-arl" or "--allow-relative-labels") {
                    allowRelativeLabels = true;
                }
                else if(arg is "-narl" or "--no-allow-relative-labels") {
                    allowRelativeLabels = false;
                }
                else if(arg is "-l" or "--listing-file") {
                    mustGenerateListingFile = true;
                    if(i == args.Length - 1 || args[i + 1][0] == '-') {
                        listingFileName = null;
                    }
                    else {
                        listingFileName = args[i + 1];
                        i++;
                    }
                }
                else if(arg is "-nl" or "--no-listing-file") {
                    mustGenerateListingFile = false;
                    listingFileName = null;
                }
                else if(arg is "-le" or "--listing-file-encoding") {
                    if(i == args.Length - 1 || args[i + 1][0] == '-') {
                        return $"The {arg} argument needs to be followed by an encoding page or name";
                    }
                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                    var listingFileEncodingName = args[i + 1];
                    try {
                        if(int.TryParse(listingFileEncodingName, out int encodingPage)) {
                            listingFileEncoding = Encoding.GetEncoding(encodingPage);
                        }
                        else {
                            listingFileEncoding = Encoding.GetEncoding(listingFileEncodingName);
                        }
                    }
                    catch {
                        return $"Unknown source file encoding '{listingFileEncodingName}'";
                    }
                    i++;
                }
                else if(arg is "-lx" or "--listing-file-extension") {
                    if(i == args.Length - 1 || args[i + 1][0] == '-') {
                        return $"The {arg} argument needs to be followed by a file extension";
                    }
                    i++;
                    listingFileExtension = args[i];
                }
                else if(arg is "-nlx" or "--no-listing-file-extension") {
                    listingFileExtension = null;
                }
                else if(arg is "-lspl" or "--listing-symbols-per-line") {
                    if(i == args.Length - 1 || args[i + 1][0] == '-') {
                        return $"The {arg} argument needs to be followed by a number";
                    }

                    if(int.TryParse(args[i + 1], out int symbolsPerLine)) {
                        listingConfig.SymbolsPerRow = Math.Clamp(symbolsPerLine, 1, 256);
                    }
                    else {
                        return $"The {arg} argument needs to be followed by a number";
                    }
                    i++;
                }
                else if(arg is "-lmsl" or "--listing-max-symbol-length") {
                    if(i == args.Length - 1 || args[i + 1][0] == '-') {
                        return $"The {arg} argument needs to be followed by a number";
                    }

                    if(int.TryParse(args[i + 1], out int maxSymbolLength)) {
                        listingConfig.MaxSymbolLength = Math.Clamp(maxSymbolLength, 4, 256);
                    }
                    else {
                        return $"The {arg} argument needs to be followed by a number";
                    }
                    i++;
                }
                else if(arg is "-lic" or "--listing-include-code") {
                    listingConfig.ListCode = true;
                }
                else if(arg is "-nlic" or "--no-listing-include-code") {
                    listingConfig.ListCode = false;
                }
                else if(arg is "-lis" or "--listing-include-symbols") {
                    listingConfig.ListSymbols = true;
                }
                else if(arg is "-nlis" or "--no-listing-include-symbols") {
                    listingConfig.ListSymbols = false;
                }
                else if(arg is "-lfc" or "--listing-false-conditionals") {
                    listingConfig.ListFalseConditionals = true;
                }
                else if(arg is "-nlfc" or "--no-listing-false-conditionals") {
                    listingConfig.ListFalseConditionals = false;
                }
                else if(arg is "-lbpl" or "--listing-bytes-per-line") {
                    if(i == args.Length - 1 || args[i + 1][0] == '-') {
                        return $"The {arg} argument needs to be followed by a number";
                    }

                    if(int.TryParse(args[i + 1], out int bytesPerLine)) {
                        listingConfig.BytesPerRow = Math.Clamp(bytesPerLine, 2, 256);
                    }
                    else {
                        return $"The {arg} argument needs to be followed by a number";
                    }
                    i++;
                }
                else if(arg is "-lmbi" or "--listing-max-bytes-per-instruction") {
                    if(i == args.Length - 1 || args[i + 1][0] == '-') {
                        return $"The {arg} argument needs to be followed by a number";
                    }

                    if(int.TryParse(args[i + 1], out int maxBytesPerInstruction)) {
                        listingConfig.MaxBytesPerInstruction = Math.Clamp(maxBytesPerInstruction, 1, 65536);
                    }
                    else {
                        return $"The {arg} argument needs to be followed by a number";
                    }
                    i++;
                }
                else if(arg is "-lus" or "--listing-uppercase-symbols") {
                    listingConfig.UppercaseSymbolNames = true;
                }
                else if(arg is "-nlus" or "--no-listing-uppercase-symbols") {
                    listingConfig.UppercaseSymbolNames = false;
                }
                else if(arg is "-l8c" or "--link-80-compatibility") {
                    link80compatibility = true;
                }
                else if(arg is "-nl8c" or "--no-link-80-compatibility") {
                    link80compatibility = false;
                }
                else if(arg is "-dhp" or "--discard-hash-prefix") {
                    discardHashPrefix = true;
                }
                else if(arg is "-ndhp" or "--no-discard-hash-prefix") {
                    discardHashPrefix = false;
                }
                else if(arg is "-adp" or "--accept-dot-prefix") {
                    acceptDottedInstructionAliases = true;
                }
                else if(arg is "-nadp" or "-no-accept-dot-prefix") {
                    acceptDottedInstructionAliases = false;
                }
                else if(arg is "-use" or "--unknown-symbols-external") {
                    treatUnknownSymbolsAsExternals = true;
                }
                else if(arg is "-nuse" or "--no-unknown-symbols-external") {
                    treatUnknownSymbolsAsExternals = false;
                }
                else if(arg is "-eol" or "--end-of-line") {
                    if(i == args.Length - 1 || args[i + 1][0] == '-') {
                        return $"The {arg} argument needs to be followed by the end of line sequence (cr, lf, crlf or auto)";
                    }

                    if(endOfLines.ContainsKey(args[i + 1])) {
                        endOfLIne = args[i + 1];
                    }
                    else {
                        return $"{arg}: the end of line sequence must be one of: cr, lf, crlf, auto";
                    }
                    i++;
                }
                else {
                    return $"Unknwon argument '{arg}'";
                }
            }

            return null;
        }

        /// <summary>
        /// Process arguments coming from a file, taking in account
        /// that a relative path refers to the current directory
        /// and "$/" refers to the directory of the source file.
        /// </summary>
        /// <param name="fileName">The file path.</param>
        /// <returns>An error message, or null if no error.</returns>
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

        /// <summary>
        /// Do the actual assembly process after all the arguments have been processed.
        /// </summary>
        /// <param name="writtenBytes">Returns the size of the generated output file.</param>
        /// <param name="warnCount">Returns the count of warnings generated.</param>
        /// <param name="errCount">Returns the count of normal errors generated.</param>
        /// <param name="fatalCount">Returns the count of fatal errors generated.</param>
        /// <param name="listingWrittenBytes">Returns the count of the generated listing file.</param>
        /// <returns>An error code to be returned by the program.</returns>
        private static int DoAssembly(out int writtenBytes, out int warnCount, out int errCount, out int fatalCount, out int listingWrittenBytes)
        {
            Stream inputStream;
            writtenBytes = warnCount = errCount = fatalCount = listingWrittenBytes = 0;

            // Step 1: Process source

            try {
                inputStream = File.OpenRead(inputFilePath);
            }
            catch(Exception ex) {
                PrintFatal($"Can't open input file: {ex.Message}");
                return ERR_CANT_OPEN_INPUT_FILE;
            }

            AssemblySourceProcessor.AssemblyErrorGenerated += AssemblySourceProcessor_AssemblyErrorGenerated;
            AssemblySourceProcessor.BuildTypeAutomaticallySelected += AssemblySourceProcessor_BuildTypeAutomaticallySelected;
            AssemblySourceProcessor.Pass2Started += AssemblySourceProcessor_Pass2Started;
            AssemblySourceProcessor.IncludedFileFinished += AssemblySourceProcessor_IncludedFileFinished;

            if(!silenceAssemblyPrints) {
                AssemblySourceProcessor.PrintMessage += AssemblySourceProcessor_PrintMessage;
            }

            var config = new AssemblyConfiguration() {
                OutputStringEncoding = stringEncoding,
                AllowEscapesInStrings = stringEscapes,
                BuildType = buildType,
                GetStreamForInclude = name => GetStreamForInclude(name, false),
                GetStreamForIncbin = name => GetStreamForInclude(name, true),
                PredefinedSymbols = symbolDefinitions.ToArray(),
                MaxErrors = maxErrors,
                CpuName = defaultCpu,
                AllowBareExpressions = allowBareExpressions,
                AllowRelativeLabels = allowRelativeLabels,
                MaxIncbinFileSize = (buildType is BuildType.Absolute && directOutputWrite) ? MAX_INCBIN_SIZE_DOW : MAX_INCBIN_SIZE_MEMMAP,
                Link80Compatibility = link80compatibility,
                DiscardHashPrefix = discardHashPrefix,
                AcceptDottedInstructionAliases = acceptDottedInstructionAliases,
                TreatUnknownSymbolsAsExternals = treatUnknownSymbolsAsExternals,
                EndOfLine = endOfLines[endOfLIne]
            };

            if(showAssemblyDuration) assemblyTimeMeasurer.Start();
            var result = AssemblySourceProcessor.Assemble(inputStream, inputFileEncoding, config);
            if(showAssemblyDuration) assemblyTimeMeasurer.Stop();

            warnCount = printedWarningsCount;
            errCount = result.Errors.Count(e => !e.IsWarning && !e.IsFatal);
            fatalCount = result.Errors.Count(e => e.IsFatal);

            if(result.HasFatalErrors) {
                return ERR_ASSEMBLY_FATAL;
            }
            else if(result.HasNonFatalErrors) {
                return ERR_ASSEMBLY_ERROR;
            }

            if(mustProcessOutputFileName) {
                var defaultExtension = result.BuildType is BuildType.Absolute ? ".BIN" : ".REL";

                if(outputFileCase is OF_CASE_ORIGINAL) {
                    outputFilePath = Path.ChangeExtension(outputFilePath, outputFileExtension ?? defaultExtension);
                } else { 
                    if(outputFileExtension is null) {
                        outputFilePath = Path.ChangeExtension(outputFilePath, defaultExtension);
                    }

                    var directoryName = Path.GetDirectoryName(outputFilePath);
                    var fileName = Path.GetFileName(outputFilePath);
                    fileName = outputFileCase is OF_CASE_LOWER ? fileName.ToLower() : fileName.ToUpper();
                    outputFilePath = Path.Combine(directoryName ?? "", fileName);

                    if(outputFileExtension is not null) {
                        outputFilePath = Path.ChangeExtension(outputFilePath, outputFileExtension);
                    }
                }
            }

            // Step 2: generate output, only if step 1 didn't generate errors AND the --no-output argument wasn't specified.

            if(generateOutputFile) {
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
                        link80compatibility && inputFileNameNoExt.Length > AssemblySourceProcessor.MaxEffectiveExternalNameLength ?
                        inputFileNameNoExt[..AssemblySourceProcessor.MaxEffectiveExternalNameLength] :
                        inputFileNameNoExt;
                    result.ImplicitProgramName = true;
                }

                try {
                    writtenBytes =
                        result.BuildType is BuildType.Absolute ?
                        OutputGenerator.GenerateAbsolute(result, outputStream, directOutputWrite) :
                        result.BuildType is BuildType.Relocatable ?
                        OutputGenerator.GenerateRelocatable(result, outputStream, initDefs, !link80compatibility) :
                        OutputGenerator.GenerateSdccRelocatable(result, outputStream, endOfLines[endOfLIne]);
                }
                catch(Exception ex) {
                    PrintFatal($"Can't write to output file ({outputFilePath}): {ex.Message}");
#if DEBUG
                    throw;
#else
                    return ERR_CANT_CREATE_OUTPUT_FILE;
#endif
                }

                outputStream.Close();
            }

            // Step 3: generate listing file, if requested via --listing argument.

            if(mustGenerateListingFile) {
                try {
                    var listingStream = File.Create(listingFilePath);
                    var listingStreamWriter = new StreamWriter(listingStream, listingFileEncoding) { NewLine = config.EndOfLine };
                    listingWrittenBytes = ListingFileGenerator.GenerateListingFile(result, listingStreamWriter, listingConfig);
                    listingStream.Close();
                }
                catch(Exception ex) {
                    PrintFatal($"Can't create listing file{(listingFilePath is null ? "" : $" ({listingFilePath})")}: {ex.Message}");
#if DEBUG
                    throw;
#else
                    return ERR_CANT_CREATE_LISTING_FILE;
#endif
                }
            }

            return ERR_SUCCESS;
        }

        /// <summary>
        /// Handler for the <see cref="AssemblySourceProcessor.IncludedFileFinished"/> event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void AssemblySourceProcessor_IncludedFileFinished(object sender, EventArgs e)
        {
            currentFileDirectory = PreviousCurrentFileDirectories.Pop();
        }

        /// <summary>
        /// Handler for the <see cref="AssemblySourceProcessor.Pass2Started"/> event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void AssemblySourceProcessor_Pass2Started(object sender, EventArgs e)
        {
            inPass2 = true;
            PrintProgress($"\r\nPass 2 started\r\n", 2);
        }

        /// <summary>
        /// Handler for the <see cref="AssemblySourceProcessor.BuildTypeAutomaticallySelected"/> event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e">File name and line number in which the decision was made, and build type chosen.</param>
        private static void AssemblySourceProcessor_BuildTypeAutomaticallySelected(object sender, (string, int, BuildType) e)
        {
            var fileName = e.Item1 is null ? "" : $"[{e.Item1}]: ";
            PrintProgress($"\r\n{fileName}Line {e.Item2}: Build type automatically selected: {e.Item3}", 2);
        }

        /// <summary>
        /// Format an assembly error for printing in the console.
        /// </summary>
        /// <param name="error">The error to format.</param>
        /// <param name="prefix">The error type prefix to use.</param>
        /// <returns>Formatted error message.</returns>
        private static string FormatAssemblyError(AssemblyError error, string prefix)
        {
            var fileName = error.IncludeFileName is null ? "" : $"[{error.IncludeFileName}] ";
            var macroInfo = error.IsMacroLine ? $"<{string.Join(" --> ", error.MacroNamesAndLines.Select(nl => $"{nl.Item1}:{nl.Item2}").ToArray())}> " : "";
            var lineNumber = error.LineNumber is null ? "" : $"in line {error.LineNumber}: ";
            var errorCode = verbosityLevel >= 2 ? $"({(int)error.Code}) " : "";

            string lineText = null;
            if(sourceInErrorMessage && !string.IsNullOrWhiteSpace(prefix) && !string.IsNullOrWhiteSpace(error.SourceLineText)) {
                lineText = $"{new string(' ', prefix.Length+2)}{error.SourceLineText}\r\n";
            }

            return $"\r\n{prefix}: {errorCode}{fileName}{macroInfo}{lineNumber}{error.Message}\r\n{lineText}";
        }

        /// <summary>
        /// Handler for the <see cref="AssemblySourceProcessor.PrintMessage"/> event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void AssemblySourceProcessor_PrintMessage(object sender, string e)
        {
            PrintAssemblyPrint(e);
        }

        /// <summary>
        /// Handler for the <see cref="AssemblySourceProcessor.AssemblyErrorGenerated"/> event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e">The error that has been generated.</param>
        private static void AssemblySourceProcessor_AssemblyErrorGenerated(object sender, AssemblyError error)
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

        /// <summary>
        /// Print a generated warning if it's not configured as ignored
        /// and if either is not a duplicate or verbosity level is at least 2.
        /// </summary>
        /// <param name="error">The warning to print.</param>
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

        /// <summary>
        /// Print a text resulting from a .PRINT or similar statement in source code.
        /// </summary>
        /// <param name="text"></param>
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

        /// <summary>
        /// Callback passed to <see cref="AssemblySourceProcessor"/> to resolve INCLUDE and INCBIN instructions.
        /// 
        /// Relative files are searched in this order: current directory, the directory of the currently
        /// INCLUDEd file (or the input file if none), extra include directories in the order
        /// they were specified.
        /// 
        /// <see cref="PreviousCurrentFileDirectories"/> is a stack used to keep track of the directories 
        /// for the nested INCLUDEd files.
        /// </summary>
        /// <param name="includeFilePath">The argument of the INCLUDE/INCBIN instruction.</param>
        /// <param name="isIncbin">True for INCBIN instruction, false for INCLUDE instruction.</param>
        /// <returns>An open stream for the INCLUDEd file, or null if file is not found.</returns>
        private static Stream GetStreamForInclude(string includeFilePath, bool isIncbin)
        {
            Stream Process(string filePath)
            {
                if(!isIncbin) {
                    PreviousCurrentFileDirectories.Push(currentFileDirectory);
                    currentFileDirectory = Path.GetDirectoryName(filePath);
                }
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

        private static string GetProgramVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            while(version.EndsWith(".0") && version.Count(ch => ch is '.') > 1) {
                version = version[..^2];
            }
            return PREVIEW_LEVEL == 0 ? version : version + " preview " + PREVIEW_LEVEL;
        }

        private static void ShowArgumentHelp(string argument)
        {
            var lines = extendedHelpText.Split(Environment.NewLine);
            argument = argument.TrimStart('-');

            int i;
            for(i = 0; i < lines.Length; i++) {
                if(Regex.IsMatch(lines[i], $"^(-{argument},|-[^,]+, --{argument}(?![a-z-]))")) {
                    break;
                }
            }

            if(i == lines.Length) {
                WriteLine($"*** Argument '{argument}' not found.");
                return;
            }

            var result = lines[i++] + Environment.NewLine;
            while(i < lines.Length && (lines[i].StartsWith(' ') || string.IsNullOrWhiteSpace(lines[i]))) {
                result += lines[i++] + Environment.NewLine;
            }

            WriteLine(result.Trim());
        }
    }
}