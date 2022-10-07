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

        static readonly string extendedHelpText = """

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

            -no, --no-output
                Process the input file but don't generate the output file.

            -nco, --no-color-output
                Don't display assembly process messages and errors in color.

            -noap, --no-org-as-phase
                Don't treat ORG statements as .PHASE statements.

            -nsb, --nshow-banner
                Don't display the program title and copyright notice banner.

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

            -sb, --show-banner
                Display the program title and copyright notice banner (default).
            
            Full documentation (and donation links, wink wink):
            https://github.com/Konamiman/Nestor80
            """;
    }
}