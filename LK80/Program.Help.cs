using Konamiman.Nestor80.Linker;

namespace Konamiman.Nestor80.LK80
{
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
        static readonly string extendedHelpText = $"""

        Arguments can be specified as follows (and are combined in that order):
        
        - A LK80_ARGS environment variable (can be disabled with --no-env-args).
        - The command line.
        - Argument files (with --argument-file).
        
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

        Numeric argument values can be decimal numbers or hexadecimal numbers followed by "h".


        LINK SEQUENCE ITEMS

        <file>
            Any argument that doesn't start with "-" (and is not the value of another
            argument) is assumed to be the path for the next relocatable file to be processed.
            If the path isn't absolute then it's considered to be relative to the working
            directory (the one specified with a --working-directory argument if present,
            or the current directory otherwise).

            If the current linking mode is "separate code and data" then the code segment 
            of the file will be linked after the code segment of the last linked file 
            (unless a different address is supplied beforehand with --code), and the data 
            segment of the file will be linked after the data segment of the last linked 
            file (unless a different address is supplied beforehand with --data).

            Otherwise, the entire file (data and then code, or the opposite) will be linked 
            after the last linked file (unless a different address is supplied beforehand
            with --code). It may be a bit confusing but indeed, the entire file is linked 
            at the address specified by --code even in "data before code" mode.

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
            in the resulting binary file. Default is 0.
        
        -ld, --library-dir <path>
            The directory to search for for libraries requested with the .REQUEST instruction.
            By default it's the same as --working-dir (which itself defaults to the current
            directory).

        -me, --max-errors <count>
            Stop the linking process after reaching the specified number of errors
            (not including warnings, and the process will still stop on the first fatal error).
            0 means "infinite". Default: {LinkingConfiguration.DEFAULT_MAX_ERRORS}.

        -nco, --no-color-output
            Don't display linking process messages and errors in color.

        -nsw, --no-suppress-warnings
            Display linking process warnings (default).

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
        
        -sw, --suppress-warnings
            Don't display linking process warnings.

        -w, --working-dir <path>
            The working directory. All non-absolute file specifications will be considered
            relative to this directory (this includes also libraries requested with .REQUEST,
            unless --library-dir is specified). The default value is the current directory.


        Full documentation (and donation links, wink wink):
        https://github.com/Konamiman/Nestor80
        """;
    }
}
