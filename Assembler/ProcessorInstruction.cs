﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Konamiman.Nestor80.Assembler
{
    internal class ProcessorInstruction
    {
        public ProcessorInstruction(string instruction, string firstArgument, string secondArgument, byte[] opcodes, int valuePosition = 0, int valueSize = 0, bool isUndocumented = false)
        {
            Instruction = instruction;
            FirstArgument = firstArgument;
            SecondArgument = secondArgument;
            Opcodes = opcodes;
            ValuePosition = valuePosition;
            ValueSize = valueSize;
            IsUndocumented = isUndocumented;
        }

        public string Instruction { get; set; }

        public string FirstArgument { get; set; }

        public string SecondArgument { get; set; }

        public byte[] Opcodes { get; set; }

        public int ValuePosition { get; set; }

        public int ValueSize { get; set; }

        public bool IsUndocumented { get; set; }
    }
}
