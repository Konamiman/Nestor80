namespace Konamiman.Nestor80.LB80;

internal partial class Program
{
    static readonly string bannerText = """
        Libstor80 - The Z80 library manager for the 21th century
        (c) Konamiman 2023

        """;

    static readonly string simpleHelpText = """
        Usage: LB80 [<arguments>] <command> <library file> <relocatable files>
               LB80 -v|--version
               LB80 -h|--help
        """;

    /// <summary>
    /// Full help text, displayed when the program is run with the -h or --help argument.
    /// </summary>
    static readonly string extendedHelpText = $$$"""

    Foo!

    Libstor80 exit codes are:
        
        0: Success
        1: Invalid arguments
        2: Error opening or reading a library file or a relocatable file
        3: Error creating or writing to the library file
        4: Fatal error

    Full documentation (and donation links, wink wink):
    https://github.com/Konamiman/Nestor80
    """;
}
