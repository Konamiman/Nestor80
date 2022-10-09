namespace Konamiman.Nestor80.N80
{
    internal partial class Program
    {
        static readonly string bannerText = """
            Nestor80 - The Z80 assembler for the 21th century
            (c) Konamiman 2023

            """;

        static readonly string versionText = "1.0";

        static readonly string simpleHelpText = """
            Usage: N80 <source file> [<output file>] [<arguments>]
                   N80 -v|--version
                   N80 -h|--help
            """;

        static readonly string extendedHelpText = $"""

            Arguments:

            <source file>
                Full path of the Z80 assembly source file to process,
                can be absolute or relative to the current directory.

            <output file>
                Full path of the assembled absolute or relocatable file to generate.
                
                If omitted, a file with the same name of the source file and
                .BIN or .REL extension will be generated in the current directory.

                If it's a directory (instead of a file), the output file will be
                generated in that directory, and the file name will be as when
                the argument is omitted.

                If the path is relative it will be considered relative to
                the current directory.

                If the path starts with '$/' it will be considered relative to
                the directory of the source file.

                If the path is just '$' then the file will be geenerated in
                the directory of the source file, and the file name will be
                as when the argument is omitted.

            -cid, --clear-include-directories
                Clear the list of the directories where relative INCLUDEd files
                will be searched for. If this argument isn't followed by any
                instance of --include-directory, any INCLUDE instruction
                referencing a relative file will fail.

            -co, --color-output
                Display assembly process messages and errors in color (default).

            -ds, --define-symbols <symbol>[=<value>][,<symbol>[=<value>][,...]]
                Predefine symbols for the assembled program.
                The default value if no <value> is provided is FFFFh. 
                The symbols will be created as if they had been defined with DEFL,
                therefore they can be redefined in the source code using the same instruction.

                Example: -ds symbol1,symbol2=1234,symbol3=ABCDh
            
            -id, --include-directory <directory path>
                By default relative paths referenced in INCLUDE instructions will be
                considered to be relative to the input file. This argument allows to
                specify an extra directory where INCLUDEd files will be searched for;
                use the argument multiple times to add more than one directory.
                Directories are scanned in the order they are declared.

                If <directory path> isn't absolute it will be considered relative
                to the current directory.

                If <directory path> starts with '$/' it will be considered relative to
                the directory of the source file.

                If <directory path> is just '$' then it's the directory of the source file
                (already included by default, but you may need to re-add it if you have
                removed it with --clear-include-directories).

            -ie, --input-encoding <encoding>
                Text encoding of the source file, it can be an encoding name or a codepage number.
                Default is UTF-8.

            -me, --max-errors <count>
                Stop the assembly process after reaching the specified number of errors
                (not including warnings, and the process will still stop on the first fatal error).
                0 means "infinite". Default: {DEFAULT_MAX_ERRORS}.

            -no, --no-output
                Process the input file but don't generate the output file.

            -nco, --no-color-output
                Don't display assembly process messages and errors in color.

            -nds, --no-define-symbols
                Forget all symbols that had been predefined with --define-symbols.

            -noap, --no-org-as-phase
                Don't treat ORG statements as .PHASE statements (default).

            -nsb, --nshow-banner
                Don't display the program title and copyright notice banner.

            -nsw, --no-silence-warnings [<code>[,<code>[,...]]]
                Remove the specified warning codes from the list of warnings to silence
                as specified with --silence-warnings. If no codes are provided, 
                clear the list (so display all warnings again).

            -nsap, --no-silence-assembly-print
                Display messages generated during assembly via .PRINTX, .PRINT, .PRINT1
                and .PRINT2 instructions (default).

            -nss, --no-silence-status
                Don't display assembly status messages (input and output filenames,
                start of pass 2, assembly duration...) (default)

            -oap, --org-as-phase
                Treat ORG statements as .PHASE statements. This argument has effect only
                when the build type is absolute.

                The effect of this argument is that all the generated output will be written
                consecutively to the output file, regardless of their location in memory.
                Example:

                org 100
                db 1,2,3,4
                org 200
                db 5,6,7,8
                org 102
                db 9,10

                Output without this argument: 1,2,9,10,0,...,0,5,6,7,8 (total 104 bytes)
                Output with this argument: 1,2,3,4,5,6,7,8,9,10

            -sap, --silence-assembly-print
                Don't display messages generated during assembly via .PRINTX, .PRINT, .PRINT1
                and .PRINT2 instructions.

            -sb, --show-banner
                Display the program title and copyright notice banner (default).

            -ss, --silence-status
                Don't display assembly status messages (input and output filenames,
                start of pass 2, assembly duration...)

            -sw, --silence-warnings [<code>[,<code>[,...]]]
                Don't display the warnings with the specified codes.
                If no codes are specified, don't display any warning at all.

            --rc, --reset-config
                Reset all the assembly configuration back to default values
                (in other words: ignore all the previous arguments except input and output files)

            Full documentation (and donation links, wink wink):
            https://github.com/Konamiman/Nestor80
            """;
    }
}