using Konamiman.Nestor80.Assembler;
using Konamiman.Nestor80.Assembler.Relocatable;
using Konamiman.Nestor80.Linker.Parsing;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Konamiman.Nestor80.LB80;

internal partial class Program
{
    const int ERR_SUCCESS = 0;
    const int ERR_BAD_ARGUMENTS = 1;
    const int ERR_CANT_OPEN_FILE = 2;
    const int ERR_CANT_CREATE_FILE = 3;
    const int ERR_DUPLICATES = 4;
    const int ERR_FATAL = 5;

    const int CMD_INVALID = -1;
    const int CMD_CREATE = 0;
    const int CMD_ADD = 1;
    const int CMD_SET = 2;
    const int CMD_REMOVE = 3;
    const int CMD_EXTRACT = 4;
    const int CMD_VIEW = 5;
    const int CMD_DUMP = 6;    

    static bool colorPrint;
    static bool showBanner;
    static string workingDir;
    static int verbosityLevel;
    static string[] commandLineArgs;
    static string[] envArgs;
    static string commandString;
    static string libraryFilePath;
    static bool allowDuplicates;

    static readonly ConsoleColor defaultForegroundColor = Console.ForegroundColor;
    static readonly ConsoleColor defaultBackgroundColor = Console.BackgroundColor;

    static readonly Dictionary<AddressType, string> addressSuffixes = new() {
        { AddressType.CSEG, "'" },
        { AddressType.DSEG, "\"" },
        { AddressType.ASEG, " " },
        { AddressType.COMMON, "!" },
    };

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
        var workingDirError = SetWorkingDir(args);
        if(workingDirError != null) {
            ErrorWriteLine(workingDirError);
            return ERR_BAD_ARGUMENTS;
        }

        if(showBanner) WriteLine(bannerText);

        var argsErrorMessage = ProcessArguments(args, out int argsCount);
        if(argsErrorMessage is not null) {
            ErrorWriteLine($"Invalid arguments: {argsErrorMessage}");
            return ERR_BAD_ARGUMENTS;
        }

        args = args.Skip(argsCount).ToArray();
        if(args.Length == 0) {
            ErrorWriteLine("Missing command after the arguments");
            return ERR_BAD_ARGUMENTS;
        }

        commandString = args[0];
        var command = commandString switch {
            "c" or "create" => CMD_CREATE,
            "a" or "add" => CMD_ADD,
            "s" or "set" => CMD_SET,
            "r" or "remove" => CMD_REMOVE,
            "e" or "extract" => CMD_EXTRACT,
            "v" or "view" => CMD_VIEW,
            "d" or "dump" => CMD_DUMP,
            _ => CMD_INVALID
        };

        if(command is CMD_INVALID ) {
            ErrorWriteLine($"Unknown command: {args[0]}");
            return ERR_BAD_ARGUMENTS;
        }

        args = args.Skip(1).ToArray();
        if(args.Length == 0) {
            ErrorWriteLine("Missing library file path");
            return ERR_BAD_ARGUMENTS;
        }

        libraryFilePath = Path.GetFullPath(Path.Combine(workingDir, args[0]));

        PrintArguments();

        var remainingArgs = args.Skip(1).ToArray();

        try {
            var result = command switch {
                CMD_VIEW => ViewFile(),
                CMD_DUMP => DumpFile(),
                CMD_CREATE => CreateFile(remainingArgs),
                CMD_ADD => AddToFile(remainingArgs),
                CMD_SET => SetInFile(remainingArgs),
                CMD_REMOVE => RemoveFromFile(remainingArgs),
                //WIP
                _ => throw new Exception($"Unexpected command code: {command}")
            };

            return result;
        }
        catch(Exception ex) {
            PrintFatal($"Unexpected error processing command: {ex.Message}");
#if DEBUG
            ErrorWriteLine(ex.StackTrace.ToString());
#endif
            return ERR_FATAL;
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
        verbosityLevel = 1;
        allowDuplicates = false;
        //workingDir excluded on purpose, since it has special handling
    }

    private static string ProcessArguments(string[] args, out int argsCount)
    {
        argsCount = 0;
        for(int i = 0; i < args.Length; i++) {
            var arg = args[i];

            if(arg is "-co" or "--color-output") {
                colorPrint = true;
            }
            else if(arg is "-nco" or "--no-color-output") {
                colorPrint = false;
            }
            else if(arg is "-w" or "--working-dir") {
                //Already handled
                i++;
                argsCount += 2;
                continue;
            }
            else if(arg is "-ad" or "--allow-duplicates") {
                allowDuplicates = true;
            }
            else if(arg is "-nad" or "--no-allow-duplicates") {
                allowDuplicates = false;
            }
            else if(arg is "-vb" or "--verbosity") {
                if(i == args.Length - 1 || args[i + 1][0] == '-') {
                    return $"The {arg} argument needs to be followed by a verbosity level (a number between 0 and 3)";
                }

                i++;
                argsCount++;
                if(!int.TryParse(args[i], out verbosityLevel)) {
                    return $"The {arg} argument needs to be followed by a verbosity level (a number between 0 and 2)";
                }
                verbosityLevel = Math.Clamp(verbosityLevel, 0, 2);
            }
            else if(arg is "-rc" or "--reset-config") {
                ResetConfig();
            }
            else if(arg is "-sb" or "--show-banner" or "-nsb" or "--no-show-banner" or "-nea" or "--no-env-args") {
                //Already handled
            }
            else if(arg is "-v" or "--version" or "-h" or "--help" or "--list-encodings") {
                return $"The {arg} argument must be the first one";
            }
            else if(arg[0] is '-') {
                return $"Unknwon argument '{arg}'";
            }
            else {
                //Found the library file specification
                break;
            }

            argsCount++;
        }

        return null;
    }

    private static void PrintArguments()
    {
        if(verbosityLevel < 2) {
            return;
        }

        var info = "";
        if(envArgs?.Length > 0) {
            info += $"Args from LK80_ARGS: {string.Join(' ', envArgs)}\r\n";
        }

        info += $"Args from command line: {string.Join(' ', commandLineArgs)}\r\n";

        info += $"Working directory: {workingDir}\r\n";
        info += $"Color output: {YesOrNo(colorPrint)}\r\n";
        info += $"Show program banner: {YesOrNo(showBanner)}\r\n";
        info += $"Library file: {libraryFilePath}\r\n";

        PrintStatus(info);
    }

    private static void PrintError(string text)
    {
        text = $"ERROR: {text}";

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

    private static void PrintStatus(string text)
    {
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

        blankLinePrinted = text == ""; // || text.EndsWith("\r\n");
    }

    /// <summary>
    /// Generate the program arguments to use by merging the command line arguments,
    /// and contents of the LK80_ARGS variable if it exists.
    /// </summary>
    /// <param name="commandLineArgs"></param>
    /// <returns>The actual program arguments to use.</returns>
    private static string[] MaybeMergeArgsWithEnv(string[] commandLineArgs)
    {
        if(commandLineArgs.Any(a => a is "-nea" or "--no-env-args")) {
            return commandLineArgs;
        }

        var envVariable = Environment.GetEnvironmentVariable("LB80_ARGS");
        if(envVariable is null) {
            return commandLineArgs;
        }

        envArgs = SplitWithEscapedSpaces(envVariable);
        return envArgs.Concat(commandLineArgs).ToArray();
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

    private static void SetShowBannerFlag(string[] args)
    {
        //The arguments list could contain both --show-banner and --no-show-banner arguments.
        //The last one specified wins, so we need to check whether they are present or not AND in which order.
        var indexOfLastShowBanner = args.Select((arg, index) => new { arg, index }).LastOrDefault(x => x.arg is "-sb" or "--show-banner")?.index ?? -1;
        var indexOfLastNoShowBanner = args.Select((arg, index) => new { arg, index }).LastOrDefault(x => x.arg is "-nsb" or "--no-show-banner")?.index ?? -1;
        showBanner = indexOfLastNoShowBanner == -1 || indexOfLastShowBanner > indexOfLastNoShowBanner;
    }

    private static string SetWorkingDir(string[] args)
    {
        var indexOfLastWorkingDir = args.Select((arg, index) => new { arg, index }).LastOrDefault(x => x.arg is "-w" or "--working-dir")?.index ?? -1;
        if(indexOfLastWorkingDir == args.Length - 1) {
            return $"The {args[indexOfLastWorkingDir]} argument needs to be followed by a directory specification";
        }

        var indexOfLastResetConfig = args.Select((arg, index) => new { arg, index }).LastOrDefault(x => x.arg is "-rc" or "--reset-config")?.index ?? -1;

        if(indexOfLastResetConfig == -1) {
            workingDir = indexOfLastWorkingDir == -1 ? "" : args[indexOfLastWorkingDir + 1];
        }
        else {
            workingDir = (indexOfLastWorkingDir != -1 && indexOfLastWorkingDir > indexOfLastResetConfig) ? args[indexOfLastWorkingDir + 1] : "";
        }

        workingDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), workingDir));
        if(!Directory.Exists(workingDir)) {
            return $"Error when resolving working directory: '{workingDir}' doesn't exist or is not a directory";
        }

        return null;
    }

    private static string YesOrNo(bool what)
    {
        return what ? "YES" : "NO";
    }

    private static string GetProgramVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        while(version.EndsWith(".0") && version.Count(ch => ch is '.') > 1) {
            version = version[..^2];
        }
        return version;
    }

    static Stream libraryStream;

    static int OpenLibraryFile(string fileName = null)
    {
        var isLibraryFile = fileName == null;
        fileName ??= libraryFilePath;

        fileName = Path.GetFullPath(Path.Combine(workingDir, fileName));

        if(!File.Exists(fileName)) {
            PrintError(isLibraryFile ? "Library file not found" : $"File not found: {fileName}");
            return ERR_CANT_OPEN_FILE;
        }

        try {
            libraryStream = File.OpenRead(fileName);
        }
        catch(Exception ex) {
            PrintError(isLibraryFile ? $"Error opening library file: {ex.Message}" : $"Error opening {fileName}: {ex.Message}");
            return ERR_CANT_OPEN_FILE;
        }

        return ERR_SUCCESS;
    }

    private static int ViewFile()
    {
        var errorCode = OpenLibraryFile();
        if(errorCode != ERR_SUCCESS) {
            return errorCode;
        }

        if(verbosityLevel > 0) {
            PrintStatus($"Contents of library file {Path.GetFileName(Path.GetFullPath(libraryFilePath))}:");
            WriteLine("");
        }

        var programs = RelocatableFileParser.Parse(libraryStream);

        if(programs.Length == 0) {
            WriteLine("The file contains no programs.");
            return ERR_SUCCESS;
        }

        blankLinePrinted = true;
        foreach(var program in programs) {
            var programParts = program.Parts;

            WriteLine("");
            WriteLine($"Program: {program.ProgramName}");

            if(programParts.Any(p => p is ExtendedRelocatableFileHeader)) {
                WriteLine("  Extended relocatable file format");
            }

            var codeSegmentSizePart = programParts.FirstOrDefault(p => (p as LinkItem)?.Type is LinkItemType.ProgramAreaSize) as LinkItem;
            if(codeSegmentSizePart != null && codeSegmentSizePart.Address.Value != 0) {
                WriteLine($"  Code segment: {FormatSize(codeSegmentSizePart.Address.Value)}");
            }

            var dataSegmentSizePart = programParts.FirstOrDefault(p => (p as LinkItem)?.Type is LinkItemType.DataAreaSize) as LinkItem;
            if(dataSegmentSizePart != null && dataSegmentSizePart.Address.Value != 0) {
                WriteLine($"  Data segment: {FormatSize(dataSegmentSizePart.Address.Value)}");
            }

            var commonBlocks = programParts.Where(p => (p as LinkItem)?.Type is LinkItemType.DefineCommonSize).Cast<LinkItem>().ToArray();
            if(commonBlocks.Length > 0) {
                Console.WriteLine();
                WriteLine("  Common blocks:");
                foreach(var block in commonBlocks) {
                    WriteLine($"    {(string.IsNullOrWhiteSpace(block.Symbol) ? "(no name)" : block.Symbol)}: {FormatSize(block.Address.Value)}");
                }
            }

            var publicSymbols = programParts.Where(p => (p as LinkItem)?.Type is LinkItemType.DefineEntryPoint).Cast<LinkItem>().OrderBy(p => p.Symbol).ToArray();
            if(publicSymbols.Length > 0) {
                Console.WriteLine();
                Console.Write("  Public symbols:");
                var column = 0;
                foreach(var symbol in publicSymbols) {
                    if(column == 0) {
                        Console.WriteLine();
                        Console.Write("    ");
                    }
                    Console.Write($"{symbol.Address.Value:X4}{addressSuffixes[symbol.Address.Type]} {symbol.Symbol}\t");
                    column = (column+1) % 4; 
                }
                Console.WriteLine();
            }

            var externalReferences = programParts.Where(p => (p as LinkItem)?.Type is LinkItemType.ChainExternal).Cast<LinkItem>().OrderBy(p => p.Symbol).ToArray();
            if(externalReferences.Length > 0) {
                Console.WriteLine();
                Console.Write("  External references:");
                var column = 0;
                foreach(var symbol in externalReferences) {
                    if(column == 0) {
                        Console.WriteLine();
                        Console.Write("    ");
                    }
                    Console.Write($"{symbol.Symbol}\t");
                    column = (column + 1) % 4;
                }
                Console.WriteLine();
            }
        }

        return ERR_SUCCESS;
    }

    private static int DumpFile()
    {
        var errorCode = OpenLibraryFile();
        if(errorCode != ERR_SUCCESS) {
            return errorCode;
        }

        if(verbosityLevel > 0) {
            PrintStatus($"Full contents of library file {Path.GetFileName(Path.GetFullPath(libraryFilePath))}:");
            WriteLine("");
        }

        var ms = new MemoryStream();
        libraryStream.CopyTo(ms);
        RelFileDumper.DumpFile(ms.ToArray());

        return ERR_SUCCESS;
    }

    private static int CreateFile(string[] args)
    {
        if(args.Length == 0) {
            PrintError("At least one relocatable file path is needed.");
            return ERR_BAD_ARGUMENTS;
        }

        var result = new List<byte>();
        var allProgramNames = new List<string>();
        var allProgramBytes = new List<byte>();

        foreach(var fileName in args) {
            var filePath = Path.GetFullPath(Path.Combine(workingDir, fileName));
            if(verbosityLevel > 1) {
                PrintStatus($"Reading relocatable file: {filePath}");
            }
            var openFileError = OpenLibraryFile(filePath);
            if(openFileError != ERR_SUCCESS) {
                return openFileError;
            }

            var filePrograms = RelocatableFileParser.Parse(libraryStream);

            var programNames = filePrograms.Select(p => p.ProgramName).ToArray();
            allProgramNames.AddRange(programNames);

            var fileProgramsBytes = filePrograms.SelectMany(p => p.Bytes).ToArray();
            allProgramBytes.AddRange(fileProgramsBytes);
        }
        
        if(!allowDuplicates) {
            var firstDuplicate = allProgramNames.GroupBy(n => n, StringComparer.OrdinalIgnoreCase).Where(p => p.Count() > 1).FirstOrDefault();
            if(firstDuplicate != null) {
                PrintError($"There are {firstDuplicate.Count()} programs named '{firstDuplicate.Key}' amongst the processed files (use --allow-duplicates if that's intentional)");
                return ERR_DUPLICATES;
            }
        }

        allProgramBytes.Add(0x9E); //Add "end of file" link item

        try {
            File.WriteAllBytes(libraryFilePath, allProgramBytes.ToArray());
        }
        catch(Exception ex) {
            PrintError($"Error creating library file: {ex.Message}");
            return ERR_CANT_CREATE_FILE;
        }

        if(verbosityLevel > 1) {
            PrintStatus("");
        }

        if(verbosityLevel > 0) {
            PrintStatus($"Library file created: {libraryFilePath}");
        }

        return ERR_SUCCESS;
    }

    private static int AddToFile(string[] args)
    {
        if(args.Length == 0) {
            PrintError("At least one relocatable file path is needed.");
            return ERR_BAD_ARGUMENTS;
        }

        var errorCode = OpenLibraryFile();
        if(errorCode != ERR_SUCCESS) {
            return errorCode;
        }

        var libraryPrograms = RelocatableFileParser.Parse(libraryStream);
        libraryStream.Close();
        var allPrograms = new List<ParsedProgram>();

        foreach(var relFilePath in args) {
            if(verbosityLevel > 1) {
                PrintStatus($"Reading relocatable file: {relFilePath}");
            }

            errorCode = OpenLibraryFile(relFilePath);
            if(errorCode != ERR_SUCCESS) {
                return errorCode;
            }

            var filePrograms = RelocatableFileParser.Parse(libraryStream);
            libraryStream.Close();

            allPrograms.AddRange(filePrograms);
        }

        var distinctLibraryProgramNames = libraryPrograms.Select(p => p.ProgramName).Distinct(StringComparer.OrdinalIgnoreCase);
        var allProgramNames = distinctLibraryProgramNames.Concat(allPrograms.Select(p => p.ProgramName)).ToArray();
        if(!allowDuplicates) {
            var firstDuplicate = allProgramNames.GroupBy(n => n, StringComparer.OrdinalIgnoreCase).Where(p => p.Count() > 1).FirstOrDefault();
            if(firstDuplicate != null) {
                PrintError($"There are {firstDuplicate.Count()} programs named '{firstDuplicate.Key}' amongst the processed files (use --allow-duplicates if that's intentional)");
                return ERR_DUPLICATES;
            }
        }

        var allProgramBytes = libraryPrograms.Concat(allPrograms).Select(p => p.Bytes).SelectMany(x => x).ToList();
        allProgramBytes.Add(0x9E); //Add "end of file" link item

        try {
            File.WriteAllBytes(libraryFilePath, allProgramBytes.ToArray());
        }
        catch(Exception ex) {
            PrintError($"Error creating library file: {ex.Message}");
            return ERR_CANT_CREATE_FILE;
        }

        if(verbosityLevel > 1) {
            PrintStatus("");
        }

        if(verbosityLevel > 0) {
            PrintStatus($"Library file updated: {libraryFilePath}");
        }

        return ERR_SUCCESS;
    }

    private static int SetInFile(string[] args)
    {
        if(args.Length == 0) {
            PrintError("At least one relocatable file path is needed.");
            return ERR_BAD_ARGUMENTS;
        }

        var errorCode = OpenLibraryFile();
        if(errorCode != ERR_SUCCESS) {
            return errorCode;
        }

        var libraryPrograms = RelocatableFileParser.Parse(libraryStream).ToList();
        libraryStream.Close();
        var programsToSet = new List<ParsedProgram>();

        foreach(var relFilePath in args) {
            if(verbosityLevel > 1) {
                PrintStatus($"Reading relocatable file: {relFilePath}");
            }

            errorCode = OpenLibraryFile(relFilePath);
            if(errorCode != ERR_SUCCESS) {
                return errorCode;
            }

            var filePrograms = RelocatableFileParser.Parse(libraryStream);
            libraryStream.Close();

            programsToSet.AddRange(filePrograms);
        }

        var processedProgramNames = new List<string>();

        foreach(var program in programsToSet) {
            if(processedProgramNames.Contains(program.ProgramName, StringComparer.OrdinalIgnoreCase)) {
                continue;
            }

            var matchingLibraryProgram = libraryPrograms.FirstOrDefault(p => string.Equals(p.ProgramName, program.ProgramName, StringComparison.OrdinalIgnoreCase));
            if(matchingLibraryProgram == null) {
                libraryPrograms.Add(program);
            }
            else {
                matchingLibraryProgram.Bytes = program.Bytes;
            }

            processedProgramNames.Add(program.ProgramName);
        }

        var allProgramBytes = libraryPrograms.Select(p => p.Bytes).SelectMany(x => x).ToList();
        allProgramBytes.Add(0x9E); //Add "end of file" link item

        try {
            File.WriteAllBytes(libraryFilePath, allProgramBytes.ToArray());
        }
        catch(Exception ex) {
            PrintError($"Error creating library file: {ex.Message}");
            return ERR_CANT_CREATE_FILE;
        }

        if(verbosityLevel > 1) {
            PrintStatus("");
        }

        if(verbosityLevel > 0) {
            PrintStatus($"Library file updated: {libraryFilePath}");
        }

        return ERR_SUCCESS;
    }

    private static int RemoveFromFile(string[] args)
    {
        if(args.Length == 0) {
            PrintError("At least one program name is needed.");
            return ERR_BAD_ARGUMENTS;
        }

        var errorCode = OpenLibraryFile();
        if(errorCode != ERR_SUCCESS) {
            return errorCode;
        }

        var libraryPrograms = RelocatableFileParser.Parse(libraryStream).ToList();
        libraryStream.Close();

        var removed = false;

        foreach(var programName in args) {
            var existingProgram = libraryPrograms.FirstOrDefault(p => p.ProgramName.Equals(programName, StringComparison.OrdinalIgnoreCase));
            if(existingProgram != null) {
                libraryPrograms.Remove(existingProgram);
                removed = true;
            }
        }

        if(!removed) {
            if(verbosityLevel > 0) {
                PrintStatus($"No programs have been removed from the library file.");
            }
            return ERR_SUCCESS;
        }
        else if(libraryPrograms.Count == 0) {
            try {
                File.Delete(libraryFilePath);
            }
            catch(Exception ex) {
                PrintError($"Error deleting library file: {ex.Message}");
                return ERR_CANT_CREATE_FILE;
            }

            if(verbosityLevel > 0) {
                PrintStatus($"Library file deleted: {libraryFilePath}");
            }

            return ERR_SUCCESS;
        }

        var allProgramBytes = libraryPrograms.Select(p => p.Bytes).SelectMany(x => x).ToList();
        allProgramBytes.Add(0x9E); //Add "end of file" link item

        try {
            File.WriteAllBytes(libraryFilePath, allProgramBytes.ToArray());
        }
        catch(Exception ex) {
            PrintError($"Error creating library file: {ex.Message}");
            return ERR_CANT_CREATE_FILE;
        }

        if(verbosityLevel > 0) {
            PrintStatus($"Library file updated: {libraryFilePath}");
        }

        return ERR_SUCCESS;
    }

    private static string FormatSize(ushort size) => $"{size} ({size:X4}h) bytes";
}