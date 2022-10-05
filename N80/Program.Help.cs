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

            -co, --color-output
                Display assembly process messages and errors in color (default).

            -ie, --input-encoding
                Text encoding of the source file, default is UTF-8.

            -no, --no-output
                Process the input file but don't generate the output file.

            -nco, --no-color-output
                Don't display assembly process messages and errors in color.

            -nsb, --nshow-banner
                Don't display the program title and copyright notice banner.

            -sb, --show-banner
                Display the program title and copyright notice banner (default).
            
            Full documentation (and donation links, wink wink):
            https://github.com/Konamiman/Nestor80
            """;
    }
}