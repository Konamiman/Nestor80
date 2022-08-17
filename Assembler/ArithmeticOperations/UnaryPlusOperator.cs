using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Konamiman.Nestor80.Assembler.ArithmeticOperations
{
    internal class UnaryPlusOperator : ArithmeticOperator
    {
        public override int Precedence => 3;

        public override string Name => "u+";

        public override bool IsUnary => true;

        protected override Address OperateCore(Address value1, Address value2)
        {
            return value1;
        }
    }
}
