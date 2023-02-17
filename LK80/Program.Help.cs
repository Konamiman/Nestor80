namespace Konamiman.Nestor80.LK80
{
    internal partial class Program
    {
        static readonly string bannerText = """
            Linkstor80 - The Z80 linker for the 21th century
            (c) Konamiman 2023

            """;

        static readonly string simpleHelpText = """
            Usage: LK80 <arguments and files>
                   LK80 -v|--version
                   LK80 -h|--help
                   LK80 --dump <file>
                   LK80 --info|-i <file>
            """;

        /// <summary>
        /// Full help text, displayed when the program is run with the -h or --help argument.
        /// </summary>
        static readonly string extendedHelpText = $"""

        HELP!!!

        Full documentation (and donation links, wink wink):
        https://github.com/Konamiman/Nestor80
        """;
    }
}
