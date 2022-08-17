using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Konamiman.Nestor80.Assembler
{
    internal class SymbolReference : IAssemblyOutputPart, IExpressionPart
    {
        public string SymbolName { get; set; }
    }
}
