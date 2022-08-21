using NUnit.Framework;
using Konamiman.Nestor80.Assembler;

namespace Konamiman.Nestor80.AssemblerTests
{
    [TestFixture]
    public class ExpressionTests
    {
        static object[] TestNumberCases = {
            // Radix 10, no suffix
            new object[] { 10, "1", 1 },
            new object[] { 10, "1234", 1234 },
            new object[] { 10, "9999", 9999 },

            // Radix 10, suffixes
            new object[] { 10, "1234d", 1234 },
            new object[] { 10, "9999d", 9999 },
            new object[] { 10, "1234D", 1234 },
            new object[] { 10, "9999D", 9999 },
            new object[] { 10, "101010b", 0b101010 },
            new object[] { 10, "101010B", 0b101010 },
            new object[] { 10, "777o", 511 },
            new object[] { 10, "777O", 511 },
            new object[] { 10, "1000q", 512 },
            new object[] { 10, "1000Q", 512 },
            new object[] { 10, "12EFh", 0x12EF },
            new object[] { 10, "12efH", 0x12EF },

            // Overflow
            new object[] { 10, "99999", 0x869F }, // 99999 = 1869F
        };

        [TestCaseSource(nameof(TestNumberCases))]
        public void TestFoo(int radix, string input, int output)
        {
            Expression.DefaultRadix = radix;
            AssertParsesToNumber(input, (ushort)output);
        }

        private static void AssertParsesToNumber(string expressionString, ushort number) =>
            AssertIsNumber(Expression.Parse(expressionString), number);

        private static void AssertIsNumber(Expression expression, ushort number) =>
            AssertExpressionIs(expression, Address.Absolute(number));

        private static void AssertExpressionIs(Expression expression, params IExpressionPart[] parts) =>
            Assert.AreEqual(Expression.FromParts(parts), expression);
    }
}
