using Konamiman.Nestor80.Linker;
using Konamiman.Nestor80.Linker.Parsing;

namespace Konamiman.Nestor80.N80
{
    internal partial class Program
    {
        static int Main(string[] args)
        {
            var config = new LinkingConfiguration() {
                FillingByte = 0xFF,
                OpenFile = fileName => {
                    if(File.Exists(fileName)) {
                        return File.OpenRead(fileName);
                    }
                    else {
                        return null;
                    }
                },
                LinkingSequenceItems = new ILinkingSequenceItem[] {
                    //new SetCodeSegmentAddress() {Address = 0x100},
                    new RelocatableFileReference() {FullName=@"c:\users\nesto\Nestor80\simple.rel", DisplayName = "simple.rel"},
                    new RelocatableFileReference() {FullName=@"c:\users\nesto\Nestor80\simple2.rel", DisplayName = "simple2.rel"},
                }
            };

            var outputStream = File.Create(@"c:\users\nesto\Nestor80\simple_lk80.com");
            var result = RelocatableFilesProcessor.Link(config, outputStream);
            outputStream.Close();

            var stream = File.OpenRead(args[0]);
            var parsed = RelocatableFileParser.Parse(stream);
            return 0;
        }
    }
}