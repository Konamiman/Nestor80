using System.Text;
using Konamiman.Nestor80.Assembler;
using Konamiman.Nestor80.Assembler.Errors;
using Konamiman.Nestor80.Assembler.Output;
using Konamiman.Nestor80.Assembler.Relocatable;
using NUnit.Framework;

namespace Konamiman.Nestor80.AssemblerTests
{
    [TestFixture]
    public class AssemblySourceProcessorTests
    {
        [Test]
        // Source byte 7Fh decodes to DEL and bytes in the 80h-9Fh range decode to C1 control
        // characters with ISO 8859-1; they must be kept in string literals so that the byte
        // values round trip to the output.
        public void DelAndC1CharsInStringsSurviveByteRoundTrip()
        {
            var encoding = Encoding.GetEncoding(28591);
            var sourceBytes = encoding.GetBytes("\torg 100h\r\n\tdefb \"a\u0083b\u007fc\"\r\n\tend\r\n");

            var result = AssemblySourceProcessor.Assemble(
                new MemoryStream(sourceBytes),
                encoding,
                new AssemblyConfiguration() { BuildType = BuildType.Absolute, OutputStringEncoding = "28591" });

            Assert.IsFalse(result.HasErrors);

            var outputStream = new MemoryStream();
            OutputGenerator.GenerateAbsolute(result, outputStream);
            CollectionAssert.AreEqual(new byte[] { 0x61, 0x83, 0x62, 0x7F, 0x63 }, outputStream.ToArray());
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

        private static AssemblyResult AssembleMixedRelocatablesSource(string lines)
        {
            var source = "\textrn ext\r\nbwd:\tnop\r\n\t" + lines + "\r\nfwd:\tnop\r\n\tend\r\n";
            var result = AssemblySourceProcessor.Assemble(source, new AssemblyConfiguration() { BuildType = BuildType.Relocatable });
            Assert.IsFalse(result.HasErrors, string.Join("; ", result.Errors.Select(e => e.Message)));
            return result;
        }

        private static string DescribeRelocatablePart(RelocatableOutputPart part) => part switch {
            RelocatableValue value => $"{part.Index}:{value.Type} {value.Value:X4}",
            LinkItemsGroup group => $"{part.Index}:{group.LinkItems.Single(i => i.IsExternalReference).GetSymbolName()}",
            _ => throw new InvalidOperationException($"Unexpected relocatable part: {part}")
        };

        [TestCase("defw bwd,ext", "0:CSEG 0000", "2:ext")]
        [TestCase("defw ext,bwd", "0:ext", "2:CSEG 0000")]
        [TestCase("defw bwd+1,ext", "0:CSEG 0001", "2:ext")]
        [TestCase("defw fwd,ext", "0:CSEG 0005", "2:ext")]
        [TestCase("defw 1234h,bwd,ext", "2:CSEG 0000", "4:ext")]
        [TestCase("defw ext,bwd,ext,fwd", "0:ext", "2:CSEG 0000", "4:ext", "6:CSEG 0009")]
        [TestCase("defb bwd,ext", "0:CSEG 0000", "1:ext")]
        [TestCase("defb ext,bwd", "0:ext", "1:CSEG 0000")]
        // A line with relocatables and external references must keep all of them,
        // sorted by their position in the line regardless of how each one got resolved.
        public void RelocatablesAreKeptWhenMixedWithExternalsInDefbOrDefw(string line, params string[] expectedParts)
        {
            var result = AssembleMixedRelocatablesSource(line);

            var processedLine = result.ProcessedLines.OfType<DefbLine>().Single();
            CollectionAssert.AreEqual(expectedParts, processedLine.RelocatableParts.Select(DescribeRelocatablePart));
        }

        [TestCase("defw bwd,ext", "defw bwd\r\n\tdefw ext")]
        [TestCase("defw ext,bwd", "defw ext\r\n\tdefw bwd")]
        [TestCase("defw 1234h,bwd,ext,5678h", "defw 1234h\r\n\tdefw bwd\r\n\tdefw ext\r\n\tdefw 5678h")]
        [TestCase("defb bwd,ext", "defb bwd\r\n\tdefb ext")]
        [TestCase("defb ext,bwd", "defb ext\r\n\tdefb bwd")]
        public void MultiOperandDefbOrDefwWithExternalsGeneratesSameOutputAsOneOperandPerLine(string multiOperandLine, string oneOperandPerLine)
        {
            static byte[] Generate(AssemblyResult result)
            {
                result.ProgramName = "TEST";
                var outputStream = new MemoryStream();
                OutputGenerator.GenerateRelocatable(result, outputStream, initDefs: false, extendedFormat: false);
                return outputStream.ToArray();
            }

            var multiOperandOutput = Generate(AssembleMixedRelocatablesSource(multiOperandLine));
            var oneOperandPerLineOutput = Generate(AssembleMixedRelocatablesSource(oneOperandPerLine));

            CollectionAssert.AreEqual(oneOperandPerLineOutput, multiOperandOutput);
        }
    }
}
