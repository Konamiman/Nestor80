using Konamiman.Nestor80.Assembler;
using Konamiman.Nestor80.Assembler.Output;
using Konamiman.Nestor80.Assembler.Relocatable;
using NUnit.Framework;

namespace Konamiman.Nestor80.AssemblerTests
{
    /// <summary>
    /// Tests for expressions that make forward references to LOCAL symbols of a macro,
    /// e.g. the classic M80 "length-prefixed string" idiom:
    /// the length byte (ME-MS) is emitted before the MS and ME labels are defined.
    /// </summary>
    [TestFixture]
    public class MacroLocalForwardReferencesTests
    {
        const string source = @"	.z80
LENPFX	MACRO
	LOCAL	MS,ME
	DEFB	ME-MS
MS:	DEFB	1,2,3
ME:
	ENDM

	LENPFX
	LENPFX
	end
";

        [Test]
        public void TestForwardReferencedLocalSymbolsInRelocatableByteExpression()
        {
            var result = AssemblySourceProcessor.Assemble(source, new AssemblyConfiguration() { BuildType = BuildType.Relocatable });

            Assert.IsFalse(result.HasErrors, string.Join("\r\n", result.Errors.Select(e => e.Message)));

            //Each LOCAL symbol must have been registered with a unique generated
            //"..number" name and with the address it has in its own expansion
            var localLabels = result.Symbols.Where(s => s.Name.StartsWith("..")).ToArray();
            Assert.AreEqual(4, localLabels.Length);
            CollectionAssert.AreEquivalent(new ushort[] { 1, 4, 5, 8 }, localLabels.Select(s => s.Value).ToArray());
            Assert.IsTrue(localLabels.All(s => s.ValueArea == AddressType.CSEG));

            //The DEFB ME-MS lines must have been converted to link items groups
            //(a relocatable difference stored as a byte is resolved at linking time),
            //each referencing the ME and MS addresses of its own expansion
            var expansionLines = result.ProcessedLines.OfType<MacroExpansionLine>().ToArray();
            Assert.AreEqual(2, expansionLines.Length);
            for(int i = 0; i < expansionLines.Length; i++) {
                var expansionBase = (ushort)(i * 4);
                var defbLine = expansionLines[i].Lines.OfType<DefbLine>().First();
                var linkItemsGroup = defbLine.RelocatableParts.OfType<LinkItemsGroup>().Single();
                Assert.AreEqual(3, linkItemsGroup.LinkItems.Length);

                //ME-MS in postfix order: address of ME, address of MS, minus operator
                Assert.IsTrue(linkItemsGroup.LinkItems[0].IsAddressReference);
                Assert.AreEqual((AddressType.CSEG, (ushort)(expansionBase + 4)), linkItemsGroup.LinkItems[0].GetReferencedAddress());
                Assert.IsTrue(linkItemsGroup.LinkItems[1].IsAddressReference);
                Assert.AreEqual((AddressType.CSEG, (ushort)(expansionBase + 1)), linkItemsGroup.LinkItems[1].GetReferencedAddress());
                Assert.AreEqual(ArithmeticOperatorCode.Minus, linkItemsGroup.LinkItems[2].ArithmeticOperator);
            }
        }

        [Test]
        public void TestForwardReferencedLocalSymbolsInAbsoluteByteExpression()
        {
            var result = AssemblySourceProcessor.Assemble(source, new AssemblyConfiguration() { BuildType = BuildType.Absolute });

            Assert.IsFalse(result.HasErrors, string.Join("\r\n", result.Errors.Select(e => e.Message)));

            var expansionLines = result.ProcessedLines.OfType<MacroExpansionLine>().ToArray();
            Assert.AreEqual(2, expansionLines.Length);
            foreach(var expansionLine in expansionLines) {
                var outputBytes = expansionLine.Lines.OfType<IProducesOutput>().SelectMany(l => l.OutputBytes).ToArray();
                Assert.AreEqual(new byte[] { 3, 1, 2, 3 }, outputBytes);
            }
        }
    }
}
