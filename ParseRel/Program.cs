namespace Konamiman.ParseRel
{
    using static Console;

    /*
     * To build:
     * 
     * 1. Install the .NET Core 6 SDK, https://dotnet.microsoft.com/en-us/download/dotnet/6.0
     *
     * 2. From the directory of the files run:
     *
     *    dotnet publish --os=[linux|windows|osx]
     *
     * 3. Your program is at bin/Release/net6.0/(os)-x64/publish/
     *
     * Or you can use Visual Studio 2022.
     *
     * The generated program will require the .NET Runtime 6 to be installed in order to run.
     * To avoid this, add '--self-contained=true' to the build command
     * (but note that the generated program file will be much bigger).
     */
    internal class Program
    {
        static int Main(string[] args)
        {
            if(args.Length == 0) {
                WriteLine(
@"Z80 relocatable file parser 1.0
Bye Konamiman, 2022

Usage: ParseRel <file>"
                );
                return 0;
            }

            byte[] bytes;
            try {
                bytes = File.ReadAllBytes(args[0]);
            }
            catch(Exception ex) {
                Error.WriteLine($"*** Can't read file: {ex.Message}");
                return 1;
            }

            try {
                var parser = new RelFileParser(bytes);
                parser.ParseFile();
                return 0;
            }
            catch(EndOfStreamException) {
                Error.WriteLine("*** Unexpected end of file");
                return 2;
            }
            catch(Exception ex) {
                Error.WriteLine($"*** Unexpected error: ({ex.GetType().Name}) {ex.Message}");
                return 3;
            }
        }
    }
}