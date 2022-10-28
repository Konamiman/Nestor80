namespace Konamiman.Nestor80.Assembler
{
    public partial class AssemblySourceProcessor
    {
        /**
         * Note: Instructions having (IX) or (IY) as argument are declared separately from (IX+n) and (IY+n)
         * to simplify processing, but of course they are equivalent to their (IX+0) and (IY+0) equivalents.
         */

        static readonly string[] Z80InstructionOpcodes = new[] {
            "ADC","ADD","AND","BIT","CALL","CCF","CP","CPD","CPDR","CPI","CPIR","CPL",
            "DAA","DEC","DI","DJNZ","EI","EX","EXX","HALT","IM","IN","INC","IND","INDR","INI","INIR",
            "JP","JR","LD","LDD","LDDR","LDI","LDIR","NEG","NOP","OR","OTDR","OTIR","OUT","OUTD","OUTI",
            "POP","PUSH","RES","RET","RETI","RETN","RL","RLA","RLC","RLCA","RLD","RR","RRA","RRC","RRCA","RRD","RST",
            "SBC","SCF","SET","SLA","SRA","SRL","SUB","XOR","SLL"
        };

        static readonly string[] R800SpecificOpcodes = new[] { "MULUB", "MULUW" };

        /// <summary>
        /// Instructions that have no argument or that have one or two fixed (register or flag names) arguments.
        /// </summary>
        static readonly Dictionary<string, byte[]> FixedZ80Instructions = new(StringComparer.OrdinalIgnoreCase) {
          { "ADC HL,BC", new byte[] { 0xed, 0x4a } },
          { "ADC HL,DE", new byte[] { 0xed, 0x5a } },
          { "ADC HL,HL", new byte[] { 0xed, 0x6a } },
          { "ADC HL,SP", new byte[] { 0xed, 0x7a } },
          { "ADC A,(HL)", new byte[] { 0x8e } },
          { "ADC A,(IX)", new byte[] { 0xdd, 0x8e, 0 } },
          { "ADC A,(IY)", new byte[] { 0xfd, 0x8e, 0 } },
          { "ADC A,A", new byte[] { 0x8f } },
          { "ADC A,B", new byte[] { 0x88 } },
          { "ADC A,C", new byte[] { 0x89 } },
          { "ADC A,D", new byte[] { 0x8a } },
          { "ADC A,E", new byte[] { 0x8b } },
          { "ADC A,H", new byte[] { 0x8c } },
          { "ADC A,L", new byte[] { 0x8d } },
          { "ADC A,IXH", new byte[] { 0xdd, 0x8c } },
          { "ADC A,IXL", new byte[] { 0xdd, 0x8d } },
          { "ADC A,IYH", new byte[] { 0xfd, 0x8c } },
          { "ADC A,IYL", new byte[] { 0xfd, 0x8d } },

          //Aliases for "ADC A,..." with implicit A
          { "ADC (HL)", new byte[] { 0x8e } },
          { "ADC (IX)", new byte[] { 0xdd, 0x8e, 0 } },
          { "ADC (IY)", new byte[] { 0xfd, 0x8e, 0 } },
          { "ADC A", new byte[] { 0x8f } },
          { "ADC B", new byte[] { 0x88 } },
          { "ADC C", new byte[] { 0x89 } },
          { "ADC D", new byte[] { 0x8a } },
          { "ADC E", new byte[] { 0x8b } },
          { "ADC H", new byte[] { 0x8c } },
          { "ADC L", new byte[] { 0x8d } },
          { "ADC IXH", new byte[] { 0xdd, 0x8c } },
          { "ADC IXL", new byte[] { 0xdd, 0x8d } },
          { "ADC IYH", new byte[] { 0xfd, 0x8c } },
          { "ADC IYL", new byte[] { 0xfd, 0x8d } },

          { "ADD HL,BC", new byte[] { 0x09 } },
          { "ADD HL,DE", new byte[] { 0x19 } },
          { "ADD HL,HL", new byte[] { 0x29 } },
          { "ADD HL,SP", new byte[] { 0x39 } },
          { "ADD IX,BC", new byte[] { 0xdd, 0x09 } },
          { "ADD IX,DE", new byte[] { 0xdd, 0x19 } },
          { "ADD IX,IX", new byte[] { 0xdd, 0x29 } },
          { "ADD IX,SP", new byte[] { 0xdd, 0x39 } },
          { "ADD IY,BC", new byte[] { 0xfd, 0x09 } },
          { "ADD IY,DE", new byte[] { 0xfd, 0x19 } },
          { "ADD IY,IY", new byte[] { 0xfd, 0x29 } },
          { "ADD IY,SP", new byte[] { 0xfd, 0x39 } },
          { "ADD A,(HL)", new byte[] { 0x86 } },
          { "ADD A,(IX)", new byte[] { 0xdd, 0x86, 0 } },
          { "ADD A,(IY)", new byte[] { 0xfd, 0x86, 0 } },
          { "ADD A,A", new byte[] { 0x87 } },
          { "ADD A,B", new byte[] { 0x80 } },
          { "ADD A,C", new byte[] { 0x81 } },
          { "ADD A,D", new byte[] { 0x82 } },
          { "ADD A,E", new byte[] { 0x83 } },
          { "ADD A,H", new byte[] { 0x84 } },
          { "ADD A,L", new byte[] { 0x85 } },
          { "ADD A,IXH", new byte[] { 0xdd, 0x84 } },
          { "ADD A,IXL", new byte[] { 0xdd, 0x85 } },
          { "ADD A,IYH", new byte[] { 0xfd, 0x84 } },
          { "ADD A,IYL", new byte[] { 0xfd, 0x85 } },

          //Aliases for "ADD A,..." with implicit A
          { "ADD (HL)", new byte[] { 0x86 } },
          { "ADD (IX)", new byte[] { 0xdd, 0x86, 0 } },
          { "ADD (IY)", new byte[] { 0xfd, 0x86, 0 } },
          { "ADD A", new byte[] { 0x87 } },
          { "ADD B", new byte[] { 0x80 } },
          { "ADD C", new byte[] { 0x81 } },
          { "ADD D", new byte[] { 0x82 } },
          { "ADD E", new byte[] { 0x83 } },
          { "ADD H", new byte[] { 0x84 } },
          { "ADD L", new byte[] { 0x85 } },
          { "ADD IXH", new byte[] { 0xdd, 0x84 } },
          { "ADD IXL", new byte[] { 0xdd, 0x85 } },
          { "ADD IYH", new byte[] { 0xfd, 0x84 } },
          { "ADD IYL", new byte[] { 0xfd, 0x85 } },

          { "AND (HL)", new byte[] { 0xa6 } },
          { "AND (IX)", new byte[] { 0xdd, 0xa6, 0 } },
          { "AND (IY)", new byte[] { 0xfd, 0xa6, 0 } },
          { "AND A", new byte[] { 0xa7 } },
          { "AND B", new byte[] { 0xa0 } },
          { "AND C", new byte[] { 0xa1 } },
          { "AND D", new byte[] { 0xa2 } },
          { "AND E", new byte[] { 0xa3 } },
          { "AND H", new byte[] { 0xa4 } },
          { "AND L", new byte[] { 0xa5 } },
          { "AND IXH", new byte[] { 0xdd, 0xa4 } },
          { "AND IXL", new byte[] { 0xdd, 0xa5 } },
          { "AND IYH", new byte[] { 0xfd, 0xa4 } },
          { "AND IYL", new byte[] { 0xfd, 0xa5 } },

          { "BIT 0,(HL)", new byte[] { 0xcb, 0x46 } },
          { "BIT 0,(IX)", new byte[] { 0xdd, 0xcb, 0, 0x46 } },
          { "BIT 0,(IY)", new byte[] { 0xfd, 0xcb, 0, 0x46 } },
          { "BIT 0,A", new byte[] { 0xcb, 0x47 } },
          { "BIT 0,B", new byte[] { 0xcb, 0x40 } },
          { "BIT 0,C", new byte[] { 0xcb, 0x41 } },
          { "BIT 0,D", new byte[] { 0xcb, 0x42 } },
          { "BIT 0,E", new byte[] { 0xcb, 0x43 } },
          { "BIT 0,H", new byte[] { 0xcb, 0x44 } },
          { "BIT 0,L", new byte[] { 0xcb, 0x45 } },
          { "BIT 1,(HL)", new byte[] { 0xcb, 0x4e } },
          { "BIT 1,(IX)", new byte[] { 0xdd, 0xcb, 0, 0x4e } },
          { "BIT 1,(IY)", new byte[] { 0xfd, 0xcb, 0, 0x4e } },
          { "BIT 1,A", new byte[] { 0xcb, 0x4f } },
          { "BIT 1,B", new byte[] { 0xcb, 0x48 } },
          { "BIT 1,C", new byte[] { 0xcb, 0x49 } },
          { "BIT 1,D", new byte[] { 0xcb, 0x4a } },
          { "BIT 1,E", new byte[] { 0xcb, 0x4b } },
          { "BIT 1,H", new byte[] { 0xcb, 0x4c } },
          { "BIT 1,L", new byte[] { 0xcb, 0x4d } },
          { "BIT 2,(HL)", new byte[] { 0xcb, 0x56 } },
          { "BIT 2,(IX)", new byte[] { 0xdd, 0xcb, 0, 0x56 } },
          { "BIT 2,(IY)", new byte[] { 0xfd, 0xcb, 0, 0x56 } },
          { "BIT 2,A", new byte[] { 0xcb, 0x57 } },
          { "BIT 2,B", new byte[] { 0xcb, 0x50 } },
          { "BIT 2,C", new byte[] { 0xcb, 0x51 } },
          { "BIT 2,D", new byte[] { 0xcb, 0x52 } },
          { "BIT 2,E", new byte[] { 0xcb, 0x53 } },
          { "BIT 2,H", new byte[] { 0xcb, 0x54 } },
          { "BIT 2,L", new byte[] { 0xcb, 0x55 } },
          { "BIT 3,(HL)", new byte[] { 0xcb, 0x5e } },
          { "BIT 3,(IX)", new byte[] { 0xdd, 0xcb, 0, 0x5e } },
          { "BIT 3,(IY)", new byte[] { 0xfd, 0xcb, 0, 0x5e } },
          { "BIT 3,A", new byte[] { 0xcb, 0x5f } },
          { "BIT 3,B", new byte[] { 0xcb, 0x58 } },
          { "BIT 3,C", new byte[] { 0xcb, 0x59 } },
          { "BIT 3,D", new byte[] { 0xcb, 0x5a } },
          { "BIT 3,E", new byte[] { 0xcb, 0x5b } },
          { "BIT 3,H", new byte[] { 0xcb, 0x5c } },
          { "BIT 3,L", new byte[] { 0xcb, 0x5d } },
          { "BIT 4,(HL)", new byte[] { 0xcb, 0x66 } },
          { "BIT 4,(IX)", new byte[] { 0xdd, 0xcb, 0, 0x66 } },
          { "BIT 4,(IY)", new byte[] { 0xfd, 0xcb, 0, 0x66 } },
          { "BIT 4,A", new byte[] { 0xcb, 0x67 } },
          { "BIT 4,B", new byte[] { 0xcb, 0x60 } },
          { "BIT 4,C", new byte[] { 0xcb, 0x61 } },
          { "BIT 4,D", new byte[] { 0xcb, 0x62 } },
          { "BIT 4,E", new byte[] { 0xcb, 0x63 } },
          { "BIT 4,H", new byte[] { 0xcb, 0x64 } },
          { "BIT 4,L", new byte[] { 0xcb, 0x65 } },
          { "BIT 5,(HL)", new byte[] { 0xcb, 0x6e } },
          { "BIT 5,(IX)", new byte[] { 0xdd, 0xcb, 0, 0x6e } },
          { "BIT 5,(IY)", new byte[] { 0xfd, 0xcb, 0, 0x6e } },
          { "BIT 5,A", new byte[] { 0xcb, 0x6f } },
          { "BIT 5,B", new byte[] { 0xcb, 0x68 } },
          { "BIT 5,C", new byte[] { 0xcb, 0x69 } },
          { "BIT 5,D", new byte[] { 0xcb, 0x6a } },
          { "BIT 5,E", new byte[] { 0xcb, 0x6b } },
          { "BIT 5,H", new byte[] { 0xcb, 0x6c } },
          { "BIT 5,L", new byte[] { 0xcb, 0x6d } },
          { "BIT 6,(HL)", new byte[] { 0xcb, 0x76 } },
          { "BIT 6,(IX)", new byte[] { 0xdd, 0xcb, 0, 0x76 } },
          { "BIT 6,(IY)", new byte[] { 0xfd, 0xcb, 0, 0x76 } },
          { "BIT 6,A", new byte[] { 0xcb, 0x77 } },
          { "BIT 6,B", new byte[] { 0xcb, 0x70 } },
          { "BIT 6,C", new byte[] { 0xcb, 0x71 } },
          { "BIT 6,D", new byte[] { 0xcb, 0x72 } },
          { "BIT 6,E", new byte[] { 0xcb, 0x73 } },
          { "BIT 6,H", new byte[] { 0xcb, 0x74 } },
          { "BIT 6,L", new byte[] { 0xcb, 0x75 } },
          { "BIT 7,(HL)", new byte[] { 0xcb, 0x7e } },
          { "BIT 7,(IX)", new byte[] { 0xdd, 0xcb, 0, 0x7e } },
          { "BIT 7,(IY)", new byte[] { 0xfd, 0xcb, 0, 0x7e } },
          { "BIT 7,A", new byte[] { 0xcb, 0x7f } },
          { "BIT 7,B", new byte[] { 0xcb, 0x78 } },
          { "BIT 7,C", new byte[] { 0xcb, 0x79 } },
          { "BIT 7,D", new byte[] { 0xcb, 0x7a } },
          { "BIT 7,E", new byte[] { 0xcb, 0x7b } },
          { "BIT 7,H", new byte[] { 0xcb, 0x7c } },
          { "BIT 7,L", new byte[] { 0xcb, 0x7d } },

          { "CCF", new byte[] { 0x3f } },

          { "CP (HL)", new byte[] { 0xbe } },
          { "CP (IX)", new byte[] { 0xdd, 0xbe, 0 } },
          { "CP (IY)", new byte[] { 0xfd, 0xbe, 0 } },
          { "CP A", new byte[] { 0xbf } },
          { "CP B", new byte[] { 0xb8 } },
          { "CP C", new byte[] { 0xb9 } },
          { "CP D", new byte[] { 0xba } },
          { "CP E", new byte[] { 0xbb } },
          { "CP H", new byte[] { 0xbc } },
          { "CP L", new byte[] { 0xbd } },
          { "CP IXH", new byte[] { 0xdd, 0xbc } },
          { "CP IXL", new byte[] { 0xdd, 0xbd } },
          { "CP IYH", new byte[] { 0xfd, 0xbc } },
          { "CP IYL", new byte[] { 0xfd, 0xbd } },

          { "CPD", new byte[] { 0xed, 0xa9 } },

          { "CPDR", new byte[] { 0xed, 0xb9 } },

          { "CPI", new byte[] { 0xed, 0xa1 } },

          { "CPIR", new byte[] { 0xed, 0xb1 } },

          { "CPL", new byte[] { 0x2f } },

          { "DAA", new byte[] { 0x27 } },

          { "DEC (HL)", new byte[] { 0x35 } },
          { "DEC (IX)", new byte[] { 0xdd, 0x35, 0 } },
          { "DEC (IY)", new byte[] { 0xfd, 0x35, 0 } },
          { "DEC A", new byte[] { 0x3d } },
          { "DEC B", new byte[] { 0x05 } },
          { "DEC BC", new byte[] { 0x0b } },
          { "DEC C", new byte[] { 0x0d } },
          { "DEC D", new byte[] { 0x15 } },
          { "DEC DE", new byte[] { 0x1b } },
          { "DEC E", new byte[] { 0x1d } },
          { "DEC H", new byte[] { 0x25 } },
          { "DEC HL", new byte[] { 0x2b } },
          { "DEC IX", new byte[] { 0xdd, 0x2b } },
          { "DEC IY", new byte[] { 0xfd, 0x2b } },
          { "DEC L", new byte[] { 0x2d } },
          { "DEC SP", new byte[] { 0x3b } },
          { "DEC IXH", new byte[] { 0xdd, 0x25 }},
          { "DEC IXL", new byte[] { 0xdd, 0x2d }},
          { "DEC IYH", new byte[] { 0xfd, 0x25 }},
          { "DEC IYL", new byte[] { 0xfd, 0x2d }},

          { "DI", new byte[] { 0xf3 } },

          { "EI", new byte[] { 0xfb } },

          { "EX (SP),HL", new byte[] { 0xe3 } },
          { "EX (SP),IX", new byte[] { 0xdd, 0xe3 } },
          { "EX (SP),IY", new byte[] { 0xfd, 0xe3 } },
          { "EX AF,AF", new byte[] { 0x08 } },
          { "EX DE,HL", new byte[] { 0xeb } },

          { "EXX", new byte[] { 0xd9 } },

          { "HALT", new byte[] { 0x76 } },

          { "IM 0", new byte[] { 0xed, 0x46 } },
          { "IM 1", new byte[] { 0xed, 0x56 } },
          { "IM 2", new byte[] { 0xed, 0x5e } },

          { "IN A,(C)", new byte[] { 0xed, 0x78 } },
          { "IN B,(C)", new byte[] { 0xed, 0x40 } },
          { "IN C,(C)", new byte[] { 0xed, 0x48 } },
          { "IN D,(C)", new byte[] { 0xed, 0x50 } },
          { "IN E,(C)", new byte[] { 0xed, 0x58 } },
          { "IN H,(C)", new byte[] { 0xed, 0x60 } },
          { "IN L,(C)", new byte[] { 0xed, 0x68 } },
          { "IN F,(C)", new byte[] { 0xed, 0x70 } },

          { "INC (HL)", new byte[] { 0x34 } },
          { "INC (IX)", new byte[] { 0xdd, 0x34, 0 } },
          { "INC (IY)", new byte[] { 0xfd, 0x34, 0 } },
          { "INC A", new byte[] { 0x3c } },
          { "INC B", new byte[] { 0x04 } },
          { "INC BC", new byte[] { 0x03 } },
          { "INC C", new byte[] { 0x0c } },
          { "INC D", new byte[] { 0x14 } },
          { "INC DE", new byte[] { 0x13 } },
          { "INC E", new byte[] { 0x1c } },
          { "INC H", new byte[] { 0x24 } },
          { "INC HL", new byte[] { 0x23 } },
          { "INC IX", new byte[] { 0xdd, 0x23 } },
          { "INC IY", new byte[] { 0xfd, 0x23 } },
          { "INC L", new byte[] { 0x2c } },
          { "INC SP", new byte[] { 0x33 } },
          { "INC IXH", new byte[] { 0xdd, 0x24 } },
          { "INC IXL", new byte[] { 0xdd, 0x2c } },
          { "INC IYH", new byte[] { 0xfd, 0x24 } },
          { "INC IYL", new byte[] { 0xfd, 0x2c } },

          { "IND", new byte[] { 0xed, 0xaa } },

          { "INDR", new byte[] { 0xed, 0xba } },

          { "INI", new byte[] { 0xed, 0xa2 } },

          { "INIR", new byte[] { 0xed, 0xb2 } },

          { "JP (HL)", new byte[] { 0xe9 } },
          { "JP (IX)", new byte[] { 0xdd, 0xe9 } },
          { "JP (IY)", new byte[] { 0xfd, 0xe9 } },

          { "LD (BC),A", new byte[] { 0x02 } },
          { "LD (DE),A", new byte[] { 0x12 } },
          { "LD (HL),A", new byte[] { 0x77 } },
          { "LD (HL),B", new byte[] { 0x70 } },
          { "LD (HL),C", new byte[] { 0x71 } },
          { "LD (HL),D", new byte[] { 0x72 } },
          { "LD (HL),E", new byte[] { 0x73 } },
          { "LD (HL),H", new byte[] { 0x74 } },
          { "LD (HL),L", new byte[] { 0x75 } },
          { "LD (IX),A", new byte[] { 0xdd, 0x77, 0 } },
          { "LD (IX),B", new byte[] { 0xdd, 0x70, 0 } },
          { "LD (IX),C", new byte[] { 0xdd, 0x71, 0 } },
          { "LD (IX),D", new byte[] { 0xdd, 0x72, 0 } },
          { "LD (IX),E", new byte[] { 0xdd, 0x73, 0 } },
          { "LD (IX),H", new byte[] { 0xdd, 0x74, 0 } },
          { "LD (IX),L", new byte[] { 0xdd, 0x75, 0 } },
          { "LD (IY),A", new byte[] { 0xfd, 0x77, 0 } },
          { "LD (IY),B", new byte[] { 0xfd, 0x70, 0 } },
          { "LD (IY),C", new byte[] { 0xfd, 0x71, 0 } },
          { "LD (IY),D", new byte[] { 0xfd, 0x72, 0 } },
          { "LD (IY),E", new byte[] { 0xfd, 0x73, 0 } },
          { "LD (IY),H", new byte[] { 0xfd, 0x74, 0 } },
          { "LD (IY),L", new byte[] { 0xfd, 0x75, 0 } },
          { "LD A,(BC)", new byte[] { 0x0a } },
          { "LD A,(DE)", new byte[] { 0x1a } },
          { "LD A,(HL)", new byte[] { 0x7e } },
          { "LD A,(IX)", new byte[] { 0xdd, 0x7e, 0 } },
          { "LD A,(IY)", new byte[] { 0xfd, 0x7e, 0 } },
          { "LD A,A", new byte[] { 0x7f } },
          { "LD A,B", new byte[] { 0x78 } },
          { "LD A,C", new byte[] { 0x79 } },
          { "LD A,D", new byte[] { 0x7a } },
          { "LD A,E", new byte[] { 0x7b } },
          { "LD A,H", new byte[] { 0x7c } },
          { "LD A,I", new byte[] { 0xed, 0x57 } },
          { "LD A,R", new byte[] { 0xed, 0x5F } },
          { "LD A,L", new byte[] { 0x7d } },
          { "LD B,(HL)", new byte[] { 0x46 } },
          { "LD B,(IX)", new byte[] { 0xdd, 0x46, 0 } },
          { "LD B,(IY)", new byte[] { 0xfd, 0x46, 0 } },
          { "LD B,A", new byte[] { 0x47 } },
          { "LD B,B", new byte[] { 0x40 } },
          { "LD B,C", new byte[] { 0x41 } },
          { "LD B,D", new byte[] { 0x42 } },
          { "LD B,E", new byte[] { 0x43 } },
          { "LD B,H", new byte[] { 0x44 } },
          { "LD B,L", new byte[] { 0x45 } },
          { "LD C,(HL)", new byte[] { 0x4e } },
          { "LD C,(IX)", new byte[] { 0xdd, 0x4e, 0 } },
          { "LD C,(IY)", new byte[] { 0xfd, 0x4e, 0 } },
          { "LD C,A", new byte[] { 0x4f } },
          { "LD C,B", new byte[] { 0x48 } },
          { "LD C,C", new byte[] { 0x49 } },
          { "LD C,D", new byte[] { 0x4a } },
          { "LD C,E", new byte[] { 0x4b } },
          { "LD C,H", new byte[] { 0x4c } },
          { "LD C,L", new byte[] { 0x4d } },
          { "LD D,(HL)", new byte[] { 0x56 } },
          { "LD D,(IX)", new byte[] { 0xdd, 0x56, 0 } },
          { "LD D,(IY)", new byte[] { 0xfd, 0x56, 0 } },
          { "LD D,A", new byte[] { 0x57 } },
          { "LD D,B", new byte[] { 0x50 } },
          { "LD D,C", new byte[] { 0x51 } },
          { "LD D,D", new byte[] { 0x52 } },
          { "LD D,E", new byte[] { 0x53 } },
          { "LD D,H", new byte[] { 0x54 } },
          { "LD D,L", new byte[] { 0x55 } },
          { "LD E,(HL)", new byte[] { 0x5e } },
          { "LD E,(IX)", new byte[] { 0xdd, 0x5e, 0 } },
          { "LD E,(IY)", new byte[] { 0xfd, 0x5e, 0 } },
          { "LD E,A", new byte[] { 0x5f } },
          { "LD E,B", new byte[] { 0x58 } },
          { "LD E,C", new byte[] { 0x59 } },
          { "LD E,D", new byte[] { 0x5a } },
          { "LD E,E", new byte[] { 0x5b } },
          { "LD E,H", new byte[] { 0x5c } },
          { "LD E,L", new byte[] { 0x5d } },
          { "LD H,(HL)", new byte[] { 0x66 } },
          { "LD H,(IX)", new byte[] { 0xdd, 0x66, 0 } },
          { "LD H,(IY)", new byte[] { 0xfd, 0x66, 0 } },
          { "LD H,A", new byte[] { 0x67 } },
          { "LD H,B", new byte[] { 0x60 } },
          { "LD H,C", new byte[] { 0x61 } },
          { "LD H,D", new byte[] { 0x62 } },
          { "LD H,E", new byte[] { 0x63 } },
          { "LD H,H", new byte[] { 0x64 } },
          { "LD H,L", new byte[] { 0x65 } },
          { "LD I,A", new byte[] { 0xed, 0x47 } },
          { "LD L,(HL)", new byte[] { 0x6e } },
          { "LD L,(IX)", new byte[] { 0xdd, 0x6e, 0 } },
          { "LD L,(IY)", new byte[] { 0xfd, 0x6e, 0 } },
          { "LD L,A", new byte[] { 0x6f } },
          { "LD L,B", new byte[] { 0x68 } },
          { "LD L,C", new byte[] { 0x69 } },
          { "LD L,D", new byte[] { 0x6a } },
          { "LD L,E", new byte[] { 0x6b } },
          { "LD L,H", new byte[] { 0x6c } },
          { "LD L,L", new byte[] { 0x6d } },
          { "LD SP,HL", new byte[] { 0xf9 } },
          { "LD SP,IX", new byte[] { 0xdd, 0xf9 } },
          { "LD SP,IY", new byte[] { 0xfd, 0xf9 } },
          { "LD B,IXH", new byte[] { 0xdd, 0x44 } },
          { "LD B,IXL", new byte[] { 0xdd, 0x45 } },
          { "LD C,IXH", new byte[] { 0xdd, 0x4c } },
          { "LD C,IXL", new byte[] { 0xdd, 0x4d } },
          { "LD D,IXH", new byte[] { 0xdd, 0x54 } },
          { "LD D,IXL", new byte[] { 0xdd, 0x55 } },
          { "LD E,IXH", new byte[] { 0xdd, 0x5c } },
          { "LD E,IXL", new byte[] { 0xdd, 0x5d } },
          { "LD IXH,B", new byte[] { 0xdd, 0x60 } },
          { "LD IXH,C", new byte[] { 0xdd, 0x61 } },
          { "LD IXH,D", new byte[] { 0xdd, 0x62 } },
          { "LD IXH,E", new byte[] { 0xdd, 0x63 } },
          { "LD IXH,IXH", new byte[] { 0xdd, 0x64 } },
          { "LD IXH,IXL", new byte[] { 0xdd, 0x65 } },
          { "LD IXH,A", new byte[] { 0xdd, 0x67 } },
          { "LD IXL,B", new byte[] { 0xdd, 0x68 } },
          { "LD IXL,C", new byte[] { 0xdd, 0x69 } },
          { "LD IXL,D", new byte[] { 0xdd, 0x6a } },
          { "LD IXL,E", new byte[] { 0xdd, 0x6b } },
          { "LD IXL,IXH", new byte[] { 0xdd, 0x6c } },
          { "LD IXL,IXL", new byte[] { 0xdd, 0x6d } },
          { "LD IXL,A", new byte[] { 0xdd, 0x6f } },
          { "LD A,IXH", new byte[] { 0xdd, 0x7c } },
          { "LD A,IXL", new byte[] { 0xdd, 0x7d } },
          { "LD B,IYH", new byte[] { 0xfd, 0x44 } },
          { "LD B,IYL", new byte[] { 0xfd, 0x45 } },
          { "LD C,IYH", new byte[] { 0xfd, 0x4c } },
          { "LD C,IYL", new byte[] { 0xfd, 0x4d } },
          { "LD D,IYH", new byte[] { 0xfd, 0x54 } },
          { "LD D,IYL", new byte[] { 0xfd, 0x55 } },
          { "LD E,IYH", new byte[] { 0xfd, 0x5c } },
          { "LD E,IYL", new byte[] { 0xfd, 0x5d } },
          { "LD IYH,B", new byte[] { 0xfd, 0x60 } },
          { "LD IYH,C", new byte[] { 0xfd, 0x61 } },
          { "LD IYH,D", new byte[] { 0xfd, 0x62 } },
          { "LD IYH,E", new byte[] { 0xfd, 0x63 } },
          { "LD IYH,IYH", new byte[] { 0xfd, 0x64 } },
          { "LD IYH,IYL", new byte[] { 0xfd, 0x65 } },
          { "LD IYH,A", new byte[] { 0xfd, 0x67 } },
          { "LD IYL,B", new byte[] { 0xfd, 0x68 } },
          { "LD IYL,C", new byte[] { 0xfd, 0x69 } },
          { "LD IYL,D", new byte[] { 0xfd, 0x6a } },
          { "LD IYL,E", new byte[] { 0xfd, 0x6b } },
          { "LD IYL,IYH", new byte[] { 0xfd, 0x6c } },
          { "LD IYL,IYL", new byte[] { 0xfd, 0x6d } },
          { "LD IYL,A", new byte[] { 0xfd, 0x6f } },
          { "LD A,IYH", new byte[] { 0xfd, 0x7c } },
          { "LD A,IYL", new byte[] { 0xfd, 0x7d } },

          { "LDD", new byte[] { 0xed, 0xa8 } },

          { "LDDR", new byte[] { 0xed, 0xb8 } },

          { "LDI", new byte[] { 0xed, 0xa0 } },

          { "LDIR", new byte[] { 0xed, 0xb0 } },

          { "NEG", new byte[] { 0xed, 0x44 } },

          { "NOP", new byte[] { 0x00 } },

          { "OR (HL)", new byte[] { 0xb6 } },
          { "OR (IX)", new byte[] { 0xdd, 0xb6, 0 } },
          { "OR (IY)", new byte[] { 0xfd, 0xb6, 0 } },
          { "OR A", new byte[] { 0xb7 } },
          { "OR B", new byte[] { 0xb0 } },
          { "OR C", new byte[] { 0xb1 } },
          { "OR D", new byte[] { 0xb2 } },
          { "OR E", new byte[] { 0xb3 } },
          { "OR H", new byte[] { 0xb4 } },
          { "OR L", new byte[] { 0xb5 } },
          { "OR IXH", new byte[] { 0xdd, 0xb4 } },
          { "OR IXL", new byte[] { 0xdd, 0xb5 } },
          { "OR IYH", new byte[] { 0xfd, 0xb4 } },
          { "OR IYL", new byte[] { 0xfd, 0xb5 } },

          { "OTDR", new byte[] { 0xed, 0xbb } } ,

          { "OTIR", new byte[] { 0xed, 0xb3 } } ,

          { "OUT (C),A", new byte[] { 0xed, 0x79 } },
          { "OUT (C),B", new byte[] { 0xed, 0x41 } },
          { "OUT (C),C", new byte[] { 0xed, 0x49 } },
          { "OUT (C),D", new byte[] { 0xed, 0x51 } },
          { "OUT (C),E", new byte[] { 0xed, 0x59 } },
          { "OUT (C),H", new byte[] { 0xed, 0x61 } },
          { "OUT (C),L", new byte[] { 0xed, 0x69 } },
          { "OUT F,(C)", new byte[] { 0xed, 0x71 } },

          { "OUTD", new byte[] { 0xed, 0xab } },

          { "OUTI", new byte[] { 0xed, 0xa3 } },

          { "POP AF", new byte[] { 0xf1 } },
          { "POP BC", new byte[] { 0xc1 } },
          { "POP DE", new byte[] { 0xd1 } },
          { "POP HL", new byte[] { 0xe1 } },
          { "POP IX", new byte[] { 0xdd, 0xe1 } },
          { "POP IY", new byte[] { 0xfd, 0xe1 } },

          { "PUSH AF", new byte[] { 0xf5 } },
          { "PUSH BC", new byte[] { 0xc5 } },
          { "PUSH DE", new byte[] { 0xd5 } },
          { "PUSH HL", new byte[] { 0xe5 } },
          { "PUSH IX", new byte[] { 0xdd, 0xe5 } },
          { "PUSH IY", new byte[] { 0xfd, 0xe5 } },

          { "RET", new byte[] { 0xc9 } },
          { "RET C", new byte[] { 0xd8 } },
          { "RET M", new byte[] { 0xf8 } },
          { "RET NC", new byte[] { 0xd0 } },
          { "RET NZ", new byte[] { 0xc0 } },
          { "RET P", new byte[] { 0xf0 } },
          { "RET PE", new byte[] { 0xe8 } },
          { "RET PO", new byte[] { 0xe0 } },
          { "RET Z", new byte[] { 0xc8 } },

          { "RETI", new byte[] { 0xed, 0x4d } },

          { "RETN", new byte[] { 0xed, 0x45 } },

          { "RL (HL)", new byte[] { 0xcb, 0x16 } },
          { "RL A", new byte[] { 0xcb, 0x17 } },
          { "RL B", new byte[] { 0xcb, 0x10 } },
          { "RL C", new byte[] { 0xcb, 0x11 } },
          { "RL D", new byte[] { 0xcb, 0x12 } },
          { "RL E", new byte[] { 0xcb, 0x13 } },
          { "RL H", new byte[] { 0xcb, 0x14 } },
          { "RL L", new byte[] { 0xcb, 0x15 } },
          { "RL (IX)", new byte[] { 0xdd, 0xcb, 0, 0x16 } },
          { "RL (IY)", new byte[] { 0xfd, 0xcb, 0, 0x16 } },
          { "RLA", new byte[] { 0x17 } },

          { "RLC (HL)", new byte[] { 0xcb, 0x06 } },
          { "RLC (IX)", new byte[] { 0xdd, 0xcb, 0, 0x06 } },
          { "RLC (IY)", new byte[] { 0xfd, 0xcb, 0, 0x06 } },
          { "RLC A", new byte[] { 0xcb, 0x07 } },
          { "RLC B", new byte[] { 0xcb, 0x00 } },
          { "RLC C", new byte[] { 0xcb, 0x01 } },
          { "RLC D", new byte[] { 0xcb, 0x02 } },
          { "RLC E", new byte[] { 0xcb, 0x03 } },
          { "RLC H", new byte[] { 0xcb, 0x04 } },
          { "RLC L", new byte[] { 0xcb, 0x05 } },

          { "RLCA", new byte[] { 0x07 } },

          { "RLD", new byte[] { 0xed, 0x6f } },

          { "RR (HL)", new byte[] { 0xcb, 0x1e } },
          { "RR A", new byte[] { 0xcb, 0x1f } },
          { "RR B", new byte[] { 0xcb, 0x18 } },
          { "RR C", new byte[] { 0xcb, 0x19 } },
          { "RR D", new byte[] { 0xcb, 0x1a } },
          { "RR E", new byte[] { 0xcb, 0x1b } },
          { "RR H", new byte[] { 0xcb, 0x1c } },
          { "RR L", new byte[] { 0xcb, 0x1d } },
          { "RR (IX)", new byte[] { 0xdd, 0xcb, 0, 0x1e } },
          { "RR (IY)", new byte[] { 0xfd, 0xcb, 0, 0x1e } },

          { "RRA", new byte[] { 0x1f } },

          { "RRC (HL)", new byte[] { 0xcb, 0x0e } },
          { "RRC (IX)", new byte[] { 0xdd, 0xcb, 0, 0x0e } },
          { "RRC (IY)", new byte[] { 0xfd, 0xcb, 0, 0x0e } },
          { "RRC A", new byte[] { 0xcb, 0x0f } },
          { "RRC B", new byte[] { 0xcb, 0x08 } },
          { "RRC C", new byte[] { 0xcb, 0x09 } },
          { "RRC D", new byte[] { 0xcb, 0x0a } },
          { "RRC E", new byte[] { 0xcb, 0x0b } },
          { "RRC H", new byte[] { 0xcb, 0x0c } },
          { "RRC L", new byte[] { 0xcb, 0x0d } },

          { "RRCA", new byte[] { 0x0f } },

          { "RRD", new byte[] { 0xed, 0x67 } },

          { "RST 0", new byte[] { 0xc7 } },
          { "RST 8", new byte[] { 0xcf } },
          { "RST 16", new byte[] { 0xd7 } },
          { "RST 24", new byte[] { 0xdf } },
          { "RST 32", new byte[] { 0xe7 } },
          { "RST 40", new byte[] { 0xef } },
          { "RST 48", new byte[] { 0xf7 } },
          { "RST 56", new byte[] { 0xff } },

          { "SBC HL,BC", new byte[] { 0xed, 0x42 } },
          { "SBC HL,DE", new byte[] { 0xed, 0x52 } },
          { "SBC HL,HL", new byte[] { 0xed, 0x62 } },
          { "SBC HL,SP", new byte[] { 0xed, 0x72 } },
          { "SBC A,(HL)", new byte[] { 0x9e } },
          { "SBC A,A", new byte[] { 0x9f } },
          { "SBC A,B", new byte[] { 0x98 } },
          { "SBC A,C", new byte[] { 0x99 } },
          { "SBC A,D", new byte[] { 0x9a } },
          { "SBC A,E", new byte[] { 0x9b } },
          { "SBC A,H", new byte[] { 0x9c } },
          { "SBC A,L", new byte[] { 0x9d } },
          { "SBC A,(IX)", new byte[] { 0xdd, 0x9e, 0 } },
          { "SBC A,(IY)", new byte[] { 0xfd, 0x9e, 0 } },
          { "SBC A,IXH", new byte[] { 0xdd, 0x9c } },
          { "SBC A,IXL", new byte[] { 0xdd, 0x9d } },
          { "SBC A,IYH", new byte[] { 0xfd, 0x9c } },
          { "SBC A,IYL", new byte[] { 0xfd, 0x9d } },

          //Aliases for "SBC A,..." with implicit A
          { "SBC (HL)", new byte[] { 0x9e } },
          { "SBC A", new byte[] { 0x9f } },
          { "SBC B", new byte[] { 0x98 } },
          { "SBC C", new byte[] { 0x99 } },
          { "SBC D", new byte[] { 0x9a } },
          { "SBC E", new byte[] { 0x9b } },
          { "SBC H", new byte[] { 0x9c } },
          { "SBC L", new byte[] { 0x9d } },
          { "SBC (IX)", new byte[] { 0xdd, 0x9e, 0 } },
          { "SBC (IY)", new byte[] { 0xfd, 0x9e, 0 } },
          { "SBC IXH", new byte[] { 0xdd, 0x9c } },
          { "SBC IXL", new byte[] { 0xdd, 0x9d } },
          { "SBC IYH", new byte[] { 0xfd, 0x9c } },
          { "SBC IYL", new byte[] { 0xfd, 0x9d } },

          { "SCF", new byte[] { 0x37 } },

          { "SLA (HL)", new byte[] { 0xcb, 0x26 } },
          { "SLA (IX)", new byte[] { 0xdd, 0xcb, 0, 0x26 } },
          { "SLA (IY)", new byte[] { 0xfd, 0xcb, 0, 0x26 } },
          { "SLA A", new byte[] { 0xcb, 0x27 } },
          { "SLA B", new byte[] { 0xcb, 0x20 } },
          { "SLA C", new byte[] { 0xcb, 0x21 } },
          { "SLA D", new byte[] { 0xcb, 0x22 } },
          { "SLA E", new byte[] { 0xcb, 0x23 } },
          { "SLA H", new byte[] { 0xcb, 0x24 } },
          { "SLA L", new byte[] { 0xcb, 0x25 } },

          { "SLL B", new byte[] { 0xcb, 0x30 } },
          { "SLL C", new byte[] { 0xcb, 0x31 } },
          { "SLL D", new byte[] { 0xcb, 0x32 } },
          { "SLL E", new byte[] { 0xcb, 0x33 } },
          { "SLL H", new byte[] { 0xcb, 0x34 } },
          { "SLL L", new byte[] { 0xcb, 0x35 } },
          { "SLL (HL)", new byte[] { 0xcb, 0x36 } },
          { "SLL A", new byte[] { 0xcb, 0x37 } },

          { "SRA (HL)", new byte[] { 0xcb, 0x2e } },
          { "SRA (IX)", new byte[] { 0xdd, 0xcb, 0, 0x2e } },
          { "SRA (IY)", new byte[] { 0xfd, 0xcb, 0, 0x2e } },
          { "SRA A", new byte[] { 0xcb, 0x2f } },
          { "SRA B", new byte[] { 0xcb, 0x28 } },
          { "SRA C", new byte[] { 0xcb, 0x29 } },
          { "SRA D", new byte[] { 0xcb, 0x2a } },
          { "SRA E", new byte[] { 0xcb, 0x2b } },
          { "SRA H", new byte[] { 0xcb, 0x2c } },
          { "SRA L", new byte[] { 0xcb, 0x2d } },

          { "SRL (HL)", new byte[] { 0xcb, 0x3e } },
          { "SRL A", new byte[] { 0xcb, 0x3f } },
          { "SRL B", new byte[] { 0xcb, 0x38 } },
          { "SRL C", new byte[] { 0xcb, 0x39 } },
          { "SRL D", new byte[] { 0xcb, 0x3a } },
          { "SRL E", new byte[] { 0xcb, 0x3b } },
          { "SRL H", new byte[] { 0xcb, 0x3c } },
          { "SRL L", new byte[] { 0xcb, 0x3d } },

          { "SUB A,(HL)", new byte[] { 0x96 } },
          { "SUB A,(IX)", new byte[] { 0xdd, 0x96, 0 } },
          { "SUB A,(IY)", new byte[] { 0xfd, 0x96, 0 } },
          { "SUB A,A", new byte[] { 0x97 } },
          { "SUB A,B", new byte[] { 0x90 } },
          { "SUB A,C", new byte[] { 0x91 } },
          { "SUB A,D", new byte[] { 0x92 } },
          { "SUB A,E", new byte[] { 0x93 } },
          { "SUB A,H", new byte[] { 0x94 } },
          { "SUB A,L", new byte[] { 0x95 } },
          { "SUB A,IXH", new byte[] { 0xdd, 0x94 } },
          { "SUB A,IXL", new byte[] { 0xdd, 0x95 } },
          { "SUB A,IYH", new byte[] { 0xfd, 0x94 } },
          { "SUB A,IYL", new byte[] { 0xfd, 0x95 } },

          //Aliases for "SUB A,..." with implicit A
          { "SUB A", new byte[] { 0x97 } },
          { "SUB B", new byte[] { 0x90 } },
          { "SUB C", new byte[] { 0x91 } },
          { "SUB D", new byte[] { 0x92 } },
          { "SUB E", new byte[] { 0x93 } },
          { "SUB H", new byte[] { 0x94 } },
          { "SUB L", new byte[] { 0x95 } },
          { "SUB IXH", new byte[] { 0xdd, 0x94 } },
          { "SUB IXL", new byte[] { 0xdd, 0x95 } },
          { "SUB IYH", new byte[] { 0xfd, 0x94 } },
          { "SUB IYL", new byte[] { 0xfd, 0x95 } },
          { "SUB (HL)", new byte[] { 0x96 } },
          { "SUB (IX)", new byte[] { 0xdd, 0x96, 0 } },
          { "SUB (IY)", new byte[] { 0xfd, 0x96, 0 } },

          { "XOR (HL)", new byte[] { 0xae } },
          { "XOR (IX)", new byte[] { 0xdd, 0xae, 0 } },
          { "XOR (IY)", new byte[] { 0xfd, 0xae, 0 } },
          { "XOR A", new byte[] { 0xaf } },
          { "XOR B", new byte[] { 0xa8 } },
          { "XOR C", new byte[] { 0xa9 } },
          { "XOR D", new byte[] { 0xaa } },
          { "XOR E", new byte[] { 0xab } },
          { "XOR H", new byte[] { 0xac } },
          { "XOR L", new byte[] { 0xad } },
          { "XOR IXH", new byte[] { 0xdd, 0xac } },
          { "XOR IXL", new byte[] { 0xdd, 0xad } },
          { "XOR IYH", new byte[] { 0xfd, 0xac } },
          { "XOR IYL", new byte[] { 0xfd, 0xad } },

          { "MULUB A,A", new byte[] { 0xed, 0xf9 } },
          { "MULUB A,B", new byte[] { 0xed, 0xc1 } },
          { "MULUB A,C", new byte[] { 0xed, 0xc9 } },
          { "MULUB A,D", new byte[] { 0xed, 0xd1 } },
          { "MULUB A,E", new byte[] { 0xed, 0xd9 } },
          { "MULUB A,H", new byte[] { 0xed, 0xe1 } },
          { "MULUB A,L", new byte[] { 0xed, 0xe9 } },
          { "MULUW HL,BC", new byte[] { 0xed, 0xc3 } },
          { "MULUW HL,DE", new byte[] { 0xed, 0xd3 } },
          { "MULUW HL,HL", new byte[] { 0xed, 0xe3 } },
          { "MULUW HL,SP", new byte[] { 0xed, 0xf3 } },
        };

        /// <summary>
        /// Instructions that have one variable argument and maybe also one fixed argument.
        /// 
        /// Items in the tuples are:
        /// - 1: Instruction, followed by the fixed argument if present.
        /// - 2: Type of the variable argument.
        /// - 3: Position of the variable argument in the instruction (single, first or second).
        /// - 4: Byte position of the variable argument in the output.
        /// </summary>
        static readonly (string, CpuInstrArgType, CpuArgPos, byte[], int)[] 
            Z80InstructionsWithOneVariableArgument = new (string, CpuInstrArgType, CpuArgPos, byte[], int)[] {
            ( "ADC A", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0x8e, 0 }, 2 ), // ADC A,(IX+n)
            ( "ADC A", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0x8e, 0 }, 2 ), // ADC A,(IY+n)
            ( "ADC A", CpuInstrArgType.Byte, CpuArgPos.Second, new byte[] { 0xce, 0 }, 1 ), // ADC A,n
            //Aliases for "ADC A,..." with implicit A
            ( "ADC", CpuInstrArgType.IxOffset, CpuArgPos.Single, new byte[] { 0xdd, 0x8e, 0 }, 2 ), // ADC (IX+n)
            ( "ADC", CpuInstrArgType.IyOffset, CpuArgPos.Single, new byte[] { 0xfd, 0x8e, 0 }, 2 ), // ADC (IY+n)
            ( "ADC", CpuInstrArgType.Byte, CpuArgPos.Single, new byte[] { 0xce, 0 }, 1 ), // ADC n

            ( "ADD A", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0x86, 0 }, 2 ), // ADD A,(ix+n)
            ( "ADD A", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0x86, 0 }, 2 ), // ADD A,(iy+n)
            ( "ADD A", CpuInstrArgType.Byte, CpuArgPos.Second, new byte[] { 0xc6, 0 }, 1 ), // ADD a,n
            //Aliases for "ADD A,..." with implicit A
            ( "ADD", CpuInstrArgType.IxOffset, CpuArgPos.Single, new byte[] { 0xdd, 0x86, 0 }, 2 ), // ADD (IX+n)
            ( "ADD", CpuInstrArgType.IyOffset, CpuArgPos.Single, new byte[] { 0xfd, 0x86, 0 }, 2 ), // ADD (IY+n)
            ( "ADD", CpuInstrArgType.Byte, CpuArgPos.Single, new byte[] { 0xc6, 0 }, 1 ), // ADD n

            ( "AND", CpuInstrArgType.IxOffset, CpuArgPos.Single, new byte[] { 0xdd, 0xa6, 0 }, 2 ), // AND (IX+n)
            ( "AND", CpuInstrArgType.IyOffset, CpuArgPos.Single, new byte[] { 0xfd, 0xa6, 0 }, 2 ), // AND (IY+n)
            ( "AND", CpuInstrArgType.Byte, CpuArgPos.Single, new byte[] { 0xe6, 0 }, 1 ), // AND n

            ( "CALL C",  CpuInstrArgType.Word, CpuArgPos.Second, new byte[] { 0xdc, 0, 0 }, 1 ), // CALL C,nn
            ( "CALL M",  CpuInstrArgType.Word, CpuArgPos.Second, new byte[] { 0xfc, 0, 0 }, 1 ), // CALL M,nn
            ( "CALL NC", CpuInstrArgType.Word, CpuArgPos.Second, new byte[] { 0xd4, 0, 0 }, 1 ), // CALL NC,nn
            ( "CALL",    CpuInstrArgType.Word, CpuArgPos.Single, new byte[] { 0xcd, 0, 0 }, 1 ), // CALL nn
            ( "CALL NZ", CpuInstrArgType.Word, CpuArgPos.Second, new byte[] { 0xc4, 0, 0 }, 1 ), // CALL NZ,nn
            ( "CALL PE", CpuInstrArgType.Word, CpuArgPos.Second, new byte[] { 0xec, 0, 0 }, 1 ), // CALL PE,nn
            ( "CALL PO", CpuInstrArgType.Word, CpuArgPos.Second, new byte[] { 0xe4, 0, 0 }, 1 ), // CALL PO,nn
            ( "CALL P",  CpuInstrArgType.Word, CpuArgPos.Second, new byte[] { 0xF4, 0, 0 }, 1 ), // CALL P,nn
            ( "CALL Z",  CpuInstrArgType.Word, CpuArgPos.Second, new byte[] { 0xcc, 0, 0 }, 1 ), // CALL Z,nn

            ( "CP", CpuInstrArgType.IxOffset, CpuArgPos.Single, new byte[] { 0xdd, 0xbe, 0 }, 2 ), //CP (IX+n)
            ( "CP", CpuInstrArgType.IyOffset, CpuArgPos.Single, new byte[] { 0xfd, 0xbe, 0 }, 2 ), //CP (IY+n)
            ( "CP", CpuInstrArgType.Byte, CpuArgPos.Single, new byte[] { 0xfe, 0 }, 1 ), // CP n

            ( "DEC", CpuInstrArgType.IxOffset, CpuArgPos.Single, new byte[] { 0xdd, 0x35, 0 }, 2 ), // DEC (IX+n)
            ( "DEC", CpuInstrArgType.IyOffset, CpuArgPos.Single, new byte[] { 0xfd, 0x35, 0 }, 2 ), // DEC (IY+n)

            ( "DJNZ", CpuInstrArgType.Byte, CpuArgPos.Single, new byte[] { 0x10, 0 }, 1 ), // DJNZ n

            ( "IN A", CpuInstrArgType.ByteInParenthesis, CpuArgPos.Second, new byte[] { 0xdb, 0 }, 1 ), // IN A,(n)

            ( "INC", CpuInstrArgType.IxOffset, CpuArgPos.Single, new byte[] { 0xdd, 0x34, 0 }, 2 ), // INC (IX+n)
            ( "INC", CpuInstrArgType.IyOffset, CpuArgPos.Single, new byte[] { 0xfd, 0x34, 0 }, 2 ), // INC (IY+n)

            ( "JP",    CpuInstrArgType.Word, CpuArgPos.Single, new byte[] { 0xc3, 0, 0 }, 1 ), // JP nn
            ( "JP C",  CpuInstrArgType.Word, CpuArgPos.Second, new byte[] { 0xda, 0, 0 }, 1 ), // JP C,nn
            ( "JP M",  CpuInstrArgType.Word, CpuArgPos.Second, new byte[] { 0xfa, 0, 0 }, 1 ), // JP M,nn
            ( "JP NC", CpuInstrArgType.Word, CpuArgPos.Second, new byte[] { 0xd2, 0, 0 }, 1 ), // JP NC,nn
            ( "JP NZ", CpuInstrArgType.Word, CpuArgPos.Second, new byte[] { 0xc2, 0, 0 }, 1 ), // JP NZ,nn
            ( "JP P",  CpuInstrArgType.Word, CpuArgPos.Second, new byte[] { 0xf2, 0, 0 }, 1 ), // JP P,nn
            ( "JP PE", CpuInstrArgType.Word, CpuArgPos.Second, new byte[] { 0xea, 0, 0 }, 1 ), // JP PE,nn
            ( "JP PO", CpuInstrArgType.Word, CpuArgPos.Second, new byte[] { 0xe2, 0, 0 }, 1 ), // JP PO,nn
            ( "JP Z",  CpuInstrArgType.Word, CpuArgPos.Second, new byte[] { 0xca, 0, 0 }, 1 ), // JP Z,nn

            ( "JR",    CpuInstrArgType.Byte, CpuArgPos.Single, new byte[] { 0x18, 0 }, 1 ),
            ( "JR C",  CpuInstrArgType.Byte, CpuArgPos.Second, new byte[] { 0x38, 0 }, 1 ),
            ( "JR NC", CpuInstrArgType.Byte, CpuArgPos.Second, new byte[] { 0x30, 0 }, 1 ),
            ( "JR NZ", CpuInstrArgType.Byte, CpuArgPos.Second, new byte[] { 0x20, 0 }, 1 ),
            ( "JR Z",  CpuInstrArgType.Byte, CpuArgPos.Second, new byte[] { 0x28, 0 }, 1 ),
            
            // Important: instructions that accept a variable argument in parenthesis
            // must come before the equivalent instructions without parenthesis,
            // e.g. "LD A,(nn)" before "LD A,n"
            ( "LD A",  CpuInstrArgType.WordInParenthesis, CpuArgPos.Second, new byte[] { 0x3a, 0, 0 }, 1 ), // LD A,(nn)
            ( "LD BC", CpuInstrArgType.WordInParenthesis, CpuArgPos.Second, new byte[] { 0xed, 0x4b, 0, 0 }, 2 ), // LD BC,(nn)
            ( "LD DE", CpuInstrArgType.WordInParenthesis, CpuArgPos.Second, new byte[] { 0xed, 0x5b, 0, 0 }, 2 ), // LD DE,(nn)
            ( "LD HL", CpuInstrArgType.WordInParenthesis, CpuArgPos.Second, new byte[] { 0x2a, 0, 0 }, 1 ), // LD HL,(nn)
            ( "LD IX", CpuInstrArgType.WordInParenthesis, CpuArgPos.Second, new byte[] { 0xdd, 0x2a, 0, 0 }, 2 ), // LD IX,(nn)
            ( "LD IY", CpuInstrArgType.WordInParenthesis, CpuArgPos.Second, new byte[] { 0xfd, 0x2a, 0, 0 }, 2 ), // LD IY,(nn)
            ( "LD SP", CpuInstrArgType.WordInParenthesis, CpuArgPos.Second, new byte[] { 0xed, 0x7b, 0, 0 }, 2 ), // LD SP,(nn)
            ( "LD A",  CpuInstrArgType.WordInParenthesis, CpuArgPos.First, new byte[] { 0x32, 0, 0 }, 1 ), // LD (nn),A
            ( "LD BC", CpuInstrArgType.WordInParenthesis, CpuArgPos.First, new byte[] { 0xed, 0x43, 0, 0 }, 2 ), // LD (nn),BC
            ( "LD DE", CpuInstrArgType.WordInParenthesis, CpuArgPos.First, new byte[] { 0xed, 0x53, 0, 0 }, 2 ), // LD (nn),DE
            ( "LD HL", CpuInstrArgType.WordInParenthesis, CpuArgPos.First, new byte[] { 0x22, 0, 0 }, 1 ), // LD (nn),HL
            ( "LD IX", CpuInstrArgType.WordInParenthesis, CpuArgPos.First, new byte[] { 0xdd, 0x22, 0, 0 }, 2 ), // LD (nn),IX
            ( "LD IY", CpuInstrArgType.WordInParenthesis, CpuArgPos.First, new byte[] { 0xfd, 0x22, 0, 0 }, 2 ), // LD (nn),IY
            ( "LD SP", CpuInstrArgType.WordInParenthesis, CpuArgPos.First, new byte[] { 0xed, 0x73, 0, 0 }, 2 ), // LD (nn),SP
            ( "LD A", CpuInstrArgType.Byte, CpuArgPos.Second, new byte[] { 0x3e, 0 }, 1 ), // LD A,n
            ( "LD B", CpuInstrArgType.Byte, CpuArgPos.Second, new byte[] { 0x06, 0 }, 1 ), // LD B,n
            ( "LD C", CpuInstrArgType.Byte, CpuArgPos.Second, new byte[] { 0x0e, 0 }, 1 ), // LD B,n
            ( "LD D", CpuInstrArgType.Byte, CpuArgPos.Second, new byte[] { 0x16, 0 }, 1 ), // LD D,n
            ( "LD E", CpuInstrArgType.Byte, CpuArgPos.Second, new byte[] { 0x1e, 0 }, 1 ), // LD E,n
            ( "LD H", CpuInstrArgType.Byte, CpuArgPos.Second, new byte[] { 0x26, 0 }, 1 ), // LD H,n
            ( "LD L", CpuInstrArgType.Byte, CpuArgPos.Second, new byte[] { 0x2e, 0 }, 1 ), // LD L,n
            ( "LD IXH", CpuInstrArgType.Byte, CpuArgPos.Second, new byte[] { 0xdd, 0x26, 0 }, 2 ), // LD IXH,n
            ( "LD IXL", CpuInstrArgType.Byte, CpuArgPos.Second, new byte[] { 0xdd, 0x2e, 0 }, 2 ), // LD IXL,n
            ( "LD IYH", CpuInstrArgType.Byte, CpuArgPos.Second, new byte[] { 0xfd, 0x26, 0 }, 2 ), // LD IYH,n
            ( "LD IYL", CpuInstrArgType.Byte, CpuArgPos.Second, new byte[] { 0xfd, 0x2e, 0 }, 2 ), // LD IYL,n
            ( "LD BC", CpuInstrArgType.Word, CpuArgPos.Second, new byte[] { 0x01, 0, 0 }, 1 ), // LD BC,nn
            ( "LD DE", CpuInstrArgType.Word, CpuArgPos.Second, new byte[] { 0x11, 0, 0 }, 1 ), // LD DE,nn
            ( "LD HL", CpuInstrArgType.Word, CpuArgPos.Second, new byte[] { 0x21, 0, 0 }, 1 ), // LD HL,nn
            ( "LD IX", CpuInstrArgType.Word, CpuArgPos.Second, new byte[] { 0xdd, 0x21, 0, 0 }, 2 ), // LD IX,nn
            ( "LD IY", CpuInstrArgType.Word, CpuArgPos.Second, new byte[] { 0xfd, 0x21, 0, 0 }, 2 ), // LD IY,nn
            ( "LD SP", CpuInstrArgType.Word, CpuArgPos.Second, new byte[] { 0x31, 0, 0 }, 1 ), // LD SP,nn
            ( "LD (HL)",  CpuInstrArgType.Byte, CpuArgPos.Second, new byte[] { 0x36, 0 }, 1 ), // LD (HL),n
            ( "LD (IX)",  CpuInstrArgType.Byte, CpuArgPos.Second, new byte[] { 0xdd, 0x36, 0, 0 }, 3 ), // LD (IX),n
            ( "LD (IY)",  CpuInstrArgType.Byte, CpuArgPos.Second, new byte[] { 0xfd, 0x36, 0, 0 }, 3 ), // LD (IY),n
            ( "LD A", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0x7e, 0 }, 2 ), // LD A,(IX+s)
            ( "LD B", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0x46, 0 }, 2 ), // LD B,(IX+s)
            ( "LD C", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0x4e, 0 }, 2 ), // LD C,(IX+s)
            ( "LD D", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0x56, 0 }, 2 ), // LD D,(IX+s)
            ( "LD E", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0x5e, 0 }, 2 ), // LD E,(IX+s)
            ( "LD H", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0x66, 0 }, 2 ), // LD H,(IX+s)
            ( "LD L", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0x6e, 0 }, 2 ), // LD L,(IX+s)
            ( "LD A", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0x7e, 0 }, 2 ), // LD A,(IY+s)
            ( "LD B", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0x46, 0 }, 2 ), // LD B,(IY+s)
            ( "LD C", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0x4e, 0 }, 2 ), // LD C,(IY+s)
            ( "LD D", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0x56, 0 }, 2 ), // LD D,(IY+s)
            ( "LD E", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0x5e, 0 }, 2 ), // LD E,(IY+s)
            ( "LD H", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0x66, 0 }, 2 ), // LD H,(IY+s)
            ( "LD L", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0x6e, 0 }, 2 ), // LD L,(IY+s)
            ( "LD A", CpuInstrArgType.IxOffset, CpuArgPos.First, new byte[] { 0xdd, 0x77, 0 }, 2 ), // LD (IX+s),A
            ( "LD B", CpuInstrArgType.IxOffset, CpuArgPos.First, new byte[] { 0xdd, 0x70, 0 }, 2 ), // LD (IX+s),B
            ( "LD C", CpuInstrArgType.IxOffset, CpuArgPos.First, new byte[] { 0xdd, 0x71, 0 }, 2 ), // LD (IX+s),C
            ( "LD D", CpuInstrArgType.IxOffset, CpuArgPos.First, new byte[] { 0xdd, 0x72, 0 }, 2 ), // LD (IX+s),D
            ( "LD E", CpuInstrArgType.IxOffset, CpuArgPos.First, new byte[] { 0xdd, 0x73, 0 }, 2 ), // LD (IX+s),E
            ( "LD H", CpuInstrArgType.IxOffset, CpuArgPos.First, new byte[] { 0xdd, 0x74, 0 }, 2 ), // LD (IX+s),H
            ( "LD L", CpuInstrArgType.IxOffset, CpuArgPos.First, new byte[] { 0xdd, 0x75, 0 }, 2 ), // LD (IX+s),L
            ( "LD A", CpuInstrArgType.IyOffset, CpuArgPos.First, new byte[] { 0xfd, 0x77, 0 }, 2 ), // LD (IY+s),A
            ( "LD B", CpuInstrArgType.IyOffset, CpuArgPos.First, new byte[] { 0xfd, 0x70, 0 }, 2 ), // LD (IY+s),B
            ( "LD C", CpuInstrArgType.IyOffset, CpuArgPos.First, new byte[] { 0xfd, 0x71, 0 }, 2 ), // LD (IY+s),C
            ( "LD D", CpuInstrArgType.IyOffset, CpuArgPos.First, new byte[] { 0xfd, 0x72, 0 }, 2 ), // LD (IY+s),D
            ( "LD E", CpuInstrArgType.IyOffset, CpuArgPos.First, new byte[] { 0xfd, 0x73, 0 }, 2 ), // LD (IY+s),E
            ( "LD H", CpuInstrArgType.IyOffset, CpuArgPos.First, new byte[] { 0xfd, 0x74, 0 }, 2 ), // LD (IY+s),H
            ( "LD L", CpuInstrArgType.IyOffset, CpuArgPos.First, new byte[] { 0xfd, 0x75, 0 }, 2 ), // LD (IY+s),L

            ( "OR", CpuInstrArgType.IxOffset, CpuArgPos.Single, new byte[] { 0xdd, 0xb6, 0 }, 2 ), // OR (IX+n)
            ( "OR", CpuInstrArgType.IyOffset, CpuArgPos.Single, new byte[] { 0xfd, 0xb6, 0 }, 2 ), // OR (IY+n)
            ( "OR", CpuInstrArgType.Byte, CpuArgPos.Single, new byte[] { 0xf6, 0 }, 1 ), // OR n

            ( "OUT A", CpuInstrArgType.ByteInParenthesis, CpuArgPos.First, new byte[] { 0xd3, 0 }, 1 ), // OUT (n),A

            ( "RL", CpuInstrArgType.IxOffset, CpuArgPos.Single, new byte[] { 0xdd, 0xcb, 0, 0x16 }, 2 ), // RL (IX+n)
            ( "RL", CpuInstrArgType.IyOffset, CpuArgPos.Single, new byte[] { 0xfd, 0xcb, 0, 0x16 }, 2 ), // RL (IY+n)

            ( "RLC", CpuInstrArgType.IxOffset, CpuArgPos.Single, new byte[] { 0xdd, 0xcb, 0, 0x06 }, 2 ), // RLC (IX+n)
            ( "RLC", CpuInstrArgType.IyOffset, CpuArgPos.Single, new byte[] { 0xfd, 0xcb, 0, 0x06 }, 2 ), // RLC (IY+n)

            ( "RR", CpuInstrArgType.IxOffset, CpuArgPos.Single, new byte[] { 0xdd, 0xcb, 0, 0x1e }, 2 ), // RR (IX+n)
            ( "RR", CpuInstrArgType.IyOffset, CpuArgPos.Single, new byte[] { 0xfd, 0xcb, 0, 0x1e }, 2 ), // RR (IY+n)

            ( "RRC", CpuInstrArgType.IxOffset, CpuArgPos.Single, new byte[] { 0xdd, 0xcb, 0, 0x0e }, 2 ), // RRC (IX+n)
            ( "RRC", CpuInstrArgType.IyOffset, CpuArgPos.Single, new byte[] { 0xfd, 0xcb, 0, 0x0e }, 2 ), // RRC (IY+n)

            ( "SBC A", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0x9e, 0 }, 2 ), // SBC A,(IX+s)
            ( "SBC A", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0x9e, 0 }, 2 ), // SBC A,(IY+s)
            ( "SBC A", CpuInstrArgType.Byte, CpuArgPos.Second, new byte[] { 0xde, 0 }, 1 ), // SBC A,n
            //Aliases for "SBC A,..." with implicit A
            ( "SBC", CpuInstrArgType.IxOffset, CpuArgPos.Single, new byte[] { 0xdd, 0x9e, 0 }, 2 ), // SBC (IX+n)
            ( "SBC", CpuInstrArgType.IyOffset, CpuArgPos.Single, new byte[] { 0xfd, 0x9e, 0 }, 2 ), // SBC (IY+n)
            ( "SBC", CpuInstrArgType.Byte, CpuArgPos.Single, new byte[] { 0xde, 0 }, 1 ), // SBC n

            ( "SLA", CpuInstrArgType.IxOffset, CpuArgPos.Single, new byte[] { 0xdd, 0xcb, 0, 0x26 }, 2 ), // SLA (IX+n)
            ( "SLA", CpuInstrArgType.IyOffset, CpuArgPos.Single, new byte[] { 0xfd, 0xcb, 0, 0x26 }, 2 ), // SLA (IY+n)

            ( "SRA", CpuInstrArgType.IxOffset, CpuArgPos.Single, new byte[] { 0xdd, 0xcb, 0, 0x2e }, 2 ), // SRA (IX+n)
            ( "SRA", CpuInstrArgType.IyOffset, CpuArgPos.Single, new byte[] { 0xfd, 0xcb, 0, 0x2e }, 2 ), // SRA (IY+n)

            ( "SUB A", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0x96, 0 }, 2 ), // SUB A,(IX+s)
            ( "SUB A", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0x96, 0 }, 2 ), // SUB A,(IY+s)
            ( "SUB A", CpuInstrArgType.Byte, CpuArgPos.Second, new byte[] { 0xd6, 0 }, 1 ), // SUB A,n
            //Aliases for "SUB A,..." with implicit A
            ( "SUB", CpuInstrArgType.IxOffset, CpuArgPos.Single, new byte[] { 0xdd, 0x96, 0 }, 2 ), // SUB (IX+n)
            ( "SUB", CpuInstrArgType.IyOffset, CpuArgPos.Single, new byte[] { 0xfd, 0x96, 0 }, 2 ), // SUB (IY+n)
            ( "SUB", CpuInstrArgType.Byte, CpuArgPos.Single, new byte[] { 0xd6, 0 }, 1 ), // SUB n

            ( "XOR", CpuInstrArgType.IxOffset, CpuArgPos.Single, new byte[] { 0xdd, 0xae, 0 }, 2 ), // XOR (IX+n)
            ( "XOR", CpuInstrArgType.IyOffset, CpuArgPos.Single, new byte[] { 0xfd, 0xae, 0 }, 2 ), // XOR (IY+n)
            ( "XOR", CpuInstrArgType.Byte, CpuArgPos.Single, new byte[] { 0xee, 0 }, 1 ), // XOR n
        };

        /// <summary>
        /// Instructions whose first argument is one of a fixed set.
        /// 
        /// It's assumed that if there's a second argument it's either fixed (register reference) or (IX+n) or (IY+n),
        /// and that if the second argument is (IX+n) or (IY+n) then its byte position in the output is 2.
        /// 
        /// Items in the tuples are:
        /// - 1: Fixed argument, null if none, "x" for (IX+n), "y" for (IY+n).
        /// - 2: Output bytes of the instruction.
        /// - 3: Value of the first argument that selects this variant of the instruction.
        /// </summary>
        static readonly Dictionary<string, (string, byte[], ushort)[]> Z80InstructionsWithSelectorValue = 
            new(StringComparer.OrdinalIgnoreCase) {
            { "BIT", new (string, byte[], ushort)[] {
                ( "(HL)", new byte[] { 0xcb, 0x46 }, 0 ),
                ( "(IX)", new byte[] { 0xdd, 0xcb, 0, 0x46 }, 0 ),
                ( "(IY)", new byte[] { 0xfd, 0xcb, 0, 0x46 }, 0 ),
                ( "A", new byte[] { 0xcb, 0x47 }, 0 ),
                ( "B", new byte[] { 0xcb, 0x40 }, 0 ),
                ( "C", new byte[] { 0xcb, 0x41 }, 0 ),
                ( "D", new byte[] { 0xcb, 0x42 }, 0 ),
                ( "E", new byte[] { 0xcb, 0x43 }, 0 ),
                ( "H", new byte[] { 0xcb, 0x44 }, 0 ),
                ( "L", new byte[] { 0xcb, 0x45 }, 0 ),
                ( "(HL)", new byte[] { 0xcb, 0x4e }, 1 ),
                ( "(IX)", new byte[] { 0xdd, 0xcb, 0, 0x4e }, 1 ),
                ( "(IY)", new byte[] { 0xfd, 0xcb, 0, 0x4e }, 1 ),
                ( "A", new byte[] { 0xcb, 0x4f }, 1 ),
                ( "B", new byte[] { 0xcb, 0x48 }, 1 ),
                ( "C", new byte[] { 0xcb, 0x49 }, 1 ),
                ( "D", new byte[] { 0xcb, 0x4a }, 1 ),
                ( "E", new byte[] { 0xcb, 0x4b }, 1 ),
                ( "H", new byte[] { 0xcb, 0x4c }, 1 ),
                ( "L", new byte[] { 0xcb, 0x4d }, 1 ),
                ( "(HL)", new byte[] { 0xcb, 0x56 }, 2 ),
                ( "(IX)", new byte[] { 0xdd, 0xcb, 0, 0x56 }, 2 ),
                ( "(IY)", new byte[] { 0xfd, 0xcb, 0, 0x56 }, 2 ),
                ( "A", new byte[] { 0xcb, 0x57 }, 2 ),
                ( "B", new byte[] { 0xcb, 0x50 }, 2 ),
                ( "C", new byte[] { 0xcb, 0x51 }, 2 ),
                ( "D", new byte[] { 0xcb, 0x52 }, 2 ),
                ( "E", new byte[] { 0xcb, 0x53 }, 2 ),
                ( "H", new byte[] { 0xcb, 0x54 }, 2 ),
                ( "L", new byte[] { 0xcb, 0x55 }, 2 ),
                ( "(HL)", new byte[] { 0xcb, 0x5e }, 3 ),
                ( "(IX)", new byte[] { 0xdd, 0xcb, 0, 0x5e }, 3 ),
                ( "(IY)", new byte[] { 0xfd, 0xcb, 0, 0x5e }, 3 ),
                ( "A", new byte[] { 0xcb, 0x5f }, 3 ),
                ( "B", new byte[] { 0xcb, 0x58 }, 3 ),
                ( "C", new byte[] { 0xcb, 0x59 }, 3 ),
                ( "D", new byte[] { 0xcb, 0x5a }, 3 ),
                ( "E", new byte[] { 0xcb, 0x5b }, 3 ),
                ( "H", new byte[] { 0xcb, 0x5c }, 3 ),
                ( "L", new byte[] { 0xcb, 0x5d }, 3 ),
                ( "(HL)", new byte[] { 0xcb, 0x66 }, 4 ),
                ( "(IX)", new byte[] { 0xdd, 0xcb, 0, 0x66 }, 4 ),
                ( "(IY)", new byte[] { 0xfd, 0xcb, 0, 0x66 }, 4 ),
                ( "A", new byte[] { 0xcb, 0x67 }, 4 ),
                ( "B", new byte[] { 0xcb, 0x60 }, 4 ),
                ( "C", new byte[] { 0xcb, 0x61 }, 4 ),
                ( "D", new byte[] { 0xcb, 0x62 }, 4 ),
                ( "E", new byte[] { 0xcb, 0x63 }, 4 ),
                ( "H", new byte[] { 0xcb, 0x64 }, 4 ),
                ( "L", new byte[] { 0xcb, 0x65 }, 4 ),
                ( "(HL)", new byte[] { 0xcb, 0x6e }, 5 ),
                ( "(IX)", new byte[] { 0xdd, 0xcb, 0, 0x6e }, 5 ),
                ( "(IY)", new byte[] { 0xfd, 0xcb, 0, 0x6e }, 5 ),
                ( "A", new byte[] { 0xcb, 0x6f }, 5 ),
                ( "B", new byte[] { 0xcb, 0x68 }, 5 ),
                ( "C", new byte[] { 0xcb, 0x69 }, 5 ),
                ( "D", new byte[] { 0xcb, 0x6a }, 5 ),
                ( "E", new byte[] { 0xcb, 0x6b }, 5 ),
                ( "H", new byte[] { 0xcb, 0x6c }, 5 ),
                ( "L", new byte[] { 0xcb, 0x6d }, 5 ),
                ( "(HL)", new byte[] { 0xcb, 0x76 }, 6 ),
                ( "(IX)", new byte[] { 0xdd, 0xcb, 0, 0x76 }, 6 ),
                ( "(IY)", new byte[] { 0xfd, 0xcb, 0, 0x76 }, 6 ),
                ( "A", new byte[] { 0xcb, 0x77 }, 6 ),
                ( "B", new byte[] { 0xcb, 0x70 }, 6 ),
                ( "C", new byte[] { 0xcb, 0x71 }, 6 ),
                ( "D", new byte[] { 0xcb, 0x72 }, 6 ),
                ( "E", new byte[] { 0xcb, 0x73 }, 6 ),
                ( "H", new byte[] { 0xcb, 0x74 }, 6 ),
                ( "L", new byte[] { 0xcb, 0x75 }, 6 ),
                ( "(HL)", new byte[] { 0xcb, 0x7e }, 7 ),
                ( "(IX)", new byte[] { 0xdd, 0xcb, 0, 0x7e }, 7 ),
                ( "(IY)", new byte[] { 0xfd, 0xcb, 0, 0x7e }, 7 ),
                ( "A", new byte[] { 0xcb, 0x7f }, 7 ),
                ( "B", new byte[] { 0xcb, 0x78 }, 7 ),
                ( "C", new byte[] { 0xcb, 0x79 }, 7 ),
                ( "D", new byte[] { 0xcb, 0x7a }, 7 ),
                ( "E", new byte[] { 0xcb, 0x7b }, 7 ),
                ( "H", new byte[] { 0xcb, 0x7c }, 7 ),
                ( "L", new byte[] { 0xcb, 0x7d }, 7 ),
                ( "x", new byte[] { 0xdd, 0xcb, 0, 0x46 }, 0 ),
                ( "y", new byte[] { 0xfd, 0xcb, 0, 0x46 }, 0 ),
                ( "x", new byte[] { 0xdd, 0xcb, 0, 0x4e }, 1 ),
                ( "y", new byte[] { 0xfd, 0xcb, 0, 0x4e }, 1 ),
                ( "x", new byte[] { 0xdd, 0xcb, 0, 0x56 }, 2 ),
                ( "y", new byte[] { 0xfd, 0xcb, 0, 0x56 }, 2 ),
                ( "x", new byte[] { 0xdd, 0xcb, 0, 0x5e }, 3 ),
                ( "y", new byte[] { 0xfd, 0xcb, 0, 0x5e }, 3 ),
                ( "x", new byte[] { 0xdd, 0xcb, 0, 0x66 }, 4 ),
                ( "y", new byte[] { 0xfd, 0xcb, 0, 0x66 }, 4 ),
                ( "x", new byte[] { 0xdd, 0xcb, 0, 0x6e }, 5 ),
                ( "y", new byte[] { 0xfd, 0xcb, 0, 0x6e }, 5 ),
                ( "x", new byte[] { 0xdd, 0xcb, 0, 0x76 }, 6 ),
                ( "y", new byte[] { 0xfd, 0xcb, 0, 0x76 }, 6 ),
                ( "x", new byte[] { 0xdd, 0xcb, 0, 0x7e }, 7 ),
                ( "y", new byte[] { 0xfd, 0xcb, 0, 0x7e }, 7 ) }
            },
            { "SET", new (string, byte[], ushort)[] {
                ( "(HL)", new byte[] { 0xcb, 0xc6 }, 0 ),
                ( "(IX)", new byte[] { 0xdd, 0xcb, 0, 0xc6 }, 0 ),
                ( "(IY)", new byte[] { 0xfd, 0xcb, 0, 0xc6 }, 0 ),
                ( "A", new byte[] { 0xcb, 0xc7 }, 0 ),
                ( "B", new byte[] { 0xcb, 0xc0 }, 0 ),
                ( "C", new byte[] { 0xcb, 0xc1 }, 0 ),
                ( "D", new byte[] { 0xcb, 0xc2 }, 0 ),
                ( "E", new byte[] { 0xcb, 0xc3 }, 0 ),
                ( "H", new byte[] { 0xcb, 0xc4 }, 0 ),
                ( "L", new byte[] { 0xcb, 0xc5 }, 0 ),
                ( "(HL)", new byte[] { 0xcb, 0xce }, 1 ),
                ( "(IX)", new byte[] { 0xdd, 0xcb, 0, 0xce }, 1 ),
                ( "(IY)", new byte[] { 0xfd, 0xcb, 0, 0xce }, 1 ),
                ( "A", new byte[] { 0xcb, 0xcf }, 1 ),
                ( "B", new byte[] { 0xcb, 0xc8 }, 1 ),
                ( "C", new byte[] { 0xcb, 0xc9 }, 1 ),
                ( "D", new byte[] { 0xcb, 0xca }, 1 ),
                ( "E", new byte[] { 0xcb, 0xcb }, 1 ),
                ( "H", new byte[] { 0xcb, 0xcc }, 1 ),
                ( "L", new byte[] { 0xcb, 0xcd }, 1 ),
                ( "(HL)", new byte[] { 0xcb, 0xd6 }, 2 ),
                ( "(IX)", new byte[] { 0xdd, 0xcb, 0, 0xd6 }, 2 ),
                ( "(IY)", new byte[] { 0xfd, 0xcb, 0, 0xd6 }, 2 ),
                ( "A", new byte[] { 0xcb, 0xd7 }, 2 ),
                ( "B", new byte[] { 0xcb, 0xd0 }, 2 ),
                ( "C", new byte[] { 0xcb, 0xd1 }, 2 ),
                ( "D", new byte[] { 0xcb, 0xd2 }, 2 ),
                ( "E", new byte[] { 0xcb, 0xd3 }, 2 ),
                ( "H", new byte[] { 0xcb, 0xd4 }, 2 ),
                ( "L", new byte[] { 0xcb, 0xd5 }, 2 ),
                ( "(HL)", new byte[] { 0xcb, 0xde }, 3 ),
                ( "(IX)", new byte[] { 0xdd, 0xcb, 0, 0xde }, 3 ),
                ( "(IY)", new byte[] { 0xfd, 0xcb, 0, 0xde }, 3 ),
                ( "A", new byte[] { 0xcb, 0xdf }, 3 ),
                ( "B", new byte[] { 0xcb, 0xd8 }, 3 ),
                ( "C", new byte[] { 0xcb, 0xd9 }, 3 ),
                ( "D", new byte[] { 0xcb, 0xda }, 3 ),
                ( "E", new byte[] { 0xcb, 0xdb }, 3 ),
                ( "H", new byte[] { 0xcb, 0xdc }, 3 ),
                ( "L", new byte[] { 0xcb, 0xdd }, 3 ),
                ( "(HL)", new byte[] { 0xcb, 0xe6 }, 4 ),
                ( "(IX)", new byte[] { 0xdd, 0xcb, 0, 0xe6 }, 4 ),
                ( "(IY)", new byte[] { 0xfd, 0xcb, 0, 0xe6 }, 4 ),
                ( "A", new byte[] { 0xcb, 0xe7 }, 4 ),
                ( "B", new byte[] { 0xcb, 0xe0 }, 4 ),
                ( "C", new byte[] { 0xcb, 0xe1 }, 4 ),
                ( "D", new byte[] { 0xcb, 0xe2 }, 4 ),
                ( "E", new byte[] { 0xcb, 0xe3 }, 4 ),
                ( "H", new byte[] { 0xcb, 0xe4 }, 4 ),
                ( "L", new byte[] { 0xcb, 0xe5 }, 4 ),
                ( "(HL)", new byte[] { 0xcb, 0xee }, 5 ),
                ( "(IX)", new byte[] { 0xdd, 0xcb, 0, 0xee }, 5 ),
                ( "(IY)", new byte[] { 0xfd, 0xcb, 0, 0xee }, 5 ),
                ( "A", new byte[] { 0xcb, 0xef }, 5 ),
                ( "B", new byte[] { 0xcb, 0xe8 }, 5 ),
                ( "C", new byte[] { 0xcb, 0xe9 }, 5 ),
                ( "D", new byte[] { 0xcb, 0xea }, 5 ),
                ( "E", new byte[] { 0xcb, 0xeb }, 5 ),
                ( "H", new byte[] { 0xcb, 0xec }, 5 ),
                ( "L", new byte[] { 0xcb, 0xed }, 5 ),
                ( "(HL)", new byte[] { 0xcb, 0xf6 }, 6 ),
                ( "(IX)", new byte[] { 0xdd, 0xcb, 0, 0xf6 }, 6 ),
                ( "(IY)", new byte[] { 0xfd, 0xcb, 0, 0xf6 }, 6 ),
                ( "A", new byte[] { 0xcb, 0xf7 }, 6 ),
                ( "B", new byte[] { 0xcb, 0xf0 }, 6 ),
                ( "C", new byte[] { 0xcb, 0xf1 }, 6 ),
                ( "D", new byte[] { 0xcb, 0xf2 }, 6 ),
                ( "E", new byte[] { 0xcb, 0xf3 }, 6 ),
                ( "H", new byte[] { 0xcb, 0xf4 }, 6 ),
                ( "L", new byte[] { 0xcb, 0xf5 }, 6 ),
                ( "(HL)", new byte[] { 0xcb, 0xfe }, 7 ),
                ( "(IX)", new byte[] { 0xdd, 0xcb, 0, 0xfe }, 7 ),
                ( "(IY)", new byte[] { 0xfd, 0xcb, 0, 0xfe }, 7 ),
                ( "A", new byte[] { 0xcb, 0xff }, 7 ),
                ( "B", new byte[] { 0xcb, 0xf8 }, 7 ),
                ( "C", new byte[] { 0xcb, 0xf9 }, 7 ),
                ( "D", new byte[] { 0xcb, 0xfa }, 7 ),
                ( "E", new byte[] { 0xcb, 0xfb }, 7 ),
                ( "H", new byte[] { 0xcb, 0xfc }, 7 ),
                ( "L", new byte[] { 0xcb, 0xfd }, 7 ),
                ( "x", new byte[] { 0xdd, 0xcb, 0, 0xc6 }, 0 ),
                ( "y", new byte[] { 0xfd, 0xcb, 0, 0xc6 }, 0 ),
                ( "x", new byte[] { 0xdd, 0xcb, 0, 0xce }, 1 ),
                ( "y", new byte[] { 0xfd, 0xcb, 0, 0xce }, 1 ),
                ( "x", new byte[] { 0xdd, 0xcb, 0, 0xd6 }, 2 ),
                ( "y", new byte[] { 0xfd, 0xcb, 0, 0xd6 }, 2 ),
                ( "x", new byte[] { 0xdd, 0xcb, 0, 0xde }, 3 ),
                ( "y", new byte[] { 0xfd, 0xcb, 0, 0xde }, 3 ),
                ( "x", new byte[] { 0xdd, 0xcb, 0, 0xe6 }, 4 ),
                ( "y", new byte[] { 0xfd, 0xcb, 0, 0xe6 }, 4 ),
                ( "x", new byte[] { 0xdd, 0xcb, 0, 0xee }, 5 ),
                ( "y", new byte[] { 0xfd, 0xcb, 0, 0xee }, 5 ),
                ( "x", new byte[] { 0xdd, 0xcb, 0, 0xf6 }, 6 ),
                ( "y", new byte[] { 0xfd, 0xcb, 0, 0xf6 }, 6 ),
                ( "x", new byte[] { 0xdd, 0xcb, 0, 0xfe }, 7 ),
                ( "y", new byte[] { 0xfd, 0xcb, 0, 0xfe }, 7 ) }
            },
            { "RES", new (string, byte[], ushort )[] {
                ( "(HL)", new byte[] { 0xcb, 0x86 }, 0 ),
                ( "(IX)", new byte[] { 0xdd, 0xcb, 0, 0x86 }, 0 ),
                ( "(IY)", new byte[] { 0xfd, 0xcb, 0, 0x86 }, 0 ),
                ( "A", new byte[] { 0xcb, 0x87 }, 0 ),
                ( "B", new byte[] { 0xcb, 0x80 }, 0 ),
                ( "C", new byte[] { 0xcb, 0x81 }, 0 ),
                ( "D", new byte[] { 0xcb, 0x82 }, 0 ),
                ( "E", new byte[] { 0xcb, 0x83 }, 0 ),
                ( "H", new byte[] { 0xcb, 0x84 }, 0 ),
                ( "L", new byte[] { 0xcb, 0x85 }, 0 ),
                ( "(HL)", new byte[] { 0xcb, 0x8e }, 1 ),
                ( "(IX)", new byte[] { 0xdd, 0xcb, 0, 0x8e }, 1 ),
                ( "(IY)", new byte[] { 0xfd, 0xcb, 0, 0x8e }, 1 ),
                ( "A", new byte[] { 0xcb, 0x8f }, 1 ),
                ( "B", new byte[] { 0xcb, 0x88 }, 1 ),
                ( "C", new byte[] { 0xcb, 0x89 }, 1 ),
                ( "D", new byte[] { 0xcb, 0x8a }, 1 ),
                ( "E", new byte[] { 0xcb, 0x8b }, 1 ),
                ( "H", new byte[] { 0xcb, 0x8c }, 1 ),
                ( "L", new byte[] { 0xcb, 0x8d }, 1 ),
                ( "(HL)", new byte[] { 0xcb, 0x96 }, 2 ),
                ( "(IX)", new byte[] { 0xdd, 0xcb, 0, 0x96 }, 2 ),
                ( "(IY)", new byte[] { 0xfd, 0xcb, 0, 0x96 }, 2 ),
                ( "A", new byte[] { 0xcb, 0x97 }, 2 ),
                ( "B", new byte[] { 0xcb, 0x90 }, 2 ),
                ( "C", new byte[] { 0xcb, 0x91 }, 2 ),
                ( "D", new byte[] { 0xcb, 0x92 }, 2 ),
                ( "E", new byte[] { 0xcb, 0x93 }, 2 ),
                ( "H", new byte[] { 0xcb, 0x94 }, 2 ),
                ( "L", new byte[] { 0xcb, 0x95 }, 2 ),
                ( "(HL)", new byte[] { 0xcb, 0x9e }, 3 ),
                ( "(IX)", new byte[] { 0xdd, 0xcb, 0, 0x9e }, 3 ),
                ( "(IY)", new byte[] { 0xfd, 0xcb, 0, 0x9e }, 3 ),
                ( "A", new byte[] { 0xcb, 0x9f }, 3 ),
                ( "B", new byte[] { 0xcb, 0x98 }, 3 ),
                ( "C", new byte[] { 0xcb, 0x99 }, 3 ),
                ( "D", new byte[] { 0xcb, 0x9a }, 3 ),
                ( "E", new byte[] { 0xcb, 0x9b }, 3 ),
                ( "H", new byte[] { 0xcb, 0x9c }, 3 ),
                ( "L", new byte[] { 0xcb, 0x9d }, 3 ),
                ( "(HL)", new byte[] { 0xcb, 0xa6 }, 4 ),
                ( "(IX)", new byte[] { 0xdd, 0xcb, 0, 0xa6 }, 4 ),
                ( "(IY)", new byte[] { 0xfd, 0xcb, 0, 0xa6 }, 4 ),
                ( "A", new byte[] { 0xcb, 0xa7 }, 4 ),
                ( "B", new byte[] { 0xcb, 0xa0 }, 4 ),
                ( "C", new byte[] { 0xcb, 0xa1 }, 4 ),
                ( "D", new byte[] { 0xcb, 0xa2 }, 4 ),
                ( "E", new byte[] { 0xcb, 0xa3 }, 4 ),
                ( "H", new byte[] { 0xcb, 0xa4 }, 4 ),
                ( "L", new byte[] { 0xcb, 0xa5 }, 4 ),
                ( "(HL)", new byte[] { 0xcb, 0xae }, 5 ),
                ( "(IX)", new byte[] { 0xdd, 0xcb, 0, 0xae }, 5 ),
                ( "(IY)", new byte[] { 0xfd, 0xcb, 0, 0xae }, 5 ),
                ( "A", new byte[] { 0xcb, 0xaf }, 5 ),
                ( "B", new byte[] { 0xcb, 0xa8 }, 5 ),
                ( "C", new byte[] { 0xcb, 0xa9 }, 5 ),
                ( "D", new byte[] { 0xcb, 0xaa }, 5 ),
                ( "E", new byte[] { 0xcb, 0xab }, 5 ),
                ( "H", new byte[] { 0xcb, 0xac }, 5 ),
                ( "L", new byte[] { 0xcb, 0xad }, 5 ),
                ( "(HL)", new byte[] { 0xcb, 0xb6 }, 6 ),
                ( "(IX)", new byte[] { 0xdd, 0xcb, 0, 0xb6 }, 6 ),
                ( "(IY)", new byte[] { 0xfd, 0xcb, 0, 0xb6 }, 6 ),
                ( "A", new byte[] { 0xcb, 0xb7 }, 6 ),
                ( "B", new byte[] { 0xcb, 0xb0 }, 6 ),
                ( "C", new byte[] { 0xcb, 0xb1 }, 6 ),
                ( "D", new byte[] { 0xcb, 0xb2 }, 6 ),
                ( "E", new byte[] { 0xcb, 0xb3 }, 6 ),
                ( "H", new byte[] { 0xcb, 0xb4 }, 6 ),
                ( "L", new byte[] { 0xcb, 0xb5 }, 6 ),
                ( "(HL)", new byte[] { 0xcb, 0xbe }, 7 ),
                ( "(IX)", new byte[] { 0xdd, 0xcb, 0, 0xbe }, 7 ),
                ( "(IY)", new byte[] { 0xfd, 0xcb, 0, 0xbe }, 7 ),
                ( "A", new byte[] { 0xcb, 0xbf }, 7 ),
                ( "B", new byte[] { 0xcb, 0xb8 }, 7 ),
                ( "C", new byte[] { 0xcb, 0xb9 }, 7 ),
                ( "D", new byte[] { 0xcb, 0xba }, 7 ),
                ( "E", new byte[] { 0xcb, 0xbb }, 7 ),
                ( "H", new byte[] { 0xcb, 0xbc }, 7 ),
                ( "L", new byte[] { 0xcb, 0xbd }, 7 ),
                ( "x", new byte[] { 0xdd, 0xcb, 0, 0x86 }, 0 ),
                ( "y", new byte[] { 0xfd, 0xcb, 0, 0x86 }, 0 ),
                ( "x", new byte[] { 0xdd, 0xcb, 0, 0x8e }, 1 ),
                ( "y", new byte[] { 0xfd, 0xcb, 0, 0x8e }, 1 ),
                ( "x", new byte[] { 0xdd, 0xcb, 0, 0x96 }, 2 ),
                ( "y", new byte[] { 0xfd, 0xcb, 0, 0x96 }, 2 ),
                ( "x", new byte[] { 0xdd, 0xcb, 0, 0x9e }, 3 ),
                ( "y", new byte[] { 0xfd, 0xcb, 0, 0x9e }, 3 ),
                ( "x", new byte[] { 0xdd, 0xcb, 0, 0xa6 }, 4 ),
                ( "y", new byte[] { 0xfd, 0xcb, 0, 0xa6 }, 4 ),
                ( "x", new byte[] { 0xdd, 0xcb, 0, 0xae }, 5 ),
                ( "y", new byte[] { 0xfd, 0xcb, 0, 0xae }, 5 ),
                ( "x", new byte[] { 0xdd, 0xcb, 0, 0xb6 }, 6 ),
                ( "y", new byte[] { 0xfd, 0xcb, 0, 0xb6 }, 6 ),
                ( "x", new byte[] { 0xdd, 0xcb, 0, 0xbe }, 7 ),
                ( "y", new byte[] { 0xfd, 0xcb, 0, 0xbe }, 7 ) }
            },
            { "IM", new (string, byte[], ushort )[] {
                ( null, new byte[] { 0xed, 0x46 }, 0 ),
                ( null, new byte[] { 0xed, 0x56 }, 1 ),
                ( null, new byte[] { 0xed, 0x5e }, 2 ) }
            },
            { "RST", new (string, byte[], ushort)[] {
                (null, new byte[] { 0xc7 }, 0 ),
                (null, new byte[] { 0xcf }, 0x08 ),
                (null, new byte[] { 0xd7 }, 0x10 ),
                (null, new byte[] { 0xdf }, 0x18 ),
                (null, new byte[] { 0xe7 }, 0x20 ),
                (null, new byte[] { 0xef }, 0x28 ),
                (null, new byte[] { 0xf7 }, 0x30 ),
                (null, new byte[] { 0xff }, 0x38 ) }
            }
        };
    }
}
