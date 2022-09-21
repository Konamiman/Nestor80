using Konamiman.Nestor80.Assembler;
using NUnit.Framework;

namespace Konamiman.Nestor80.AssemblerTests
{
    [TestFixture]
    public class SourceLineWalkerTests
    {
        [SetUp]
        public void Setup()
        {
            SourceLineWalker.AllowEscapesInStrings = false;
        }

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

        [Test]
        public void TestExtractSymbolsAsExpressions()
        {
            var sut = new SourceLineWalker("  FO O,B AR , FIZZ  ;I'll be back");
            Assert.IsFalse(sut.AtEndOfLine);
            Assert.AreEqual("FO O", sut.ExtractExpression());
            Assert.IsFalse(sut.AtEndOfLine);
            Assert.AreEqual("B AR", sut.ExtractExpression());
            Assert.IsFalse(sut.AtEndOfLine);
            Assert.AreEqual("FIZZ", sut.ExtractExpression());
            Assert.IsTrue(sut.AtEndOfLine);
            Assert.IsNull(sut.ExtractExpression());
            Assert.AreEqual("  FO O,B AR , FIZZ  ".Length, sut.EffectiveLength);
        }

        [Test]
        public void TestExtractSrings()
        {
            var sut = new SourceLineWalker(@" ""Hi, I'm """"good"""" today."" , 'Hi, I''m not bad.' ;Cool! ");
            Assert.IsFalse(sut.AtEndOfLine);
            Assert.AreEqual(@"""Hi, I'm """"good"""" today.""", sut.ExtractExpression());
            Assert.IsFalse(sut.AtEndOfLine);
            Assert.AreEqual("'Hi, I''m not bad.'", sut.ExtractExpression());
            Assert.IsTrue(sut.AtEndOfLine);
        }

        [Test]
        [TestCase("FOO,")]
        [TestCase("  FOO , ")]
        public void TestStrayComma(string line)
        {
            var sut = new SourceLineWalker(line);
            Assert.IsFalse(sut.AtEndOfLine);
            Assert.AreEqual("FOO", sut.ExtractExpression());
            Assert.IsFalse(sut.AtEndOfLine);
            Assert.AreEqual("", sut.ExtractExpression());
            Assert.IsTrue(sut.AtEndOfLine);
        }

        [Test]
        [TestCase(@"'Hello \', friend.'", @"'Hello \'")]
        [TestCase(@"""Hello \"", friend.'""", @"""Hello \""")]
        public void TestUnsupportedEscapes(string line, string expectedString)
        {
            var sut = new SourceLineWalker(line);
            Assert.AreEqual(expectedString, sut.ExtractExpression());
        }

        [Test]
        [TestCase(@"'Hello \', friend.',12", @"'Hello \', friend.'")]
        [TestCase(@"""Hello \"", friend."",34", @"""Hello \"", friend.""")]
        public void TestSupportedEscapes(string line, string expectedString)
        {
            SourceLineWalker.AllowEscapesInStrings = true;
            var sut = new SourceLineWalker(line);
            Assert.AreEqual(expectedString, sut.ExtractExpression());
        }

        [Test]
        [TestCase("", null)]
        [TestCase("foo", null)]
        [TestCase("<foo", null)]
        [TestCase("<>", "")]
        [TestCase("< >", " ")]
        [TestCase("<,>", ",")]
        public void TestExtractAngleBracketed(string line, string expectedString)
        {
            var sut = new SourceLineWalker(line);
            Assert.AreEqual(expectedString, sut.ExtractAngleBracketed());
        }

        [Test]
        public void TestExtractMultipleAngleBracketed()
        {
            var sut = new SourceLineWalker("<abc>, <def> ;Foo");
            Assert.AreEqual("abc", sut.ExtractAngleBracketed());
            Assert.IsTrue(sut.SkipComma());
            Assert.AreEqual("def", sut.ExtractAngleBracketed());
            Assert.True(sut.AtEndOfLine);
        }

        [Test]
        [TestCase("", null)]
        [TestCase("Foo Bar", "Foo")]
        [TestCase(@"""Hola que tal"" yo bien", "Hola que tal")]
        [TestCase(@"""Hola que tal """"estamos"""" eh"" yo bien", @"Hola que tal ""estamos"" eh")]
        [TestCase(@"""Bla""""", @"Bla""")]
        [TestCase(@"""Bla"""""" blo", @"Bla""")]
        [TestCase(@"""Bla", "Bla")]
        public void TestExtractFilename(string line, string expectedString)
        {
            var sut = new SourceLineWalker(line);
            Assert.AreEqual(expectedString, sut.ExtractFileName());
        }
    }
}
