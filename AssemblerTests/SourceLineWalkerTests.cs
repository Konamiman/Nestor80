using Konamiman.Nestor80.Assembler;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
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

        [Test]

        //Invalid strings
        [TestCase("", null)]
        [TestCase("a", null)]

        //Empty strings
        [TestCase("<>", new[] { "" })]
        [TestCase("<   >", new[] { "" })]
        [TestCase("<\t\t\t>", new[] { "" })]

        //Single string
        [TestCase("<abc>", new[] { "abc" })]

        //Comma and space as separator,
        //extra spaces at left are ignored
        //but extra spaces at right generate empty arg
        [TestCase("<a,b>", new[] { "a", "b" })]
        [TestCase("<a b>", new[] { "a", "b" })]
        [TestCase("<a\tb>", new[] { "a", "b" })]
        [TestCase("< a  b>", new[] { "a", "b" })]
        [TestCase("<\ta\t\tb>", new[] { "a", "b" })]
        [TestCase("< a, b>", new[] { "a", "b" })]
        [TestCase("<\ta,\tb>", new[] { "a", "b" })]
        [TestCase("<a ,b>", new[] { "a", "", "b" })]
        [TestCase("<a\t,b>", new[] { "a", "", "b" })]
        [TestCase("< a , b>", new[] { "a", "", "b" })]
        [TestCase("<\ta\t,\tb>", new[] { "a", "", "b" })]

        //Empty arguments at the beginning, end and middle of the list
        [TestCase("<a,,b>", new[] { "a", "", "b" })]
        [TestCase("<a, ,b>", new[] { "a", "", "b" })]
        [TestCase("<a,\t,b>", new[] { "a", "", "b" })]
        [TestCase("< a , ,, , b>", new[] { "a", "", "", "", "", "b" })]
        [TestCase("<,,>", new[] { "", "", "" })]
        [TestCase("< , , >", new[] { "", "", "" })]
        [TestCase("<\t,\t,\t>", new[] { "", "", "" })]
        [TestCase("<a >", new[] { "a", "" })]
        [TestCase("<a\t>", new[] { "a", "" })]
        [TestCase("<a,>", new[] { "a", "" })]
        [TestCase("<a, >", new[] { "a", "" })]
        //[TestCase("<a,\t>", new[] { "a", "" })]
        [TestCase("<a, ,>", new[] { "a", "", "" })]
        [TestCase("<a,\t,>", new[] { "a", "", "" })]
        [TestCase("<, a, ,>", new[] { "", "a", "", "" })]
        [TestCase("<,\ta,\t,>", new[] { "", "a", "", "" })]
        [TestCase("< , a, ,>", new[] { "", "a", "", "" })]
        [TestCase("<\t,\ta,\t,>", new[] { "", "a", "", "" })]

        //Unterminated string
        [TestCase("<abc", new[] { "abc" })]
        [TestCase("<abc ", new[] { "abc", "" })]
        [TestCase("<a,", new[] { "a", "" })]
        [TestCase("<a, ", new[] { "a", "" })]
        [TestCase("<a, , ", new[] { "a", "", "" })]
        [TestCase("<a,,", new[] { "a", "", "" })]

        //Char literals
        [TestCase("<! ,!,,!!,!<,!>,!%>", new[] { " ", ",", "!", "<", ">", "%" })]

        //<> delimited sequences
        [TestCase("<a,<b< c,d >e> f>", new[] { "a", "b< c,d >e", "f" })]
        [TestCase("<a,<b>,c>", new[] { "a", "b", "c" })]
        [TestCase("< a ,< b< !c,d !> e > f>", new[] { "a", "", " b< c,d > e > f" })]
        [TestCase("<<a> ,b>", new[] { "a", "", "b" })]
        [TestCase("<<a><b>>", new[] { "ab" })]
        [TestCase("<<a> <b>>", new[] { "a", "b" })]

        //Expressions
        [TestCase("<a,% 1 + 1 ,b>", new[] { "a", "UNICODE_ONE 1 + 1 ", "b" })]
        [TestCase("<a,% 1 + 1>", new[] { "a", "UNICODE_ONE 1 + 1" })]
        [TestCase("< % 1 + 1 ,>", new[] { "UNICODE_ONE 1 + 1 ", "" })]
        [TestCase("< % 1 + 1 ", new[] { "UNICODE_ONE 1 + 1 " })]
        [TestCase("<%>", new[] { "UNICODE_ONE" })]
        [TestCase("<% >", new[] { "UNICODE_ONE " })]
        [TestCase("<%", new[] { "UNICODE_ONE" })]
        [TestCase("<% ", new[] { "UNICODE_ONE " })]

        //With comments
        [TestCase("a,b ;c", new[] {"a", "b"}, false)]
        [TestCase("a,b\t;c", new[] { "a", "b" }, false)]
        [TestCase("a,b;c", new[] { "a", "b" }, false)]
        [TestCase("<a,b ;c>", new[] { "a,b ;c" }, false)]

        //Strings
        [TestCase("'a,b','c'',d',\"e,\"\"f\"", new[] { "'a,b'", "'c'',d'", "\"e,\"\"f\"" }, false)]

        //A bit of everything
        [TestCase("< a, , !, ! , < a , !b > <b<c<d>>>, ef> > whatever", new[] { "a", "", ",", " ", " a , b ", "b<c<d>>", "ef" })]
        public void ExtractArgsListForIrp(string line, string[] expectedArgsList, bool requireDelimiter=true)
        {
            //Workaround for a bug in NUnit
            //(tests with arguments containing non-printable chars won't run)
            if(expectedArgsList is not null) {
                expectedArgsList = expectedArgsList
                    .Select(x => x.Replace("UNICODE_ONE", "\u0001"))
                    .ToArray();
            }

            var sut = new SourceLineWalker(line);
            var (actual, _) = sut.ExtractArgsListForIrp(requireDelimiter);
            CollectionAssert.AreEqual(expectedArgsList, actual);
        }

        [Test]
        [TestCase("'a,b','c\\',d',\"e,\"\\\"f\"", new[] { "'a,b'", "'c\\',d'", "\"e,\"\\\"f\"" })]

        public void ExtractArgsListForIrp_EscapesInStrings(string line, string[] expectedArgsList)
        {
            SourceLineWalker.AllowEscapesInStrings = true;
            var sut = new SourceLineWalker(line);
            var (actual, _) = sut.ExtractArgsListForIrp(false);
            CollectionAssert.AreEqual(expectedArgsList, actual);
        }

        [TestCase("<a>", 0)]
        [TestCase("<a", 1)]
        [TestCase("<a!>", 1)]
        [TestCase("<a<b>", 1)]
        [TestCase("<a<b<c>", 2)]
        [TestCase("<a<b<c>>", 1)]
        [TestCase("<a<b<c>>>", 0)]
        public void ExtractArgsListForIrp_MissingClosingDelimitersCounter(string line, int expectedDelimiterCounter)
        {
            var sut = new SourceLineWalker(line);
            var (_, counter) = sut.ExtractArgsListForIrp();
            Assert.AreEqual(expectedDelimiterCounter, counter);
        }

        [TestCase("", new string[0])]
        [TestCase("abc", new string[] { "a", "b", "c" })]
        [TestCase("abc de", new string[] { "a", "b", "c" })]
        [TestCase("abc\tde", new string[] { "a", "b", "c" })]
        [TestCase("<>", new string[0])]
        [TestCase("< a b c >", new string[] { " ", "a", " ", "b", " ", "c", " " })]
        [TestCase("<a<b>c>", new string[] { "a", "<", "b", ">", "c" })]
        [TestCase("<a<b>c>>", new string[] { "a", "<", "b", ">", "c" })]
        [TestCase("<a b  ", new string[] { "a", " ", "b", " ", " " })]
        public void ExtractArgsListForIrpc(string line, string[] expectedArgsList)
        {
            var sut = new SourceLineWalker(line);
            var (actual, _) = sut.ExtractArgsListForIrpc();
            CollectionAssert.AreEqual(expectedArgsList, actual);
        }

        [TestCase("", 0)]
        [TestCase("abc", 0)]
        [TestCase("<abc", 1)]
        [TestCase("<ab<c>", 1)]
        [TestCase("<a<b<c>", 2)]
        [TestCase("<a<b<c", 3)]
        public void ExtractArgsListForIrpc_MissingClosingDelimitersCounter(string line, int expectedDelimiterCounter)
        {
            var sut = new SourceLineWalker(line);
            var (_, counter) = sut.ExtractArgsListForIrpc();
            Assert.AreEqual(expectedDelimiterCounter, counter);
        }

        [Test]
        [TestCase("", "foo", "")]
        [TestCase("abcde", "foo", "abcde")]
        [TestCase("foo", "foo", "{1}")]
        [TestCase("FOO", "foo", "{1}")]
        [TestCase("a foo b", "foo", "a {1} b")]
        [TestCase("a&foo: b", "foo", "a{1}: b")]
        [TestCase("a&foo&b c", "foo", "a{1}b c")]
        [TestCase("afoo", "foo", "afoo")]
        [TestCase("fooa", "foo", "fooa")]
        [TestCase("&foo&", "foo", "{1}")]
        [TestCase("foo foo foo", "foo", "{1} {1} {1}")]
        [TestCase("&foo&&foo&&foo&", "foo", "{1}{1}{1}")]
        [TestCase(";", "foo", ";")]
        [TestCase(";foo", "foo", ";foo")]
        [TestCase("foo ;foo", "foo", "{1} ;foo")]
        [TestCase("foo foo ;foo foo", "foo", "{1} {1} ;foo foo")]
        [TestCase("foo foo 'foo &foo &foo& \"tal\" \\' foo' foo", "foo", "{1} {1} 'foo {1} {1} \"tal\" \\' foo' {1}")]
        [TestCase("foo foo \"foo &foo &foo& 'tal' \\\" foo\" foo", "foo", "{1} {1} \"foo {1} {1} 'tal' \\\" foo\" {1}")]
        public void ReplaceMacroLineArgWithPlaceholder(string line, string arg, string expectedResult)
        {
            SourceLineWalker.AllowEscapesInStrings = true;
            var actual = SourceLineWalker.ReplaceMacroLineArgWithPlaceholder(line, arg, 1);
            Assert.AreEqual(expectedResult, actual);
        }
    }
}
