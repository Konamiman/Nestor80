using Konamiman.Nestor80.Linker;
using Konamiman.Nestor80.Linker.Parsing;

namespace Konamiman.Nestor80.N80
{
    internal partial class Program
    {
        static int Main(string[] args)
        {
            var config = new LinkingConfiguration() {
                FillingByte = 0, //0xFF,
                OpenFile = fileName => {
                    if(File.Exists(fileName)) {
                        return File.OpenRead(fileName);
                    }
                    else {
                        return null;
                    }
                },
                LinkingSequenceItems = new ILinkingSequenceItem[] {
                    new SetCodeSegmentAddress() {Address = 0},
                    //new SetCodeBeforeDataMode(),
                    new RelocatableFileReference() {FullName=@"c:\users\nesto\Nestor80\simple.rel", DisplayName = "simple.rel"},
                    new SetDataBeforeCodeMode(),
                    new RelocatableFileReference() {FullName=@"c:\users\nesto\Nestor80\simple2.rel", DisplayName = "simple2.rel"},
                    //new SetCodeSegmentAddress() {Address = 0x103},
                    //new SetDataSegmentAddress() {Address = 0x230},
                    new RelocatableFileReference() {FullName=@"c:\users\nesto\Nestor80\simple3.rel", DisplayName = "simple3.rel"},
                    new SetCodeSegmentAddress() {Address = 0xFFF5},
                    new RelocatableFileReference() {FullName=@"c:\users\nesto\Nestor80\simple4.rel", DisplayName = "simple4.rel"},
                },
                StartAddress = 0x100,
                EndAddress = 0x200
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