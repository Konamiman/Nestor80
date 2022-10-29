using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace Konamiman.Nestor80.Assembler.Output
{
    public class MacroExpansionLine : LinesContainerLine
    {
        public MacroType MacroType { get; set; }

        public string Name { get; set; }

        public int RepetitionsCount { get; set; }

        public string[] Parameters { get; set; }

    }
}
