using Konamiman.Nestor80.Assembler;
using NUnit.Framework;

namespace Konamiman.Nestor80.AssemblerTests
{
    [TestFixture]
    public class SourceLineWalkerTests
    {
        [Test]
        [TestCase("")]
        [TestCase("  ")]
        [TestCase("\t \t")]
        public void TestBlankLine(string line)
        {
            var sut = new SourceLineWalker(line);
            Assert.IsTrue(sut.AtEndOfLine);
            Assert.IsNull(sut.ExtractSymbol());
            Assert.IsNull(sut.ExtractExpression());
            Assert.AreEqual(line.Length, sut.EffectiveLength);
        }

        [Test]
        [TestCase(";Comment", 0)]
        [TestCase("  ;Comment", 2)]
        [TestCase("\t \t ;Comment", 4)]
        public void TestCommentLine(string line, int expectedEffectiveLength)
        {
            var sut = new SourceLineWalker(line);
            Assert.IsTrue(sut.AtEndOfLine);
            Assert.IsNull(sut.ExtractSymbol());
            Assert.IsNull(sut.ExtractExpression());
            Assert.AreEqual(expectedEffectiveLength, sut.EffectiveLength);
        }

        [Test]
        [TestCase("NOP", 3)]
        [TestCase("  NOP  ", 7)]
        [TestCase("NOP;Nope", 3)]
        [TestCase("  NOP  ;Nope", 7)]
        [TestCase("\t NOP \t", 7)]
        [TestCase(" \tNOP\t ;Nope", 7)]
        public void TestSingleSymbolLine(string line, int expectedEffectiveLength)
        {
            var sut = new SourceLineWalker(line);
            Assert.IsFalse(sut.AtEndOfLine);
            Assert.AreEqual("NOP", sut.ExtractSymbol());
            Assert.IsTrue(sut.AtEndOfLine);
            Assert.IsNull(sut.ExtractSymbol());
            Assert.AreEqual(expectedEffectiveLength, sut.EffectiveLength);
        }

        [Test]
        [TestCase("RET NZ", 6)]
        [TestCase("  RET NZ  ", 10)]
        [TestCase("RET NZ;I'll be back", 6)]
        [TestCase("  RET NZ  ;I'll be back", 10)]
        [TestCase(" \tRET NZ\t ;I'll be back", 10)]
        public void TestTwoSymbolsLine(string line, int expectedEffectiveLength)
        {
            var sut = new SourceLineWalker(line);
            Assert.IsFalse(sut.AtEndOfLine);
            Assert.AreEqual("RET", sut.ExtractSymbol());
            Assert.IsFalse(sut.AtEndOfLine);
            Assert.AreEqual("NZ", sut.ExtractSymbol());
            Assert.IsTrue(sut.AtEndOfLine);
            Assert.IsNull(sut.ExtractSymbol());
            Assert.AreEqual(expectedEffectiveLength, sut.EffectiveLength);
        }
    }
}
