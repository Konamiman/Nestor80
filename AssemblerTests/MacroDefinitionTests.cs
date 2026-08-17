using Konamiman.Nestor80.Assembler;
using NUnit.Framework;

namespace Konamiman.Nestor80.AssemblerTests
{
    [TestFixture]
    public class MacroDefinitionTests
    {
        private static byte[] AssembleAbsolute(string source)
        {
            var result = AssemblySourceProcessor.Assemble(source, new AssemblyConfiguration() { BuildType = BuildType.Absolute });
            Assert.IsFalse(result.HasErrors, string.Join("; ", result.Errors.Select(e => e.Message)));

            var outputStream = new MemoryStream();
            OutputGenerator.GenerateAbsolute(result, outputStream);
            return outputStream.ToArray();
        }

        [Test]
        // The label goes to the macro body, so it's defined at each expansion
        // pointing to the address that follows the expanded bytes.
        public void LabelOnSameLineAsEndmTerminatesNamedMacroDefinition()
        {
            var bytes = AssembleAbsolute(
                "\torg 100h\r\n" +
                "M\tmacro\r\n" +
                "MS:\tdefb 1,2,3\r\n" +
                "ME:\tendm\r\n" +
                "\tM\r\n" +
                "\tdefb ME-MS\r\n" +
                "\tend\r\n");

            CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 3 }, bytes);
        }

        [Test]
        public void LabelOnSameLineAsEndmTerminatesReptDefinition()
        {
            var bytes = AssembleAbsolute(
                "\torg 100h\r\n" +
                "\trept 1\r\n" +
                "\tdefb 7\r\n" +
                "ME:\tendm\r\n" +
                "\tdefb ME-100h\r\n" +
                "\tend\r\n");

            CollectionAssert.AreEqual(new byte[] { 7, 1 }, bytes);
        }

        [Test]
        // The "XX: endm" line closes the nested REPT, not the macro being defined,
        // so the entire line must be stored as part of the macro body.
        public void LabelOnEndmOfNestedReptStaysInMacroBody()
        {
            var bytes = AssembleAbsolute(
                "\torg 100h\r\n" +
                "M\tmacro\r\n" +
                "\trept 1\r\n" +
                "\tdefb 9\r\n" +
                "XX:\tendm\r\n" +
                "\tdefb XX-100h\r\n" +
                "\tendm\r\n" +
                "\tM\r\n" +
                "\tend\r\n");

            CollectionAssert.AreEqual(new byte[] { 9, 1 }, bytes);
        }

        [Test]
        // A label before REPT must not prevent the nesting depth increase,
        // otherwise the ENDM of the nested block would terminate the macro definition.
        public void LabeledReptInsideMacroDefinitionTracksNestingDepth()
        {
            var bytes = AssembleAbsolute(
                "\torg 100h\r\n" +
                "M\tmacro\r\n" +
                "FOO:\trept 2\r\n" +
                "\tdefb 9\r\n" +
                "\tendm\r\n" +
                "\tendm\r\n" +
                "\tM\r\n" +
                "\tend\r\n");

            CollectionAssert.AreEqual(new byte[] { 9, 9 }, bytes);
        }
    }
}
