using NUnit.Framework;
using Konamiman.Nestor80.Assembler;

namespace Konamiman.Nestor80.AssemblerTests
{
    [TestFixture]
    public class ExpressionTests
    {
        [Test]
        public void TestFoo()
        {
            var ex = Expression.Parse("1");
        }
    }
}
