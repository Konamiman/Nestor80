using System.Text;
using Konamiman.Nestor80.Assembler;
using Konamiman.Nestor80.Assembler.Errors;
using NUnit.Framework;

namespace Konamiman.Nestor80.AssemblerTests
{
    [TestFixture]
    public class AssemblySourceProcessorTests
    {
        [Test]
        // Source bytes in the 80h-9Fh range decode to C1 control characters with ISO 8859-1;
        // they must be kept in string literals so that the byte values round trip to the output.
        public void CharsAbove7FhInStringsSurviveByteRoundTrip()
        {
            var encoding = Encoding.GetEncoding(28591);
            var sourceBytes = encoding.GetBytes("\torg 100h\r\n\tdefb \"a\u0083b\"\r\n\tend\r\n");

            var result = AssemblySourceProcessor.Assemble(
                new MemoryStream(sourceBytes),
                encoding,
                new AssemblyConfiguration() { BuildType = BuildType.Absolute, OutputStringEncoding = "28591" });

            Assert.IsFalse(result.HasErrors);

            var outputStream = new MemoryStream();
            OutputGenerator.GenerateAbsolute(result, outputStream);
            CollectionAssert.AreEqual(new byte[] { 0x61, 0x83, 0x62 }, outputStream.ToArray());
        }

        [Test]
        public void CharsNotSupportedByStringEncodingProduceAnError()
        {
            var encoding = Encoding.GetEncoding(28591);
            var sourceBytes = encoding.GetBytes("\torg 100h\r\n\tdefb \"a\u0083b\"\r\n\tend\r\n");

            var result = AssemblySourceProcessor.Assemble(
                new MemoryStream(sourceBytes),
                encoding,
                new AssemblyConfiguration() { BuildType = BuildType.Absolute, OutputStringEncoding = "ASCII" });

            Assert.IsTrue(result.Errors.Any(e => e.Code is AssemblyErrorCode.InvalidExpression && e.Message.Contains("aren't supported by the current encoding")));
        }

        [Test]
        public void ControlCharsBelow20hAreRemovedFromSourceLines()
        {
            var encoding = Encoding.GetEncoding(28591);
            var sourceBytes = encoding.GetBytes("\torg 100h\r\n\tdefb 1\u001a\r\n\tend\r\n");

            var result = AssemblySourceProcessor.Assemble(
                new MemoryStream(sourceBytes),
                encoding,
                new AssemblyConfiguration() { BuildType = BuildType.Absolute });

            Assert.IsFalse(result.HasErrors);

            var outputStream = new MemoryStream();
            OutputGenerator.GenerateAbsolute(result, outputStream);
            CollectionAssert.AreEqual(new byte[] { 0x01 }, outputStream.ToArray());
        }
    }
}
