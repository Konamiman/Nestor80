using Konamiman.Nestor80.Assembler.Infrastructure;
using NUnit.Framework;

namespace Konamiman.Nestor80.AssemblerTests
{
    [TestFixture]
    public class MacroExpansionStateTests
    {
        [Test]
        public void TestRepWithCountMacroExpansionState()
        {
            var sut = new ReptWithCountExpansionState(null, new[] { "line 1", "line 2", "line 3" }, 3, 34);
            var result = new List<string>();

            while(sut.HasMore) {
                result.Add(sut.GetNextSourceLine());
            }

            var expected = new string[] {
                "line 1", "line 2", "line 3",
                "line 1", "line 2", "line 3",
                "line 1", "line 2", "line 3"
            };

            Assert.AreEqual(expected, result.ToArray());
        }

        [Test]
        public void TestRepWithCountMacroExpansionState_ZeroRepetitions()
        {
            var sut = new ReptWithCountExpansionState(null, new[] { "line 1", "line 2", "line 3" }, 0, 34);
            Assert.IsFalse(sut.HasMore);
        }

        [Test]
        public void TestRepWithCountMacroExpansionState_RelativeLineNumber()
        {
            var sut = new ReptWithCountExpansionState(null, new[] { "line 1", "line 2", "line 3" }, 2, 34);

            Assert.AreEqual(-1, sut.RelativeLineNumber);
            sut.GetNextSourceLine();
            Assert.AreEqual(0, sut.RelativeLineNumber);
            sut.GetNextSourceLine();
            Assert.AreEqual(1, sut.RelativeLineNumber);
            sut.GetNextSourceLine();
            Assert.AreEqual(2, sut.RelativeLineNumber);
            sut.GetNextSourceLine();
            Assert.AreEqual(0, sut.RelativeLineNumber);
            sut.GetNextSourceLine();
            Assert.AreEqual(1, sut.RelativeLineNumber);
            sut.GetNextSourceLine();
            Assert.AreEqual(2, sut.RelativeLineNumber);
        }

        [Test]
        public void TestRepWithParamsMacroExpansionState()
        {
            var sut = new ReptWithParamsExpansionState(
                null,
                new[] { "the foo is {0}", "the bar is {0}", "the fizz is {0}" },
                new[] { "FOO", "BAR", "FIZZ"},
                34);
            var result = new List<string>();

            while(sut.HasMore) {
                result.Add(sut.GetNextSourceLine());
            }

            var expected = new string[] {
                "the foo is FOO",
                "the bar is FOO",
                "the fizz is FOO",
                "the foo is BAR",
                "the bar is BAR",
                "the fizz is BAR",
                "the foo is FIZZ",
                "the bar is FIZZ",
                "the fizz is FIZZ",
            };

            Assert.AreEqual(expected, result.ToArray());
        }

        [Test]
        public void TestRepWithParamsMacroExpansionState_ZeroParams()
        {
            var sut = new ReptWithParamsExpansionState(
                null,
                new[] { "the foo is {0}", "the bar is {0}", "the fizz is {0}" },
                Array.Empty<string>(),
                34);
            var result = new List<string>();

            while(sut.HasMore) {
                result.Add(sut.GetNextSourceLine());
            }

            var expected = new string[] {
                "the foo is ",
                "the bar is ",
                "the fizz is ",
            };

            Assert.AreEqual(expected, result.ToArray());
        }

        [Test]
        public void TestRepWithParamsMacroExpansionState_RelativeLineNumber()
        {
            var sut = new ReptWithParamsExpansionState(
                null,
                new[] { "the foo is {0}", "the bar is {0}", "the fizz is {0}" },
                new[] { "FOO", "BAR" },
                34);

            Assert.AreEqual(-1, sut.RelativeLineNumber);
            sut.GetNextSourceLine();
            Assert.AreEqual(0, sut.RelativeLineNumber);
            sut.GetNextSourceLine();
            Assert.AreEqual(1, sut.RelativeLineNumber);
            sut.GetNextSourceLine();
            Assert.AreEqual(2, sut.RelativeLineNumber);
            sut.GetNextSourceLine();
            Assert.AreEqual(0, sut.RelativeLineNumber);
            sut.GetNextSourceLine();
            Assert.AreEqual(1, sut.RelativeLineNumber);
            sut.GetNextSourceLine();
            Assert.AreEqual(2, sut.RelativeLineNumber);
        }

        [Test]
        public void TestNamedMacroExpansionState()
        {
            var sut = new NamedMacroExpansionState(
                "TheMacro",
                null,
                new[] { "the foo is {0} and {1}", "the bar is {2} and {3}" },
                4,
                new[] { "FOO", "BAR", "FIZZ", "BUZZ" },
                34);
            var result = new List<string>();

            while(sut.HasMore) {
                result.Add(sut.GetNextSourceLine());
            }

            var expected = new string[] {
                "the foo is FOO and BAR",
                "the bar is FIZZ and BUZZ",
            };

            Assert.AreEqual(expected, result.ToArray());
        }

        [Test]
        public void TestNamedMacroExpansionState_ExtraParams()
        {
            var sut = new NamedMacroExpansionState(
                "TheMacro",
                null,
                new[] { "the foo is {0} and {1}", "the bar is {2} and {3}" },
                4,
                new[] { "FOO", "BAR", "FIZZ", "BUZZ", "UNUSED", "ALSO_UNUSED" },
                34);
            var result = new List<string>();

            while(sut.HasMore) {
                result.Add(sut.GetNextSourceLine());
            }

            var expected = new string[] {
                "the foo is FOO and BAR",
                "the bar is FIZZ and BUZZ",
            };

            Assert.AreEqual(expected, result.ToArray());
        }

        [Test]
        public void TestNamedMacroExpansionState_TooFewParams()
        {
            var sut = new NamedMacroExpansionState(
                "TheMacro",
                null,
                new[] { "the foo is {0} and {1}", "the bar is {2} and {3}" },
                4,
                new[] { "FOO", "BAR", "FIZZ" },
                34);
            var result = new List<string>();

            while(sut.HasMore) {
                result.Add(sut.GetNextSourceLine());
            }

            var expected = new string[] {
                "the foo is FOO and BAR",
                "the bar is FIZZ and ",
            };

            Assert.AreEqual(expected, result.ToArray());
        }

        [Test]
        public void TestNamedMacroExpansionState_NoLines()
        {
            var sut = new NamedMacroExpansionState(
                "TheMacro",
                null,
                Array.Empty<string>(),
                4,
                new[] { "FOO", "BAR", "FIZZ" },
                34);

            Assert.IsFalse(sut.HasMore);
        }

        [Test]
        public void TestNamedMacroExpansionState_NoParamsDefined()
        {
            var sut = new NamedMacroExpansionState(
                "TheMacro",
                null,
                new[] { "the foo is bar", "the fizz is buzz" },
                0,
                Array.Empty<string>(),
                34);
            var result = new List<string>();

            while(sut.HasMore) {
                result.Add(sut.GetNextSourceLine());
            }

            var expected = new string[] {
                "the foo is bar",
                "the fizz is buzz",
            };

            Assert.AreEqual(expected, result.ToArray());
        }

        [Test]
        public void TestNamedMacroExpansionState_NoParamsPassed()
        {
            var sut = new NamedMacroExpansionState(
                "TheMacro",
                null,
                new[] { "the foo is {0} and {1}", "the bar is {2} and {3}" },
                4,
                Array.Empty<string>(),
                34);
            var result = new List<string>();

            while(sut.HasMore) {
                result.Add(sut.GetNextSourceLine());
            }

            var expected = new string[] {
                "the foo is  and ",
                "the bar is  and ",
            };

            Assert.AreEqual(expected, result.ToArray());
        }
    }
}
