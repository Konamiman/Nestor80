﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Konamiman.Nestor80.Assembler
{
    internal class SourceCodeLine
    {
        public string OriginalLine { get; set; }

        public string Label { get; set; }

        public string Operator { get; set; }

        public string Arguments { get; set; }

        public Address Address { get; set; }
    }
}