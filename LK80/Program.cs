using Konamiman.Nestor80.Linker;
using Konamiman.Nestor80.Linker.Parsing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;

namespace Konamiman.Nestor80.LK80
{
    internal partial class Program
    {
        const int ERR_SUCCESS = 0;
        const int ERR_BAD_ARGUMENTS = 1;
        const int ERR_CANT_OPEN_INPUT_FILE = 2;
        const int ERR_CANT_CREATE_OUTPUT_FILE = 3;
        const int ERR_CANT_CREATE_LISTING_FILE = 4;
        const int ERR_LINKING_ERROR = 5;
        const int ERR_LINKING_FATAL = 6;

        const int OF_CASE_ORIGINAL = 0;
        const int OF_CASE_LOWER = 1;
        const int OF_CASE_UPPER = 2;

        static bool colorPrint;
        static bool showBanner;
        static byte fillByte;
        static ushort startAddress;
        static ushort endAddress;
        static bool suppressWarnings;
        static ushort maxErrors;
        static string workingDir;
        static string libraryDir;
        static int outputFileCase;
        static string outputFileExtension;
        static bool hexformat;
        static string outputFileName;
        static int verbosityLevel;
        static readonly List<ILinkingSequenceItem> linkingSequence = new();

        static string[] commandLineArgs;
        static string[] envArgs = null;
        static readonly List<(string, string[])> argsByFile = new();
        static string outputFilePath;

        static readonly ConsoleColor defaultForegroundColor = Console.ForegroundColor;
        static readonly ConsoleColor defaultBackgroundColor = Console.BackgroundColor;

        static int Main(string[] args)
        {
            if(args.Length == 0) {
                WriteLine(bannerText);
                WriteLine(simpleHelpText);
                return ERR_SUCCESS;
            }

            if(args[0] is "-v" or "--version") {
                Console.Write(GetProgramVersion());
                return ERR_SUCCESS;
            }

            if(args[0] is "-h" or "--help") {
                WriteLine(bannerText);
                WriteLine(simpleHelpText);
                WriteLine(extendedHelpText);
                return ERR_SUCCESS;
            }

            ResetConfig();

            commandLineArgs = args;
            args = MaybeMergeArgsWithEnv(args);
            SetShowBannerFlag(args);

            if(showBanner) WriteLine(bannerText);

            var argsErrorMessage = ProcessArguments(args);
            if(argsErrorMessage is not null) {
                ErrorWriteLine($"Invalid arguments: {argsErrorMessage}");
                return ERR_BAD_ARGUMENTS;
            }

            try {
                workingDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), workingDir ?? ""));
            }
            catch(Exception ex) {
                ErrorWriteLine($"Error when resolving working directory: {ex.Message}");
                return ERR_BAD_ARGUMENTS;
            }
            if(!Directory.Exists(workingDir)) {
                ErrorWriteLine($"Error when resolving working directory: '{workingDir}' doesn't exist or is not a directory");
                return ERR_BAD_ARGUMENTS;
            }

            try {
                libraryDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), libraryDir ?? workingDir ?? ""));
            }
            catch(Exception ex) {
                ErrorWriteLine($"Error when resolving libraries directory': {ex.Message}");
                return ERR_BAD_ARGUMENTS;
            }
            if(!Directory.Exists(workingDir)) {
                ErrorWriteLine($"Error when resolving libraries directory: '{workingDir}' doesn't exist or is not a directory");
                return ERR_BAD_ARGUMENTS;
            }

            var filesToProcess = linkingSequence.OfType<RelocatableFileReference>();
            foreach(var file in filesToProcess) {
                file.FullName = Path.Combine(workingDir, file.FullName);
                file.DisplayName = Path.GetFileName(file.FullName);
            }

            PrintArgumentsAndFiles();

            if(!filesToProcess.Any()) {
                PrintProgress("No files to process.", 1);
                PrintProgress("No output generated.", 1);
                return ERR_SUCCESS;
            }

            var firstFilePath = filesToProcess.First().FullName;
            if(outputFileName is null) {
                var firstFileName = Path.GetFileNameWithoutExtension(firstFilePath);
                var extension = outputFileExtension ?? ChangeCase(hexformat ? "HEX" : "BIN");
                firstFileName = Path.ChangeExtension(firstFileName, extension);

                outputFilePath = Path.Combine(workingDir, Path.GetDirectoryName(firstFilePath) ?? "", firstFileName);
            }
            else {
                outputFilePath = Path.Combine(workingDir, outputFileName);
            }

            if(Directory.Exists(outputFilePath)) {
                outputFileName = ChangeCase(Path.GetFileName(firstFilePath));
                if(!Path.HasExtension(outputFileName)) {
                    var extension = outputFileExtension ?? ChangeCase(hexformat ? "HEX" : "BIN");
                    outputFileName = Path.ChangeExtension(outputFileName, extension);
                }
                outputFilePath = Path.Combine(outputFilePath, outputFileName);
            }

            outputFilePath = Path.GetFullPath(outputFilePath);

            if(outputFilePath == firstFilePath) {
                PrintError($"Automatically generated output file path is the same as the first input file: {outputFilePath}. Please supply an output file path with --output-file.");
                return ERR_BAD_ARGUMENTS;
            }

            if(verbosityLevel >= 1) {
                RelocatableFilesProcessor.FileProcessingStart += RelocatableFilesProcessor_FileProcessingStart;
            }

            if(!suppressWarnings) {
                RelocatableFilesProcessor.LinkWarning += RelocatableFilesProcessor_LinkWarning;
            }

            RelocatableFilesProcessor.LinkError += RelocatableFilesProcessor_LinkError;

            var config = new LinkingConfiguration() {
                StartAddress = startAddress,
                EndAddress = endAddress,
                FillingByte = fillByte,
                LinkingSequenceItems = linkingSequence.ToArray(),
                GetFullNameOfRequestedLibraryFile = GetFullNameOfRequestedLibraryFile,
                OpenFile = OpenFile,
                MaxErrors = maxErrors
            };

            Stream outputStream;
            try {
                outputStream = File.Create(outputFilePath);
            }
            catch(Exception ex) {
                PrintError($"Can't create output file {outputFilePath}: {ex.Message}");
                return ERR_CANT_CREATE_OUTPUT_FILE;
            }

            LinkingResult linkingResult = null;
            try {
                linkingResult = RelocatableFilesProcessor.Link(config, outputStream);
            }
            catch(CantOpenFileException ex) {
                PrintError($"Can't open file {ex.FilePath}: {ex.Message}");
                return ERR_CANT_OPEN_INPUT_FILE;
            }
            catch(Exception ex) {
                PrintFatal($"Unexpected error: {ex.Message}");

#if DEBUG
                ErrorWriteLine(ex.StackTrace.ToString());
#endif
                return ERR_LINKING_FATAL;
            }

            try {
                outputStream.Close();
            }
            catch {
            }

            WriteLine();
            if(linkingResult.Errors.Length == 0) {
                PrintProgress("Success!", 1);
                PrintProgress($"Output file: {outputFilePath}", 1);
                return ERR_SUCCESS;
            }
            else {
                File.Delete(outputFilePath);
                PrintProgress("Not output file generated", 1);
                return ERR_LINKING_ERROR;
            }
        }

        private static string GetFullNameOfRequestedLibraryFile(string file)
        {
            var fullName = Path.Combine(libraryDir, file);
            fullName = Path.GetFullPath(fullName);
            if(!Path.HasExtension(fullName)) {
                fullName = Path.ChangeExtension(fullName, ".REL");
            }
            return fullName;
        }

        private static Stream OpenFile(string file)
        {
            if(!File.Exists(file)) {
                throw new CantOpenFileException(file, "File not found");
            }

            try {
                return File.OpenRead(file);
            }
            catch(Exception ex) {
                throw new CantOpenFileException(file, ex.Message);
            }
        }

        static int errorsCount = 0;

        private static void RelocatableFilesProcessor_LinkError(object sender, string e)
        {
            PrintError(e);
        }

        private static void RelocatableFilesProcessor_LinkWarning(object sender, string e)
        {
            PrintWarning(e);
        }

        private static void RelocatableFilesProcessor_FileProcessingStart(object sender, RelocatableFileReference e)
        {
            if(errorsPrintedForCurrentProgram) {
                WriteLine();
            }
            errorsPrintedForCurrentProgram = false;
            PrintLinkingProgress($"Processing file: {(verbosityLevel < 3 ? e.DisplayName : e.FullName)}");
        }

        private static string ChangeCase(string filename) =>
            outputFileCase switch {
                OF_CASE_UPPER => filename.ToUpper(),
                OF_CASE_LOWER => filename.ToLower(),
                _ => filename
            };

        private static string ProcessArguments(string[] args, bool fromFile = false)
        {
            string error;

            for(int i = 0; i < args.Length; i++) {
                var arg = args[i];

                if(arg is "-co" or "--color-output") {
                    colorPrint = true;
                }
                else if(arg is "-nco" or "--no-color-output") {
                    colorPrint = false;
                }
                else if(arg is "-f" or "--fill") {
                    if(i == args.Length - 1 || args[i + 1][0] == '-') {
                        return $"The {arg} argument needs to be followed by a value";
                    }
                    else {
                        i++;
                        fillByte = (byte)ParseNumericArg(arg, args[i], true, out error);
                        if(error is not null) return error;
                    }
                }
                else if(arg is "-s" or "--start") {
                    if(i == args.Length - 1 || args[i + 1][0] == '-') {
                        return $"The {arg} argument needs to be followed by a value";
                    }
                    else {
                        i++;
                        startAddress = (byte)ParseNumericArg(arg, args[i], false, out error);
                        if(error is not null) return error;
                    }
                }
                else if(arg is "-e" or "--end") {
                    if(i == args.Length - 1 || args[i + 1][0] == '-') {
                        return $"The {arg} argument needs to be followed by a value";
                    }
                    else {
                        i++;
                        endAddress = (byte)ParseNumericArg(arg, args[i], false, out error);
                        if(error is not null) return error;
                    }
                }
                else if(arg is "-sw" or "--suppress-warnings") {
                    suppressWarnings = true;
                }
                else if(arg is "-nsw" or "--no-suppress-warnings") {
                    suppressWarnings = false;
                }
                else if(arg is "-me" or "--max-errors") {
                    if(i == args.Length - 1 || args[i + 1][0] == '-') {
                        return $"The {arg} argument needs to be followed by a value";
                    }
                    else {
                        i++;
                        maxErrors = (byte)ParseNumericArg(arg, args[i], false, out error);
                        if(error is not null) return error;
                    }
                }
                else if(arg is "-w" or "--working-dir") {
                    if(i == args.Length - 1 || args[i + 1][0] == '-') {
                        return $"The {arg} argument needs to be followed by a value";
                    }
                    else {
                        i++;
                        workingDir = args[i];
                    }
                }
                else if(arg is "-ld" or "--library-dir") {
                    if(i == args.Length - 1 || args[i + 1][0] == '-') {
                        return $"The {arg} argument needs to be followed by a value";
                    }
                    else {
                        i++;
                        libraryDir = args[i];
                    }
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
                else if(arg is "-ofe" or "--output-file-extension") {
                    if(i == args.Length - 1 || args[i + 1][0] == '-') {
                        return $"The {arg} argument needs to be followed by a file extension";
                    }

                    i++;
                    outputFileExtension = args[i];
                }
                else if(arg is "-of" or "--output-format") {
                    if(i == args.Length - 1 || args[i + 1][0] == '-') {
                        return $"The {arg} argument needs to be followed by 'bin' or 'hex'";
                    }

                    i++;
                    var outputFormat = args[i];
                    if(outputFormat == "bin") {
                        hexformat = false;
                    }
                    else if(outputFormat == "hex") {
                        hexformat = true;
                    }
                    else {
                        return $"The {arg} argument needs to be followed by 'bin' or 'hex'";
                    }
                }
                else if(arg is "-o" or "--output-file") {
                    if(i == args.Length - 1 || args[i + 1][0] == '-') {
                        return $"The {arg} argument needs to be followed by a file name or path";
                    }

                    i++;
                    outputFileName = args[i];
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
                else if(arg is "-c" or "--code") {
                    if(i == args.Length - 1 || args[i + 1][0] == '-') {
                        return $"The {arg} argument needs to be followed by a value";
                    }
                    else {
                        i++;
                        var address = (byte)ParseNumericArg(arg, args[i], false, out error);
                        if(error is not null) return error;
                        linkingSequence.Add(new SetCodeSegmentAddress() { Address = address });
                    }
                }
                else if(arg is "-d" or "--data") {
                    if(i == args.Length - 1 || args[i + 1][0] == '-') {
                        return $"The {arg} argument needs to be followed by a value";
                    }
                    else {
                        i++;
                        var address = (byte)ParseNumericArg(arg, args[i], false, out error);
                        if(error is not null) return error;
                        linkingSequence.Add(new SetDataSegmentAddress() { Address = address });
                    }
                }
                else if(arg is "-cbd" or "--code-before-data") {
                    linkingSequence.Add(new SetCodeBeforeDataMode());
                }
                else if(arg is "-dbc" or "--data-before-code") {
                    linkingSequence.Add(new SetDataBeforeCodeMode());
                }
                else if(arg is "-rc" or "--reset-config") {
                    ResetConfig();
                }
                else if(arg is "-sb" or "--show-banner" or "-nsb" or "--no-show-banner" or "-nea" or "--no-env-args" or "-nfa" or "--no-file-args") {
                    //already handled
                }
                else if(arg is "-v" or "--version" or "-h" or "--help" or "--list-encodings") {
                    return $"The {arg} argument must be the first one";
                }
                else if(arg[0] is not '-') {
                    linkingSequence.Add(new RelocatableFileReference() { FullName = arg });
                }
                else {
                    return $"Unknwon argument '{arg}'";
                }
            }

            return null;
        }

        /// <summary>
        /// Process arguments coming from a file, taking in account
        /// that a relative path refers to the current directory.
        /// </summary>
        /// <param name="fileName">The file path.</param>
        /// <returns>An error message, or null if no error.</returns>
        private static string ProcessArgsFromFile(string fileName)
        {
            var filePath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, fileName));

            if(!File.Exists(filePath)) {
                return "File not found";
            }

            var fileLines = File.ReadLines(filePath).Select(l => l.Trim()).Where(l => l[0] is not ';' and not '#').ToArray();
            var fileArgsString = string.Join(' ', fileLines);
            var fileArgs = SplitWithEscapedSpaces(fileArgsString);

            argsByFile.Add((filePath, fileArgs));
            return ProcessArguments(fileArgs, true);
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

        private static ushort ParseNumericArg(string argName, string argValue, bool isByte, out string error)
        {
            ushort value;

            try {
                if(argValue.EndsWith("h", StringComparison.OrdinalIgnoreCase)) {
                    value = Convert.ToUInt16(argValue[..^1], 16);
                }
                else {
                    value = isByte ? byte.Parse(argValue) : ushort.Parse(argValue);
                }

                error = null;
                return value;
            }
            catch {
                error = $"{argName}: Invalid value, must be a decimal number or a hexadecimal number with the 'h' suffix, and be in the range 0-{(isByte ? "255/FFh" : "65535/FFFFh")}";
                return 0;
            }
        }

        /// <summary>
        /// Reset the configuration to default values, effectively forgetting
        /// any previous command line arguments processed.
        /// </summary>
        static void ResetConfig()
        {
            colorPrint = true;
            showBanner = true;
            fillByte = 0;
            startAddress = 0xFFFF;
            endAddress = 0;
            suppressWarnings = false;
            maxErrors = LinkingConfiguration.DEFAULT_MAX_ERRORS;
            workingDir = null;
            libraryDir = null;
            outputFileCase = OF_CASE_ORIGINAL;
            outputFileExtension = null;
            hexformat = false;
            outputFileName = null;
            verbosityLevel = 1;
            linkingSequence.Clear();
        }

        /// <summary>
        /// Generate the program arguments to use by merging the command line arguments,
        /// and contents of the LK80_ARGS variable if it exists.
        /// </summary>
        /// <param name="commandLineArgs"></param>
        /// <returns>The actual program arguments to use.</returns>
        private static string[] MaybeMergeArgsWithEnv(string[] commandLineArgs)
        {
            if(!commandLineArgs.Any(a => a is "-nea" or "--no-env-args")) {
                var envVariable = Environment.GetEnvironmentVariable("LK80_ARGS");
                if(envVariable is not null) {
                    envArgs = SplitWithEscapedSpaces(envVariable);
                }
                else {
                    envArgs = Array.Empty<string>();
                }
            }

            return envArgs.Concat(commandLineArgs).ToArray();
        }

        private static void PrintArgumentsAndFiles()
        {
            if(verbosityLevel < 3) {
                return;
            }

            var info = "";
            if(envArgs?.Length > 0) {
                info += $"Args from LK80_ARGS: {string.Join(' ', envArgs)}\r\n";
            }

            info += $"Args from command line: {string.Join(' ', commandLineArgs)}\r\n";

            for(int i = 0; i < argsByFile.Count; i++) {
                info += $"Args from {argsByFile[i].Item1}: {string.Join(' ', argsByFile[i].Item2)}\r\n";
            }

            info += $"Working directory: {workingDir}\r\n";
            info += $"Libraries directory: {libraryDir}\r\n";
            info += $"Color output: {YesOrNo(colorPrint)}\r\n";
            info += $"Show program banner: {YesOrNo(showBanner)}\r\n";
            info += $"Max errors: {(maxErrors == 0 ? "infinite" : maxErrors.ToString())}\r\n";
            info += $"Suppress warnings: {YesOrNo(suppressWarnings)}\r\n";
            info += $"Status verbosity level: {verbosityLevel}\r\n";
            if(outputFileExtension is not null) {
                info += $"Output file extension: {outputFileExtension}\r\n";
            }
            info += $"Fill byte: {fillByte:X2}h\r\n";
            if(startAddress is not 0xFFFF) {
                info += $"Start address: {startAddress:X4}h\r\n";
            }
            if(startAddress is not 0xFFFF) {
                info += $"End address: {endAddress:X4}h\r\n";
            }
            info += $"Output format: {(hexformat ? "HEX" : "BIN")}\r\n";

            if(linkingSequence.Any()) {
                info += "\r\nLinking sequence:\r\n\r\n";
                foreach(var item in linkingSequence) {
                    info += item switch {
                        SetCodeBeforeDataMode => "Set code before data\r\n",
                        SetDataBeforeCodeMode => "Set data before code\r\n",
                        SetCodeSegmentAddress scsa => $"Set code segment address to {scsa.Address:X4}h\r\n",
                        SetDataSegmentAddress sdsa => $"Set data segment address to {sdsa.Address:X4}h\r\n",
                        RelocatableFileReference rfr => $"Process file {rfr.FullName}\r\n",
                        _ => throw new InvalidOperationException($"Unexpected linking sequence item: {item.GetType().Name}")
                    };
                }
            }

            PrintProgress(info, 3);
        }

        private static string YesOrNo(bool what)
        {
            return what ? "YES" : "NO";
        }

        static bool errorsPrintedForCurrentProgram;

        private static void PrintWarning(string text)
        {
            text = $"WARNING: {text}";

            ErrorWriteLine();
            errorsPrintedForCurrentProgram = true;

            if(colorPrint) {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.BackgroundColor = defaultBackgroundColor;
                ErrorWriteLine(text);
                Console.ForegroundColor = defaultForegroundColor;
            }
            else {
                ErrorWriteLine(text);
            }
        }

        private static void PrintError(string text)
        {
            text = $"ERROR: {text}";

            ErrorWriteLine();
            errorsPrintedForCurrentProgram = true;

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

        private static void PrintFatal(string text)
        {
            ErrorWriteLine();
            errorsPrintedForCurrentProgram = true;

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

        private static void PrintLinkingProgress(string text)
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

        private static void SetShowBannerFlag(string[] args)
        {
            //The arguments list could contain both --show-banner and --no-show-banner arguments.
            //The last one specified wins, so we need to check whether they are present or not AND in which order.
            var indexOfLastShowBanner = args.Select((arg, index) => new { arg, index }).LastOrDefault(x => x.arg is "-sb" or "--show-banner")?.index ?? -1;
            var indexOfLastNoShowBanner = args.Select((arg, index) => new { arg, index }).LastOrDefault(x => x.arg is "-nsb" or "--no-show-banner")?.index ?? -1;
            showBanner = indexOfLastNoShowBanner == -1 || indexOfLastShowBanner > indexOfLastNoShowBanner;
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

        private static string GetProgramVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            while(version.EndsWith(".0") && version.Count(ch => ch is '.') > 1) {
                version = version[..^2];
            }
            return version;
        }



        static int Main_old(string[] args)
        {
            var config = new LinkingConfiguration() {
                FillingByte = 0, //0xFF,
                OpenFile = fileName => {
                    if(File.Exists(fileName)) {
                        return File.OpenRead(fileName);
                    }
                    else {
                        return null;
                    }
                },
                GetFullNameOfRequestedLibraryFile = fileName => 
                    Path.Combine(@"c:\users\Nesto\Nestor80", fileName) + (Path.HasExtension(fileName) ? "" : ".REL"),
                LinkingSequenceItems = new ILinkingSequenceItem[] {
                    /*
                    //new SetCodeSegmentAddress() {Address = 0},
                    //new SetCodeBeforeDataMode(),
                    //new RelocatableFileReference() {FullName=@"c:\users\nesto\Nestor80\empty.rel", DisplayName = "empty.rel"},
                    new RelocatableFileReference() {FullName=@"c:\users\nesto\Nestor80\simple.rel", DisplayName = "simple.rel"},
                    //new SetDataBeforeCodeMode(),
                    //new RelocatableFileReference() {FullName=@"c:\users\nesto\Nestor80\empty.rel", DisplayName = "empty.rel"},
                    new RelocatableFileReference() {FullName=@"c:\users\nesto\Nestor80\simple2.rel", DisplayName = "simple2.rel"},
                    //new SetCodeSegmentAddress() {Address = 0x103},
                    //new SetDataSegmentAddress() {Address = 0x230},
                    new RelocatableFileReference() {FullName=@"c:\users\nesto\Nestor80\simple3.rel", DisplayName = "simple3.rel"},
                    //new SetCodeSegmentAddress() {Address = 0xFFF5},
                    new RelocatableFileReference() {FullName=@"c:\users\nesto\Nestor80\simple4.rel", DisplayName = "simple4.rel"},
                    */
                    new RelocatableFileReference() {FullName=@"c:\users\nesto\Nestor80\simple5.rel", DisplayName = "simple5.rel"},
                    new RelocatableFileReference() {FullName=@"c:\users\nesto\Nestor80\simple6.rel", DisplayName = "simple6.rel"}

                },
                StartAddress = 0x100,
                //EndAddress = 0x200
            };

            var outputStream = File.Create(@"c:\users\nesto\Nestor80\simple_lk80.com");
            var result = RelocatableFilesProcessor.Link(config, outputStream);
            outputStream.Close();

            var stream = File.OpenRead(args[0]);
            var parsed = RelocatableFileParser.Parse(stream);
            return 0;
        }

        class CantOpenFileException : Exception
        {
            public CantOpenFileException(string filePath, string message) : base(message)
            {
                this.FilePath = filePath;
            }

            public string FilePath { get; }
        }
    }
}