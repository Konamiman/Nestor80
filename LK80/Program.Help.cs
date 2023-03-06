using Konamiman.Nestor80.Linker;

namespace Konamiman.Nestor80.LK80;

internal partial class Program
{
    static readonly string bannerText = """
        Linkstor80 - The Z80 linker for the 21th century
        (c) Konamiman 2023

        """;

    static readonly string simpleHelpText = """
        Usage: LK80 <configuration flags and link sequence items>
                LK80 -v|--version
                LK80 -h|--help
        """;

    /// <summary>
    /// Full help text, displayed when the program is run with the -h or --help argument.
    /// </summary>
    static readonly string extendedHelpText = $$$"""

    Arguments can be specified as follows (and are combined in that order):
        
    - A LK80_ARGS environment variable (can be disabled with --no-env-args).
    - The command line.
    - Argument files (with --args-file).
        
    If you need to include a space as part of an argument (e.g. a directory name) 
    in LK80_ARGS or in an arguments file, escape it with a backslash (so "\ "
    to represent " "). Escaping for command line arguments depends on your shell.

    Two types of arguments are supported:

    - Link sequence items: these define the linking process by specifying the 
        relocatable files to use and the memory addresses where they will be loaded. 
        As many of these as needed can be used, and the order in which they appear 
        in the arguments list is significant.
    - Configuration flags: these are used to configure the linking process as a whole.
        Except where otherwise stated, these are intended to appear only once in the
        arguments list; if they appear more than once, only the last one will be applied.

    All the configuration flags are optional. The minimum set of link sequence items
    required is one single relocatable file path.

    Numeric argument values can be decimal numbers or hexadecimal numbers followed by "h".


    LINK SEQUENCE ITEMS

    <file>
        Any argument that doesn't start with "-" (and is not the value of another
        argument) is assumed to be the path for the next relocatable file to be processed.
        If the path isn't absolute then it's considered to be relative to the working
        directory (the one specified with a --working-dir argument if present,
        or the current directory otherwise).

        If the current linking mode is "separate code and data" then the code segment 
        of the file will be linked after the code segment of the last linked file 
        (unless a different address is supplied beforehand with --code), and the data 
        segment of the file will be linked after the data segment of the last linked 
        file (unless a different address is supplied beforehand with --data).

        Otherwise, the entire file (data and then code, or the opposite) will be linked 
        after the last linked file (unless a different address is supplied beforehand
        with --code). It may be a bit confusing but indeed, the entire file is linked 
        starting at the address specified by --code even in "data before code" mode.

    -dbc, --data-before-code
        Switches the linking process to "data before code" mode: any relocatable file 
        processed after this argument is found will be placed in memory with the contents 
        of the data segment first, immediately followed by the contents of the code segment.
        This is the default mode at the start of the process for compatibility with LINK-80.

        This argument will be ignored (and a warning will be generated) if the linking process
        has been switched to "separate code and data" mode with a --data argument.

    -cbd, --code-before-data
        Switches the linking process to "code before data" mode: any relocatable file 
        processed after this argument is found will be placed in memory with the contents 
        of the code segment first, immediately followed by the contents of the data segment.
        
        This argument will be ignored (and a warning will be generated) if the linking
        process has been switched to "separate code and data" mode with a --data argument.
        
    -c, --code <address>
        Specifies the address where the linking of the next relocatable file (the whole file
        or only the code segment, depending on the linking mode) will start.
        For compatibility with LINK-80 the initial value is 0103h.

    -d, --data <address>
        Switches the linking process to "separate code and data" mode (if it wasn't in this 
        mode already), and specifies the address in which the data segment of the next 
        relocatable file will be linked. Once the linking process has been switched to 
        "separate code and data" mode it's not possible to go back to the "code before data" 
        and "data before code" modes.


    CONFIGURATION FLAGS

    -af, --args-file <file path>
        Read additional arguments from the specified file. The arguments are
        processed immediately. Recursivity is not supported (additional
        --args-file arguments aren't allowed inside an arguments file).
        A non-absolute path will be considered relative to the current directory.
        
        Arguments inside the file can be all in one single line or
        spread across multiple lines. Lines starting with ; or #
        (not counting leading spaces) will be ignored.
        
        This option can't be undone (there's not "--no-args-file" argument),
        but if necessary you can use --reset-config to start over.

    -co, --color-output
        Display linking process messages and errors in color (default).

    -e, --end <address>
        The end address of the generated binary program. This value will only be used if it's
        larger than the larger address used by any of the linked programs; the gap will be
        filled with the value specified by --fill (0 by default).
        See the examples for --start.

    -f, --fill <value>
        The byte value that will be used to fill the gaps between linked programs (and between
        the --start address and the first program, and/or the last program and --end, if needed)
        in the generated file. Default is 0.
        
    -ld, --library-dir <path>
        The directory to search for for libraries requested with the .REQUEST instruction.
        By default it's the same as --working-dir (which itself defaults to the current
        directory). Non-absolute paths are relative to the current directory.

    -me, --max-errors <count>
        Stop the linking process after reaching the specified number of errors
        (not including warnings, and the process will still stop on the first fatal error).
        0 means "infinite". Default: {{{LinkingConfiguration.DEFAULT_MAX_ERRORS}}}.

    -nco, --no-color-output
        Don't display linking process messages and errors in color.

    -nea, --no-env-args
        Don't read arguments from the LK80_ARGS environment variable.
        This argument is ignored when found inside LK80_ARGS or an arguments file.

    -nsb, --no-show-banner
        Don't display the program title and copyright notice banner.
        This argument is ignored when found inside an arguments file.

    -nsw, --no-silence-warnings
        Display linking process warnings (default).

    -ny, --no-symbols-file
        Don't generate a symbols list file. This is the default.

    -o, --output-file
        Path and name of the generated file. By default it's a file with the name of the
        first processed relocatable file, with extension .BIN if the output format is
        binary or .HEX if the output format is Intel HEX, generated in the working directory.

    -of, --output-format bin|hex
        Format of the generated file, 'bin' for binary (default) or 'hex' for Intel HEX.

    -ofc, --output-file-case upper|lower|orig
        Whether the casing of the generated file name will be converted to lower case,
        converted to upper case, or kept as the first processed file name (this is the default).
        This argument is used only when the name of the output file is chosen as 
        the same name of the first processed file (because no output file path is specified,
        or because the specified path is a directory).
        
        The default .BIN or .HEX extension added to the output file name is affected by
        this argument, but an extension explicitly supplied with --output-file-extension
        is not. Example:
        
        LK80 FILE.REL --output-file-case lower --> generates file.bin
        LK80 FILE.REL --output-file-case lower --output-file-extension COM --> generates file.COM
        
        This settings affects the casing of the generated symbols file too
        if a --symbols-file argument is supplied.

    -ofe, --output-file-extension [.]<extension>
        The extension for the generated file. This value is used only when the
        name of the output file is chosen as the same name of the first processed file
        (because no output file path is specified, or because the specified path
        is a directory). Default is .BIN when the output file format is binary and .HEX
        when the output file format is Intel HEX.

    -rc, --reset-config
        Resets all the linking configuration back to default values and empties the list of
        link items (in other words: ignores all the previous arguments and starts over).

    -s, --start <address>
        The start address of the generated binary program. This value will only be used if it's
        smaller than the smaller address used by any of the linked programs; the gap will be
        filled with the value specified by --fill (0 by default).

        Examples:

        LK80 --code 1000h file.rel
            file.BIN starts with the contents of file.rel, linked at address 1000h.

        LK80 --start 1000h --code 1200h file.rel
            file.BIN starts with 200h zero bytes, followed with the contents of file.rel,
            linked at address 1200h.

        LK80 --start 2000h --code 1000h file.rel
            file.BIN starts with the contents of file.rel, linked at address 1000h
            (the start address is ignored).
        
    -sb, --show-banner
        Display the program title and copyright notice banner (default).
        This argument is ignored when found inside an arguments file.

    -sw, --silence-warnings
        Don't display linking process warnings.

    -vb, --verbosity <level>
        Selects the verbosity of the status messages shown during the linking process.
        The information shown for each level is as follows (each level includes the
        information from all the previous levels):
        
        0: Nothing.
        1: Relocatable file names as they are processed, generated file path (default).
        2: All the command line arguments from all the sources, the entire configuration
            resulting from applying all the arguments, all the link sequence items
            with full file paths.
        3: Information about all the processed files (program name, segments start addresses,
            public symbols with their addresses).
        
        To silence other types of output see: --silence-warnings, --no-show-banner.

    -w, --working-dir <path>
        The working directory. All non-absolute path specifications will be considered
        relative to this directory (this includes also libraries requested with .REQUEST,
        unless --library-dir is specified). The default value is the current directory.

    -y, --symbols-file [<path>]
        Generate a symbols list file. If <path> is omitted, a file with the name of the
        first processed relocatable file and extension .SYM is created in the working
        directory.

    -yf, --symbols-file-format l80|l80a|json|equs|pequs
        Format for the symbols file, one of:

        l80: Four columns per line, each being <value in hex><space><symbol name><tab>.
                Symbols are sorted by symbol value.
                This is the format of symbols files generated by LINK-80, and is the default.

        l80a: Same as l80, but symbols are sorted alphabetically by symbol name.

        json: A json file: {"symbols":{"name":1234,"name2":5678}}

        equs: A source file containing all the symbols as EQU statements.

        pequs: Same as 'equs', but additionally, all symbols will be declared as public.

        The encoding of the generated symbols files is UTF-8.

    -yr, --symbols-file-regex <regex>
        Limit the symbols to be included in the generated symbols file to only those that
        match the supplied regular expression. This argument can be specified multiple times,
        the regular expressions will be combined as "OR" so that symbols matching at least
        one of the expressions will be included in the symbols file.

        Example: --symbols-file-regex ^\$ --symbols-file-regex [0-9]$
                    will match symbols that either start with "$" or end with a digit.

    Linkstor80 exit codes are:
        
        0: Success
        1: Invalid arguments
        2: Error opening or reading one of the files to link
        3: Error creating or writing to the output file
        4: Error creating or writing to the symbols file
        5: Linking errors were thrown
        6: Fatal error

    Full documentation (and donation links, wink wink):
    https://github.com/Konamiman/Nestor80
    """;
}

