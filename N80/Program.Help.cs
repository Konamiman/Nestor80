namespace Konamiman.Nestor80.N80
{
    internal partial class Program
    {
        static string bannerText = """
            Nestor80 - The Z80 assembler for the 21th century
            (c) Konamiman 2023

            """;

        static string versionText = "1.0";

        static string simpleHelpText = """
            Usage: N80 <source file> [<output file>] [<arguments>]
                   N80 -v|--version
                   N80 -h|--help
            """;

        static string extendedHelpText = """
            Usage: N80 <source file> [<output file>] [<arguments>]

            Arguments:

            -v, --version
                Display the N80 version.

            -h, --help
                Display this help text.

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

            -no, --no-output
                Process the input file but don't generate the output file.

            -ie, --input-encoding
                Text encoding of the source file, default is UTF-8.
            """;
    }
}