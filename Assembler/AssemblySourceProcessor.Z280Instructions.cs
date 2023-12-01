using Konamiman.Nestor80.Assembler.Infrastructure;

namespace Konamiman.Nestor80.Assembler
{
    public partial class AssemblySourceProcessor
    {
        static readonly string[] ExclusiveZ280InstructionOpcodes = new[] {
            "ADDW","CPW","DECW","DIV","DIVU","DIVUW","DIVW","EPUF","EPUI","EPUM","EXTS",
            "INCW","INDRW","INDW","INIRW","INIW","INW", "JAF","JAR","LDA","LDCTL",
            "LDUD","LDUP","LDW","MEPU","MULT","MULTU","MULTUW","MULTW","OTDRW","OTIRW",
            "OUTDW","OUTIW","OUTW","PCACHE","RETIL","SC","SUBW","TSET","TSTI"
        };

        /// <summary>
        /// Exclusive Z280 instructions that have no argument or that have one or two fixed (register or flag names) arguments.
        /// </summary>
        static readonly Dictionary<string, byte[]> FixedExclusiveZ280Instructions = new(StringComparer.OrdinalIgnoreCase) {
            { "ADC A,(HL+IX)", new byte[] { 0xdd, 0x89 } },
            { "ADC A,(HL+IY)", new byte[] { 0xdd, 0x8a } },
            { "ADC A,(IX+IY)", new byte[] { 0xdd, 0x8b } },
            { "ADC IX,BC", new byte[] { 0xdd, 0xed, 0x4a } },
            { "ADC IX,DE", new byte[] { 0xdd, 0xed, 0x5a } },
            { "ADC IX,IX", new byte[] { 0xdd, 0xed, 0x6a } },
            { "ADC IX,SP", new byte[] { 0xdd, 0xed, 0x7a } },
            { "ADC IY,BC", new byte[] { 0xfd, 0xed, 0x4a } },
            { "ADC IY,DE", new byte[] { 0xfd, 0xed, 0x5a } },
            { "ADC IY,IY", new byte[] { 0xfd, 0xed, 0x6a } },
            { "ADC IY,SP", new byte[] { 0xfd, 0xed, 0x7a } },

            // Aliases for "ADC A,..." with implicit A
            { "ADC (HL+IX)", new byte[] { 0xdd, 0x89 } },
            { "ADC (HL+IY)", new byte[] { 0xdd, 0x8a } },
            { "ADC (IX+IY)", new byte[] { 0xdd, 0x8b } },

            { "ADD A,(HL+IX)", new byte[] { 0xdd, 0x81 } },
            { "ADD A,(HL+IY)", new byte[] { 0xdd, 0x82 } },
            { "ADD A,(IX+IY)", new byte[] { 0xdd, 0x83 } },
            { "ADD HL,A", new byte[] { 0xed, 0x6d } },
            { "ADD IX,A", new byte[] { 0xdd, 0xed, 0x6d } },
            { "ADD IY,A", new byte[] { 0xfd, 0xed, 0x6d } },

            // Aliases for "ADD A,..." with implicit A
            { "ADD (HL+IX)", new byte[] { 0xdd, 0x81 } },
            { "ADD (HL+IY)", new byte[] { 0xdd, 0x82 } },
            { "ADD (IX+IY)", new byte[] { 0xdd, 0x83 } },

            { "ADDW HL,(HL)", new byte[] { 0xdd, 0xed, 0xc6 } },
            { "ADDW HL,BC", new byte[] { 0xed, 0xc6 } },
            { "ADDW HL,DE", new byte[] { 0xed, 0xd6 } },
            { "ADDW HL,HL", new byte[] { 0xed, 0xe6 } },
            { "ADDW HL,IX", new byte[] { 0xdd, 0xed, 0xe6 } },
            { "ADDW HL,IY", new byte[] { 0xfd, 0xed, 0xe6 } },
            { "ADDW HL,SP", new byte[] { 0xed, 0xf6 } },

            { "AND A,(HL+IX)", new byte[] { 0xdd, 0xa1 } },
            { "AND A,(HL+IY)", new byte[] { 0xdd, 0xa2 } },
            { "AND A,(IX+IY)", new byte[] { 0xdd, 0xa3 } },

            // Aliases for "AND A,..." with implicit A
            { "AND (HL+IX)", new byte[] { 0xdd, 0xa1 } },
            { "AND (HL+IY)", new byte[] { 0xdd, 0xa2 } },
            { "AND (IX+IY)", new byte[] { 0xdd, 0xa3 } },

            { "CALL (HL)", new byte[] { 0xdd, 0xcd } },
            { "CALL C,(HL)", new byte[] { 0xdd, 0xdc } },
            { "CALL M,(HL)", new byte[] { 0xdd, 0xfc } },
            { "CALL NC,(HL)", new byte[] { 0xdd, 0xd4 } },
            { "CALL NZ,(HL)", new byte[] { 0xdd, 0xc4 } },
            { "CALL P,(HL)", new byte[] { 0xdd, 0xf4 } },
            { "CALL PE,(HL)", new byte[] { 0xdd, 0xec } },
            { "CALL PO,(HL)", new byte[] { 0xdd, 0xe4 } },
            { "CALL Z,(HL)", new byte[] { 0xdd, 0xcc } },

            { "CP A,(HL+IX)", new byte[] { 0xdd, 0xb9 } },
            { "CP A,(HL+IY)", new byte[] { 0xdd, 0xba } },
            { "CP A,(IX+IY)", new byte[] { 0xdd, 0xbb } },

            // Aliases for "CP A,..." with implicit A
            { "CP (HL+IX)", new byte[] { 0xdd, 0xb9 } },
            { "CP (HL+IY)", new byte[] { 0xdd, 0xba } },
            { "CP (IX+IY)", new byte[] { 0xdd, 0xbb } },

            { "CPW HL,(HL)", new byte[] { 0xdd, 0xed, 0xc7 } },
            { "CPW HL,BC", new byte[] { 0xed, 0xc7 } },
            { "CPW HL,DE", new byte[] { 0xed, 0xd7 } },
            { "CPW HL,HL", new byte[] { 0xed, 0xe7 } },
            { "CPW HL,IX", new byte[] { 0xdd, 0xed, 0xe7 } },
            { "CPW HL,IY", new byte[] { 0xfd, 0xed, 0xe7 } },
            { "CPW HL,SP", new byte[] { 0xed, 0xf7 } },

            { "DEC (HL+IX)", new byte[] { 0xdd, 0x0d } },
            { "DEC (HL+IY)", new byte[] { 0xdd, 0x15 } },
            { "DEC (IX+IY)", new byte[] { 0xdd, 0x1d } },

            { "DECW (HL)", new byte[] { 0xdd, 0x0b } },
            { "DECW BC", new byte[] { 0x0b } },
            { "DECW DE", new byte[] { 0x1b } },
            { "DECW HL", new byte[] { 0x2b } },
            { "DECW IX", new byte[] { 0xdd, 0x2b } },
            { "DECW IY", new byte[] { 0xfd, 0x2b } },
            { "DECW SP", new byte[] { 0x3b } },

            { "DIV HL,(HL)", new byte[] { 0xed, 0xf4 } },
            { "DIV HL,(HL+IX)", new byte[] { 0xdd, 0xed, 0xcc } },
            { "DIV HL,(HL+IY)", new byte[] { 0xdd, 0xed, 0xd4 } },
            { "DIV HL,(IX+IY)", new byte[] { 0xdd, 0xed, 0xdc } },
            { "DIV HL,A", new byte[] { 0xed, 0xfc } },
            { "DIV HL,B", new byte[] { 0xed, 0xc4 } },
            { "DIV HL,C", new byte[] { 0xed, 0xcc } },
            { "DIV HL,D", new byte[] { 0xed, 0xd4 } },
            { "DIV HL,E", new byte[] { 0xed, 0xdc } },
            { "DIV HL,H", new byte[] { 0xed, 0xe4 } },
            { "DIV HL,IXH", new byte[] { 0xdd, 0xed, 0xe4 } },
            { "DIV HL,IXL", new byte[] { 0xdd, 0xed, 0xec } },
            { "DIV HL,IYH", new byte[] { 0xfd, 0xed, 0xe4 } },
            { "DIV HL,IYL", new byte[] { 0xfd, 0xed, 0xec } },
            { "DIV HL,L", new byte[] { 0xed, 0xec } },

            { "DIVU HL,(HL)", new byte[] { 0xed, 0xf5 } },
            { "DIVU HL,(HL+IX)", new byte[] { 0xdd, 0xed, 0xcd } },
            { "DIVU HL,(HL+IY)", new byte[] { 0xdd, 0xed, 0xd5 } },
            { "DIVU HL,(IX+IY)", new byte[] { 0xdd, 0xed, 0xdd } },
            { "DIVU HL,A", new byte[] { 0xed, 0xfd } },
            { "DIVU HL,B", new byte[] { 0xed, 0xc5 } },
            { "DIVU HL,C", new byte[] { 0xed, 0xcd } },
            { "DIVU HL,D", new byte[] { 0xed, 0xd5 } },
            { "DIVU HL,E", new byte[] { 0xed, 0xdd } },
            { "DIVU HL,H", new byte[] { 0xed, 0xe5 } },
            { "DIVU HL,IXH", new byte[] { 0xdd, 0xed, 0xe5 } },
            { "DIVU HL,IXL", new byte[] { 0xdd, 0xed, 0xed } },
            { "DIVU HL,IYH", new byte[] { 0xfd, 0xed, 0xe5 } },
            { "DIVU HL,IYL", new byte[] { 0xfd, 0xed, 0xed } },
            { "DIVU HL,L", new byte[] { 0xed, 0xed } },

            { "DIVUW DEHL,(HL)", new byte[] { 0xdd, 0xed, 0xcb } },
            { "DIVUW DEHL,BC", new byte[] { 0xed, 0xcb } },
            { "DIVUW DEHL,DE", new byte[] { 0xed, 0xdb } },
            { "DIVUW DEHL,HL", new byte[] { 0xed, 0xeb } },
            { "DIVUW DEHL,IX", new byte[] { 0xdd, 0xed, 0xeb } },
            { "DIVUW DEHL,IY", new byte[] { 0xfd, 0xed, 0xeb } },
            { "DIVUW DEHL,SP", new byte[] { 0xed, 0xfb } },
            { "DIVW DEHL,(HL)", new byte[] { 0xdd, 0xed, 0xca } },
            { "DIVW DEHL,BC", new byte[] { 0xed, 0xca } },
            { "DIVW DEHL,DE", new byte[] { 0xed, 0xda } },
            { "DIVW DEHL,HL", new byte[] { 0xed, 0xea } },
            { "DIVW DEHL,IX", new byte[] { 0xdd, 0xed, 0xea } },
            { "DIVW DEHL,IY", new byte[] { 0xfd, 0xed, 0xea } },
            { "DIVW DEHL,SP", new byte[] { 0xed, 0xfa } },

            { "EPUF", new byte[] { 0xed, 0x97 } },

            { "EPUI", new byte[] { 0xed, 0x9f } },

            { "EPUM (HL)", new byte[] { 0xed, 0xa6 } },
            { "EPUM (HL+IX)", new byte[] { 0xed, 0x8c } },
            { "EPUM (HL+IY)", new byte[] { 0xed, 0x94 } },
            { "EPUM (IX+IY)", new byte[] { 0xed, 0x9c } },

            { "EX A,(HL)", new byte[] { 0xed, 0x37 } },
            { "EX A,(HL+IX)", new byte[] { 0xdd, 0xed, 0x0f } },
            { "EX A,(HL+IY)", new byte[] { 0xdd, 0xed, 0x17 } },
            { "EX A,(IX+IY)", new byte[] { 0xdd, 0xed, 0x1f } },
            { "EX A,A", new byte[] { 0xed, 0x3f } },
            { "EX A,B", new byte[] { 0xed, 0x07 } },
            { "EX A,C", new byte[] { 0xed, 0x0f } },
            { "EX A,D", new byte[] { 0xed, 0x17 } },
            { "EX A,E", new byte[] { 0xed, 0x1f } },
            { "EX A,H", new byte[] { 0xed, 0x27 } },
            { "EX A,IXH", new byte[] { 0xdd, 0xed, 0x27 } },
            { "EX A,IXL", new byte[] { 0xdd, 0xed, 0x2f } },
            { "EX A,IYH", new byte[] { 0xfd, 0xed, 0x27 } },
            { "EX A,IYL", new byte[] { 0xfd, 0xed, 0x2f } },
            { "EX A,L", new byte[] { 0xed, 0x2f } },
            { "EX H,L", new byte[] { 0xed, 0xef } },
            { "EX IX,HL", new byte[] { 0xdd, 0xeb } },
            { "EX IY,HL", new byte[] { 0xfd, 0xeb } },

            { "EXTS A", new byte[] { 0xed, 0x64 } },
            { "EXTS HL", new byte[] { 0xed, 0x6c } },

            { "IN (HL+IX),(C)", new byte[] { 0xdd, 0xed, 0x48 } },
            { "IN (HL+IY),(C)", new byte[] { 0xdd, 0xed, 0x50 } },
            { "IN (IX+IY),(C)", new byte[] { 0xdd, 0xed, 0x58 } },
            { "IN HL,(C)", new byte[] { 0xed, 0xb7 } },
            { "IN IXH,(C)", new byte[] { 0xdd, 0xed, 0x60 } },
            { "IN IXL,(C)", new byte[] { 0xdd, 0xed, 0x68 } },
            { "IN IYH,(C)", new byte[] { 0xfd, 0xed, 0x60 } },
            { "IN IYL,(C)", new byte[] { 0xfd, 0xed, 0x68 } },

            { "INC (HL+IX)", new byte[] { 0xdd, 0x0c } },
            { "INC (HL+IY)", new byte[] { 0xdd, 0x14 } },
            { "INC (IX+IY)", new byte[] { 0xdd, 0x1c } },

            { "INCW (HL)", new byte[] { 0xdd, 0x03 } },
            { "INCW BC", new byte[] { 0x03 } },
            { "INCW DE", new byte[] { 0x13 } },
            { "INCW HL", new byte[] { 0x23 } },
            { "INCW IX", new byte[] { 0xdd, 0x23 } },
            { "INCW IY", new byte[] { 0xfd, 0x23 } },
            { "INCW SP", new byte[] { 0x33 } },

            { "INDRW", new byte[] { 0xed, 0x9a } },

            { "INDW", new byte[] { 0xed, 0x8a } },

            { "INIRW", new byte[] { 0xed, 0x92 } },

            { "INIW", new byte[] { 0xed, 0x82 } },

            { "INW HL,(C)", new byte[] { 0xed, 0xb7 } },

            { "JP C,(HL)", new byte[] { 0xdd, 0xda } },
            { "JP M,(HL)", new byte[] { 0xdd, 0xfa } },
            { "JP NC,(HL)", new byte[] { 0xdd, 0xd2 } },
            { "JP NZ,(HL)", new byte[] { 0xdd, 0xc2 } },
            { "JP P,(HL)", new byte[] { 0xdd, 0xf2 } },
            { "JP PE,(HL)", new byte[] { 0xdd, 0xea } },
            { "JP PO,(HL)", new byte[] { 0xdd, 0xe2 } },
            { "JP Z,(HL)", new byte[] { 0xdd, 0xca } },

            { "LD (HL),BC", new byte[] { 0xed, 0x0e } },
            { "LD (HL),DE", new byte[] { 0xed, 0x1e } },
            { "LD (HL),HL", new byte[] { 0xed, 0x2e } },
            { "LD (HL),SP", new byte[] { 0xed, 0x3e } },
            { "LD (HL+IX),A", new byte[] { 0xed, 0x0b } },
            { "LD (HL+IX),HL", new byte[] { 0xed, 0x0d } },
            { "LD (HL+IX),IX", new byte[] { 0xdd, 0xed, 0x0d } },
            { "LD (HL+IX),IY", new byte[] { 0xfd, 0xed, 0x0d } },
            { "LD (HL+IY),A", new byte[] { 0xed, 0x13 } },
            { "LD (HL+IY),HL", new byte[] { 0xed, 0x15 } },
            { "LD (HL+IY),IX", new byte[] { 0xdd, 0xed, 0x15 } },
            { "LD (HL+IY),IY", new byte[] { 0xfd, 0xed, 0x15 } },
            { "LD (IX+IY),A", new byte[] { 0xed, 0x1b } },
            { "LD (IX+IY),HL", new byte[] { 0xed, 0x1d } },
            { "LD (IX+IY),IX", new byte[] { 0xdd, 0xed, 0x1d } },
            { "LD (IX+IY),IY", new byte[] { 0xfd, 0xed, 0x1d } },
            { "LD A,(HL+IX)", new byte[] { 0xdd, 0x79 } },
            { "LD A,(HL+IY)", new byte[] { 0xdd, 0x7a } },
            { "LD A,(IX+IY)", new byte[] { 0xdd, 0x7b } },
            { "LD BC,(HL)", new byte[] { 0xed, 0x06 } },
            { "LD DE,(HL)", new byte[] { 0xed, 0x16 } },
            { "LD HL,(HL)", new byte[] { 0xed, 0x26 } },
            { "LD HL,(HL+IX)", new byte[] { 0xed, 0x0c } },
            { "LD HL,(HL+IY)", new byte[] { 0xed, 0x14 } },
            { "LD HL,(IX+IY)", new byte[] { 0xed, 0x1c } },
            { "LD IX,(HL+IX)", new byte[] { 0xdd, 0xed, 0x0c } },
            { "LD IX,(HL+IY)", new byte[] { 0xdd, 0xed, 0x14 } },
            { "LD IX,(IX+IY)", new byte[] { 0xdd, 0xed, 0x1c } },
            { "LD IY,(HL+IX)", new byte[] { 0xfd, 0xed, 0x0c } },
            { "LD IY,(HL+IY)", new byte[] { 0xfd, 0xed, 0x14 } },
            { "LD IY,(IX+IY)", new byte[] { 0xfd, 0xed, 0x1c } },
            { "LD SP,(HL)", new byte[] { 0xed, 0x36 } },

            { "LDA HL,(HL+IX)", new byte[] { 0xed, 0x0a } },
            { "LDA HL,(HL+IY)", new byte[] { 0xed, 0x12 } },
            { "LDA HL,(IX+IY)", new byte[] { 0xed, 0x1a } },
            { "LDA IX,(HL+IX)", new byte[] { 0xdd, 0xed, 0x0a } },
            { "LDA IX,(HL+IY)", new byte[] { 0xdd, 0xed, 0x12 } },
            { "LDA IX,(IX+IY)", new byte[] { 0xdd, 0xed, 0x1a } },
            { "LDA IY,(HL+IX)", new byte[] { 0xfd, 0xed, 0x0a } },
            { "LDA IY,(HL+IY)", new byte[] { 0xfd, 0xed, 0x12 } },
            { "LDA IY,(IX+IY)", new byte[] { 0xfd, 0xed, 0x1a } },

            { "LDCTL (C),HL", new byte[] { 0xed, 0x6e } },
            { "LDCTL (C),IX", new byte[] { 0xdd, 0xed, 0x6e } },
            { "LDCTL (C),IY", new byte[] { 0xfd, 0xed, 0x6e } },
            { "LDCTL HL,(C)", new byte[] { 0xed, 0x66 } },
            { "LDCTL HL,USP", new byte[] { 0xed, 0x87 } },
            { "LDCTL IX,(C)", new byte[] { 0xdd, 0xed, 0x66 } },
            { "LDCTL IX,USP", new byte[] { 0xdd, 0xed, 0x87 } },
            { "LDCTL IY,(C)", new byte[] { 0xfd, 0xed, 0x66 } },
            { "LDCTL IY,USP", new byte[] { 0xfd, 0xed, 0x87 } },
            { "LDCTL USP,HL", new byte[] { 0xed, 0x8f } },
            { "LDCTL USP,IX", new byte[] { 0xdd, 0xed, 0x8f } },
            { "LDCTL USP,IY", new byte[] { 0xfd, 0xed, 0x8f } },

            { "LDUD (HL),A", new byte[] { 0xed, 0x8e } },
            { "LDUD A,(HL)", new byte[] { 0xed, 0x86 } },

            { "LDUP (HL),A", new byte[] { 0xed, 0x9e } },
            { "LDUP A,(HL)", new byte[] { 0xed, 0x96 } },

            { "LDW (HL),BC", new byte[] { 0xed, 0x0e } },
            { "LDW (HL),DE", new byte[] { 0xed, 0x1e } },
            { "LDW (HL),HL", new byte[] { 0xed, 0x2e } },
            { "LDW (HL),SP", new byte[] { 0xed, 0x3e } },
            { "LDW (HL+IX),HL", new byte[] { 0xed, 0x0d } },
            { "LDW (HL+IX),IX", new byte[] { 0xdd, 0xed, 0x0d } },
            { "LDW (HL+IX),IY", new byte[] { 0xfd, 0xed, 0x0d } },
            { "LDW (HL+IY),HL", new byte[] { 0xed, 0x15 } },
            { "LDW (HL+IY),IX", new byte[] { 0xdd, 0xed, 0x15 } },
            { "LDW (HL+IY),IY", new byte[] { 0xfd, 0xed, 0x15 } },
            { "LDW (IX+IY),HL", new byte[] { 0xed, 0x1d } },
            { "LDW (IX+IY),IX", new byte[] { 0xdd, 0xed, 0x1d } },
            { "LDW (IX+IY),IY", new byte[] { 0xfd, 0xed, 0x1d } },
            { "LDW BC,(HL)", new byte[] { 0xed, 0x06 } },
            { "LDW DE,(HL)", new byte[] { 0xed, 0x16 } },
            { "LDW HL,(HL)", new byte[] { 0xed, 0x26 } },
            { "LDW HL,(HL+IX)", new byte[] { 0xed, 0x0c } },
            { "LDW HL,(HL+IY)", new byte[] { 0xed, 0x14 } },
            { "LDW HL,(IX+IY)", new byte[] { 0xed, 0x1c } },
            { "LDW IX,(HL+IX)", new byte[] { 0xdd, 0xed, 0x0c } },
            { "LDW IX,(HL+IY)", new byte[] { 0xdd, 0xed, 0x14 } },
            { "LDW IX,(IX+IY)", new byte[] { 0xdd, 0xed, 0x1c } },
            { "LDW IY,(HL+IX)", new byte[] { 0xfd, 0xed, 0x0c } },
            { "LDW IY,(HL+IY)", new byte[] { 0xfd, 0xed, 0x14 } },
            { "LDW IY,(IX+IY)", new byte[] { 0xfd, 0xed, 0x1c } },
            { "LDW SP,(HL)", new byte[] { 0xed, 0x36 } },
            { "LDW SP,HL", new byte[] { 0xf9 } },
            { "LDW SP,IX", new byte[] { 0xdd, 0xf9 } },
            { "LDW SP,IY", new byte[] { 0xfd, 0xf9 } },

            { "MEPU (HL)", new byte[] { 0xed, 0xae } },
            { "MEPU (HL+IX)", new byte[] { 0xed, 0x8d } },
            { "MEPU (HL+IY)", new byte[] { 0xed, 0x95 } },
            { "MEPU (IX+IY)", new byte[] { 0xed, 0x9d } },

            { "MULT A,(HL)", new byte[] { 0xed, 0xf0 } },
            { "MULT A,(HL+IX)", new byte[] { 0xdd, 0xed, 0xc8 } },
            { "MULT A,(HL+IY)", new byte[] { 0xdd, 0xed, 0xd0 } },
            { "MULT A,(IX+IY)", new byte[] { 0xdd, 0xed, 0xd8 } },
            { "MULT A,A", new byte[] { 0xed, 0xf8 } },
            { "MULT A,B", new byte[] { 0xed, 0xc0 } },
            { "MULT A,C", new byte[] { 0xed, 0xc8 } },
            { "MULT A,D", new byte[] { 0xed, 0xd0 } },
            { "MULT A,E", new byte[] { 0xed, 0xd8 } },
            { "MULT A,H", new byte[] { 0xed, 0xe0 } },
            { "MULT A,IXH", new byte[] { 0xdd, 0xed, 0xe0 } },
            { "MULT A,IXL", new byte[] { 0xdd, 0xed, 0xe8 } },
            { "MULT A,IYH", new byte[] { 0xfd, 0xed, 0xe0 } },
            { "MULT A,IYL", new byte[] { 0xfd, 0xed, 0xe8 } },
            { "MULT A,L", new byte[] { 0xed, 0xe8 } },

            { "MULTU A,(HL)", new byte[] { 0xed, 0xf1 } },
            { "MULTU A,(HL+IX)", new byte[] { 0xdd, 0xed, 0xc9 } },
            { "MULTU A,(HL+IY)", new byte[] { 0xdd, 0xed, 0xd1 } },
            { "MULTU A,(IX+IY)", new byte[] { 0xdd, 0xed, 0xd9 } },
            { "MULTU A,A", new byte[] { 0xed, 0xf9 } },
            { "MULTU A,B", new byte[] { 0xed, 0xc1 } },
            { "MULTU A,C", new byte[] { 0xed, 0xc9 } },
            { "MULTU A,D", new byte[] { 0xed, 0xd1 } },
            { "MULTU A,E", new byte[] { 0xed, 0xd9 } },
            { "MULTU A,H", new byte[] { 0xed, 0xe1 } },
            { "MULTU A,IXH", new byte[] { 0xdd, 0xed, 0xe1 } },
            { "MULTU A,IXL", new byte[] { 0xdd, 0xed, 0xe9 } },
            { "MULTU A,IYH", new byte[] { 0xfd, 0xed, 0xe1 } },
            { "MULTU A,IYL", new byte[] { 0xfd, 0xed, 0xe9 } },
            { "MULTU A,L", new byte[] { 0xed, 0xe9 } },

            { "MULTUW HL,(HL)", new byte[] { 0xdd, 0xed, 0xc3 } },
            { "MULTUW HL,BC", new byte[] { 0xed, 0xc3 } },
            { "MULTUW HL,DE", new byte[] { 0xed, 0xd3 } },
            { "MULTUW HL,HL", new byte[] { 0xed, 0xe3 } },
            { "MULTUW HL,IX", new byte[] { 0xdd, 0xed, 0xe3 } },
            { "MULTUW HL,IY", new byte[] { 0xfd, 0xed, 0xe3 } },
            { "MULTUW HL,SP", new byte[] { 0xed, 0xf3 } },

            { "MULTW HL,(HL)", new byte[] { 0xdd, 0xed, 0xc2 } },
            { "MULTW HL,BC", new byte[] { 0xed, 0xc2 } },
            { "MULTW HL,DE", new byte[] { 0xed, 0xd2 } },
            { "MULTW HL,HL", new byte[] { 0xed, 0xe2 } },
            { "MULTW HL,IX", new byte[] { 0xdd, 0xed, 0xe2 } },
            { "MULTW HL,IY", new byte[] { 0xfd, 0xed, 0xe2 } },
            { "MULTW HL,SP", new byte[] { 0xed, 0xf2 } },

            { "NEG HL", new byte[] { 0xed, 0x4c } },

            { "OR A,(HL+IX)", new byte[] { 0xdd, 0xb1 } },
            { "OR A,(HL+IY)", new byte[] { 0xdd, 0xb2 } },
            { "OR A,(IX+IY)", new byte[] { 0xdd, 0xb3 } },

            // Aliases for "OR A,..." with implicit A
            { "OR (HL+IX)", new byte[] { 0xdd, 0xb1 } },
            { "OR (HL+IY)", new byte[] { 0xdd, 0xb2 } },
            { "OR (IX+IY)", new byte[] { 0xdd, 0xb3 } },

            { "OTDRW", new byte[] { 0xed, 0x9b } },

            { "OTIRW", new byte[] { 0xed, 0x93 } },

            { "OUT (C),(HL+IX)", new byte[] { 0xdd, 0xed, 0x49 } },
            { "OUT (C),(HL+IY)", new byte[] { 0xdd, 0xed, 0x51 } },
            { "OUT (C),(IX+IY)", new byte[] { 0xdd, 0xed, 0x59 } },
            { "OUT (C),HL", new byte[] { 0xed, 0xbf } },
            { "OUT (C),IXH", new byte[] { 0xdd, 0xed, 0x61 } },
            { "OUT (C),IXL", new byte[] { 0xdd, 0xed, 0x69 } },
            { "OUT (C),IYH", new byte[] { 0xfd, 0xed, 0x61 } },
            { "OUT (C),IYL", new byte[] { 0xfd, 0xed, 0x69 } },

            { "OUTDW", new byte[] { 0xed, 0x8b } },

            { "OUTIW", new byte[] { 0xed, 0x83 } },

            { "OUTW (C),HL", new byte[] { 0xed, 0xbf } },

            { "PCACHE", new byte[] { 0xed, 0x65 } },

            { "POP (HL)", new byte[] { 0xdd, 0xc1 } },

            { "PUSH (HL)", new byte[] { 0xdd, 0xc5 } },

            { "RETIL", new byte[] { 0xed, 0x55 } },

            { "SBC A,(HL+IX)", new byte[] { 0xdd, 0x99 } },
            { "SBC A,(HL+IY)", new byte[] { 0xdd, 0x9a } },
            { "SBC A,(IX+IY)", new byte[] { 0xdd, 0x9b } },
            { "SBC IX,BC", new byte[] { 0xdd, 0xed, 0x42 } },
            { "SBC IX,DE", new byte[] { 0xdd, 0xed, 0x52 } },
            { "SBC IX,IX", new byte[] { 0xdd, 0xed, 0x62 } },
            { "SBC IX,SP", new byte[] { 0xdd, 0xed, 0x72 } },
            { "SBC IY,BC", new byte[] { 0xfd, 0xed, 0x42 } },
            { "SBC IY,DE", new byte[] { 0xfd, 0xed, 0x52 } },
            { "SBC IY,IY", new byte[] { 0xfd, 0xed, 0x62 } },
            { "SBC IY,SP", new byte[] { 0xfd, 0xed, 0x72 } },

            // Aliases for "SBC A,..." with implicit A
            { "SBC (HL+IX)", new byte[] { 0xdd, 0x99 } },
            { "SBC (HL+IY)", new byte[] { 0xdd, 0x9a } },
            { "SBC (IX+IY)", new byte[] { 0xdd, 0x9b } },

            { "SUB A,(HL+IX)", new byte[] { 0xdd, 0x91 } },
            { "SUB A,(HL+IY)", new byte[] { 0xdd, 0x92 } },
            { "SUB A,(IX+IY)", new byte[] { 0xdd, 0x93 } },

            // Aliases for "SUB A,..." with implicit A
            { "SUB (HL+IX)", new byte[] { 0xdd, 0x91 } },
            { "SUB (HL+IY)", new byte[] { 0xdd, 0x92 } },
            { "SUB (IX+IY)", new byte[] { 0xdd, 0x93 } },

            { "SUBW HL,(HL)", new byte[] { 0xdd, 0xed, 0xce } },
            { "SUBW HL,BC", new byte[] { 0xed, 0xce } },
            { "SUBW HL,DE", new byte[] { 0xed, 0xde } },
            { "SUBW HL,HL", new byte[] { 0xed, 0xee } },
            { "SUBW HL,IX", new byte[] { 0xdd, 0xed, 0xee } },
            { "SUBW HL,IY", new byte[] { 0xfd, 0xed, 0xee } },
            { "SUBW HL,SP", new byte[] { 0xed, 0xfe } },

            { "TSET (HL)", new byte[] { 0xcb, 0x36 } },
            { "TSET A", new byte[] { 0xcb, 0x37 } },
            { "TSET B", new byte[] { 0xcb, 0x30 } },
            { "TSET C", new byte[] { 0xcb, 0x31 } },
            { "TSET D", new byte[] { 0xcb, 0x32 } },
            { "TSET E", new byte[] { 0xcb, 0x33 } },
            { "TSET H", new byte[] { 0xcb, 0x34 } },
            { "TSET L", new byte[] { 0xcb, 0x35 } },

            { "TSTI (C)", new byte[] { 0xed, 0x70 } },

            { "XOR A,(HL+IX)", new byte[] { 0xdd, 0xa9 } },
            { "XOR A,(HL+IY)", new byte[] { 0xdd, 0xaa } },
            { "XOR A,(IX+IY)", new byte[] { 0xdd, 0xab } },

            // Aliases for "XOR A,..." with implicit A
            { "XOR (HL+IX)", new byte[] { 0xdd, 0xa9 } },
            { "XOR (HL+IY)", new byte[] { 0xdd, 0xaa } },
            { "XOR (IX+IY)", new byte[] { 0xdd, 0xab } }
        };


        /// <summary>
        /// Zero-index versions of the exclusive Z280 instructions that have an "(RR+n)" argument, where RR = HL, IX, IY, PC, SP
        /// (versions where n=0 and the "+n" part is omitted entirely) and don't have any other variable argument
        /// (so not including the instructions having "(RR+n),n").
        /// 
        /// Some instructions have a short index version, "(RR+byte)", which already existed in the Z80; and a long index version,
        /// "(RR+word)" that is new to the Z280. In these cases the first byte array contains the opcodes for the short version
        /// and the second one contains the opcodes for the long version. When there's only one version, the second array is null.
        /// 
        /// There are a few instructions that existed as "(HL)" in the Z80 and also exist as "(HL+word)" in the Z280, e.g. "ADC A,(HL)".
        /// There are included here only as the "(HL+word)" version since the short versions are already included in the Z80 fixed instructions list.
        /// </summary>
        static readonly Dictionary<string, (byte[], byte[])> ZeroIndexExclusiveZ280Instructions = new(StringComparer.OrdinalIgnoreCase) {
            { "ADC A,(HL)", ( new byte[] { 0xfd, 0x8b, 0x00, 0x00 }, null ) },
            { "ADC A,(IX)", ( new byte[] { 0xdd, 0x8e, 0x00 }, new byte[] { 0xfd, 0x89, 0x00, 0x00 } ) },
            { "ADC A,(IY)", ( new byte[] { 0xfd, 0x8e, 0x00 }, new byte[] { 0xfd, 0x8a, 0x00, 0x00 } ) },
            { "ADC A,(PC)", ( new byte[] { 0xfd, 0x88, 0x00, 0x00 }, null ) },
            { "ADC A,(SP)", ( new byte[] { 0xdd, 0x88, 0x00, 0x00 }, null ) },

            { "ADD A,(HL)", ( new byte[] { 0xfd, 0x83, 0x00, 0x00 }, null ) },
            { "ADD A,(IX)", ( new byte[] { 0xdd, 0x86, 0x00 }, new byte[] { 0xfd, 0x81, 0x00, 0x00 } ) },
            { "ADD A,(IY)", ( new byte[] { 0xfd, 0x86, 0x00 }, new byte[] { 0xfd, 0x82, 0x00, 0x00 } ) },
            { "ADD A,(PC)", ( new byte[] { 0xfd, 0x80, 0x00, 0x00 }, null ) },
            { "ADD A,(SP)", ( new byte[] { 0xdd, 0x80, 0x00, 0x00 }, null ) },

            { "ADDW HL,(IX)", ( new byte[] { 0xfd, 0xed, 0xc6, 0x00, 0x00 }, null ) },
            { "ADDW HL,(IY)", ( new byte[] { 0xfd, 0xed, 0xd6, 0x00, 0x00 }, null ) },
            { "ADDW HL,(PC)", ( new byte[] { 0xdd, 0xed, 0xf6, 0x00, 0x00 }, null ) },

            { "AND A,(HL)", ( new byte[] { 0xfd, 0xa3, 0x00, 0x00 } , null ) },
            { "AND A,(IX)", ( new byte[] { 0xdd, 0xa6, 0x00 }, new byte[] { 0xfd, 0xa1, 0x00, 0x00 } ) },
            { "AND A,(IY)", ( new byte[] { 0xfd, 0xa6, 0x00 }, new byte[] { 0xfd, 0xa2, 0x00, 0x00 } ) },
            { "AND A,(PC)", ( new byte[] { 0xfd, 0xa0, 0x00, 0x00 }, null ) },
            { "AND A,(SP)", ( new byte[] { 0xdd, 0xa0, 0x00, 0x00 }, null ) },

            { "CALL (PC)", ( new byte[] { 0xfd, 0xcd, 0x00, 0x00 }, null ) },
            { "CALL C,(PC)", ( new byte[] { 0xfd, 0xdc, 0x00, 0x00 }, null ) },
            { "CALL M,(PC)", ( new byte[] { 0xfd, 0xfc, 0x00, 0x00 }, null ) },
            { "CALL NC,(PC)", ( new byte[] { 0xfd, 0xd4, 0x00, 0x00 }, null ) },
            { "CALL NZ,(PC)", ( new byte[] { 0xfd, 0xc4, 0x00, 0x00 }, null ) },
            { "CALL P,(PC)", ( new byte[] { 0xfd, 0xf4, 0x00, 0x00 }, null ) },
            { "CALL PE,(PC)", ( new byte[] { 0xfd, 0xec, 0x00, 0x00 }, null ) },
            { "CALL PO,(PC)", ( new byte[] { 0xfd, 0xe4, 0x00, 0x00 }, null ) },
            { "CALL Z,(PC)", ( new byte[] { 0xfd, 0xcc, 0x00, 0x00 }, null ) },

            { "CP A,(HL)", ( new byte[] { 0xfd, 0xbb, 0x00, 0x00 } , null ) },
            { "CP A,(IX)", ( new byte[] { 0xdd, 0xbe, 0x00 }, new byte[] { 0xfd, 0xb9, 0x00, 0x00 } ) },
            { "CP A,(IY)", ( new byte[] { 0xfd, 0xbe, 0x00 }, new byte[] { 0xfd, 0xba, 0x00, 0x00 } ) },
            { "CP A,(PC)", ( new byte[] { 0xfd, 0xb8, 0x00, 0x00 }, null ) },
            { "CP A,(SP)", ( new byte[] { 0xdd, 0xb8, 0x00, 0x00 }, null ) },

            { "CPW HL,(IX)", ( new byte[] { 0xfd, 0xed, 0xc7, 0x00, 0x00 }, null ) },
            { "CPW HL,(IY)", ( new byte[] { 0xfd, 0xed, 0xd7, 0x00, 0x00 }, null ) },
            { "CPW HL,(PC)", ( new byte[] { 0xdd, 0xed, 0xf7, 0x00, 0x00 }, null ) },

            { "DEC (HL)", ( new byte[] { 0xfd, 0x1d, 0x00, 0x00 } , null ) },
            { "DEC (IX)", ( new byte[] { 0xdd, 0x35, 0x00 }, new byte[] { 0xfd, 0x0d, 0x00, 0x00 } ) },
            { "DEC (IY)", ( new byte[] { 0xfd, 0x35, 0x00 }, new byte[] { 0xfd, 0x15, 0x00, 0x00 } ) },
            { "DEC (PC)", ( new byte[] { 0xfd, 0x05, 0x00, 0x00 }, null ) },
            { "DEC (SP)", ( new byte[] { 0xdd, 0x05, 0x00, 0x00 }, null ) },

            { "DECW (IX)", ( new byte[] { 0xfd, 0x0b, 0x00, 0x00 }, null ) },
            { "DECW (IY)", ( new byte[] { 0xfd, 0x1b, 0x00, 0x00 }, null ) },
            { "DECW (PC)", ( new byte[] { 0xdd, 0x3b, 0x00, 0x00 }, null ) },

            { "DIV HL,(HL)", ( new byte[] { 0xed, 0xf4 }, new byte[] { 0xfd, 0xed, 0xdc, 0x00, 0x00 } ) },
            { "DIV HL,(IX)", ( new byte[] { 0xdd, 0xed, 0xf4, 0x00 }, new byte[] { 0xfd, 0xed, 0xcc, 0x00, 0x00 } ) },
            { "DIV HL,(IY)", ( new byte[] { 0xfd, 0xed, 0xf4, 0x00 }, new byte[] { 0xfd, 0xed, 0xd4, 0x00, 0x00 } ) },
            { "DIV HL,(PC)", ( new byte[] { 0xfd, 0xed, 0xc4, 0x00, 0x00 }, null ) },
            { "DIV HL,(SP)", ( new byte[] { 0xdd, 0xed, 0xc4, 0x00, 0x00 }, null ) },

            { "DIVU HL,(HL)", ( new byte[] { 0xed, 0xf5 }, new byte[] { 0xfd, 0xed, 0xdd, 0x00, 0x00 } ) },
            { "DIVU HL,(IX)", ( new byte[] { 0xdd, 0xed, 0xf5, 0x00 }, new byte[] { 0xfd, 0xed, 0xcd, 0x00, 0x00 } ) },
            { "DIVU HL,(IY)", ( new byte[] { 0xfd, 0xed, 0xf5, 0x00 }, new byte[] { 0xfd, 0xed, 0xd5, 0x00, 0x00 } ) },
            { "DIVU HL,(PC)", ( new byte[] { 0xfd, 0xed, 0xc5, 0x00, 0x00 }, null ) },
            { "DIVU HL,(SP)", ( new byte[] { 0xdd, 0xed, 0xc5, 0x00, 0x00 }, null ) },

            { "DIVUW DEHL,(IX)", ( new byte[] { 0xfd, 0xed, 0xcb, 0x00, 0x00 }, null ) },
            { "DIVUW DEHL,(IY)", ( new byte[] { 0xfd, 0xed, 0xdb, 0x00, 0x00 }, null ) },
            { "DIVUW DEHL,(PC)", ( new byte[] { 0xdd, 0xed, 0xfb, 0x00, 0x00 }, null ) },
            { "DIVW DEHL,(IX)", ( new byte[] { 0xfd, 0xed, 0xca, 0x00, 0x00 }, null ) },
            { "DIVW DEHL,(IY)", ( new byte[] { 0xfd, 0xed, 0xda, 0x00, 0x00 }, null ) },
            { "DIVW DEHL,(PC)", ( new byte[] { 0xdd, 0xed, 0xfa, 0x00, 0x00 }, null ) },

            { "EPUM (HL)", ( new byte[] { 0xed, 0xa6 }, new byte[] { 0xed, 0xbc, 0x00, 0x00 } ) },
            { "EPUM (IX)", ( new byte[] { 0xed, 0xac, 0x00, 0x00 }, null ) },
            { "EPUM (IY)", ( new byte[] { 0xed, 0xb4, 0x00, 0x00 }, null ) },
            { "EPUM (PC)", ( new byte[] { 0xed, 0xa4, 0x00, 0x00 }, null ) },
            { "EPUM (SP)", ( new byte[] { 0xed, 0x84, 0x00, 0x00 }, null ) },

            { "EX A,(HL)", ( new byte[] { 0xed, 0x37 }, new byte[] { 0xfd, 0xed, 0x1f, 0x00, 0x00 } ) },
            { "EX A,(IX)", ( new byte[] { 0xdd, 0xed, 0x37, 0x00 }, new byte[] { 0xfd, 0xed, 0x0f, 0x00, 0x00 } ) },
            { "EX A,(IY)", ( new byte[] { 0xfd, 0xed, 0x37, 0x00 }, new byte[] { 0xfd, 0xed, 0x17, 0x00, 0x00 } ) },
            { "EX A,(PC)", ( new byte[] { 0xfd, 0xed, 0x07, 0x00, 0x00 }, null ) },
            { "EX A,(SP)", ( new byte[] { 0xdd, 0xed, 0x07, 0x00, 0x00 }, null ) },

            { "IN (HL),(C)", ( new byte[] { 0xfd, 0xed, 0x58, 0x00, 0x00 }, null ) },
            { "IN (IX),(C)", ( new byte[] { 0xfd, 0xed, 0x48, 0x00, 0x00 }, null ) },
            { "IN (IY),(C)", ( new byte[] { 0xfd, 0xed, 0x50, 0x00, 0x00 }, null ) },
            { "IN (PC),(C)", ( new byte[] { 0xfd, 0xed, 0x40, 0x00, 0x00 }, null ) },
            { "IN (SP),(C)", ( new byte[] { 0xdd, 0xed, 0x40, 0x00, 0x00 }, null ) },

            { "INC (HL)", ( new byte[] { 0xfd, 0x1c, 0x00, 0x00 } , null ) },
            { "INC (IX)", ( new byte[] { 0xdd, 0x34, 0x00 }, new byte[] { 0xfd, 0x0c, 0x00, 0x00 } ) },
            { "INC (IY)", ( new byte[] { 0xfd, 0x34, 0x00 }, new byte[] { 0xfd, 0x14, 0x00, 0x00 } ) },
            { "INC (PC)", ( new byte[] { 0xfd, 0x04, 0x00, 0x00 }, null ) },
            { "INC (SP)", ( new byte[] { 0xdd, 0x04, 0x00, 0x00 }, null ) },

            { "INCW (IX)", ( new byte[] { 0xfd, 0x03, 0x00, 0x00 }, null ) },
            { "INCW (IY)", ( new byte[] { 0xfd, 0x13, 0x00, 0x00 }, null ) },
            { "INCW (PC)", ( new byte[] { 0xdd, 0x33, 0x00, 0x00 }, null ) },

            { "JP (PC)", ( new byte[] { 0xfd, 0xc3, 0x00, 0x00 }, null ) },
            { "JP C,(PC)", ( new byte[] { 0xfd, 0xda, 0x00, 0x00 }, null ) },
            { "JP M,(PC)", ( new byte[] { 0xfd, 0xfa, 0x00, 0x00 }, null ) },
            { "JP NC,(PC)", ( new byte[] { 0xfd, 0xd2, 0x00, 0x00 }, null ) },
            { "JP NZ,(PC)", ( new byte[] { 0xfd, 0xc2, 0x00, 0x00 }, null ) },
            { "JP P,(PC)", ( new byte[] { 0xfd, 0xf2, 0x00, 0x00 }, null ) },
            { "JP PE,(PC)", ( new byte[] { 0xfd, 0xea, 0x00, 0x00 }, null ) },
            { "JP PO,(PC)", ( new byte[] { 0xfd, 0xe2, 0x00, 0x00 }, null ) },
            { "JP Z,(PC)", ( new byte[] { 0xfd, 0xca, 0x00, 0x00 }, null ) },

            { "LD (HL),A", ( new byte[] { 0xed, 0x3b, 0x00, 0x00 }, null ) },
            { "LD (HL),HL", ( new byte[] { 0xed, 0x2e }, new byte[] { 0xed, 0x3d, 0x00, 0x00 } ) },
            { "LD (HL),IX", ( new byte[] { 0xdd, 0xed, 0x3d, 0x00, 0x00 }, null ) },
            { "LD (HL),IY", ( new byte[] { 0xfd, 0xed, 0x3d, 0x00, 0x00 }, null ) },
            { "LD (IX),A", ( new byte[] { 0xdd, 0x77, 0x00 }, new byte[] { 0xed, 0x2b, 0x00, 0x00 } ) },
            { "LD (IX),HL", ( new byte[] { 0xdd, 0xed, 0x2e, 0x00 }, new byte[] { 0xed, 0x2d, 0x00, 0x00 } ) },
            { "LD (IX),IX", ( new byte[] { 0xdd, 0xed, 0x2d, 0x00, 0x00 }, null ) },
            { "LD (IX),IY", ( new byte[] { 0xfd, 0xed, 0x2d, 0x00, 0x00 }, null ) },
            { "LD (IX),BC", ( new byte[] { 0xdd, 0xed, 0x0e, 0x00 }, null ) },
            { "LD (IX),DE", ( new byte[] { 0xdd, 0xed, 0x1e, 0x00 }, null ) },
            { "LD (IX),SP", ( new byte[] { 0xdd, 0xed, 0x3e, 0x00 }, null ) },
            { "LD (IY),A", ( new byte[] { 0xfd, 0x77, 0x00 }, new byte[] { 0xed, 0x33, 0x00, 0x00 } ) },
            { "LD (IY),HL", ( new byte[] { 0xfd, 0xed, 0x2e, 0x00 }, new byte[] { 0xed, 0x35, 0x00, 0x00 } ) },
            { "LD (IY),IX", ( new byte[] { 0xdd, 0xed, 0x35, 0x00, 0x00 }, null ) },
            { "LD (IY),IY", ( new byte[] { 0xfd, 0xed, 0x35, 0x00, 0x00 }, null ) },
            { "LD (IY),BC", ( new byte[] { 0xfd, 0xed, 0x0e, 0x00 }, null ) },
            { "LD (IY),DE", ( new byte[] { 0xfd, 0xed, 0x1e, 0x00 }, null ) },
            { "LD (IY),SP", ( new byte[] { 0xfd, 0xed, 0x3e, 0x00 }, null ) },
            { "LD (PC),A", ( new byte[] { 0xed, 0x23, 0x00, 0x00 }, null ) },
            { "LD (PC),HL", ( new byte[] { 0xed, 0x25, 0x00, 0x00 }, null ) },
            { "LD (PC),IX", ( new byte[] { 0xdd, 0xed, 0x25, 0x00, 0x00 }, null ) },
            { "LD (PC),IY", ( new byte[] { 0xfd, 0xed, 0x25, 0x00, 0x00 }, null ) },
            { "LD (SP),A", ( new byte[] { 0xed, 0x03, 0x00, 0x00 }, null ) },
            { "LD (SP),HL", ( new byte[] { 0xed, 0x05, 0x00, 0x00 }, null ) },
            { "LD (SP),IX", ( new byte[] { 0xdd, 0xed, 0x05, 0x00, 0x00 }, null ) },
            { "LD (SP),IY", ( new byte[] { 0xfd, 0xed, 0x05, 0x00, 0x00 }, null ) },
            { "LD A,(HL)", ( new byte[] { 0xfd, 0x7b, 0x00, 0x00 } , null ) },
            { "LD A,(IX)", ( new byte[] { 0xdd, 0x7e, 0x00 }, new byte[] { 0xfd, 0x79, 0x00, 0x00 } ) },
            { "LD A,(IY)", ( new byte[] { 0xfd, 0x7e, 0x00 }, new byte[] { 0xfd, 0x7a, 0x00, 0x00 } ) },
            { "LD A,(PC)", ( new byte[] { 0xfd, 0x78, 0x00, 0x00 }, null ) },
            { "LD A,(SP)", ( new byte[] { 0xdd, 0x78, 0x00, 0x00 }, null ) },
            { "LD BC,(IX)", ( new byte[] { 0xdd, 0xed, 0x06, 0x00 }, null ) },
            { "LD BC,(IY)", ( new byte[] { 0xfd, 0xed, 0x06, 0x00 }, null ) },
            { "LD DE,(IX)", ( new byte[] { 0xdd, 0xed, 0x16, 0x00 }, null ) },
            { "LD DE,(IY)", ( new byte[] { 0xfd, 0xed, 0x16, 0x00 }, null ) },
            { "LD HL,(IX)", ( new byte[] { 0xdd, 0xed, 0x26, 0x00 }, new byte[] { 0xed, 0x2c, 0x00, 0x00 } ) },
            { "LD HL,(IY)", ( new byte[] { 0xfd, 0xed, 0x26, 0x00 }, new byte[] { 0xed, 0x34, 0x00, 0x00 } ) },
            { "LD HL,(PC)", ( new byte[] { 0xed, 0x24, 0x00, 0x00 }, null ) },
            { "LD HL,(SP)", ( new byte[] { 0xed, 0x04, 0x00, 0x00 }, null ) },
            { "LD IX,(HL)", ( new byte[] { 0xdd, 0xed, 0x3c, 0x00, 0x00 }, null ) },
            { "LD IX,(IX)", ( new byte[] { 0xdd, 0xed, 0x2c, 0x00, 0x00 }, null ) },
            { "LD IX,(IY)", ( new byte[] { 0xdd, 0xed, 0x34, 0x00, 0x00 }, null ) },
            { "LD IX,(PC)", ( new byte[] { 0xdd, 0xed, 0x24, 0x00, 0x00 }, null ) },
            { "LD IX,(SP)", ( new byte[] { 0xdd, 0xed, 0x04, 0x00, 0x00 }, null ) },
            { "LD IY,(HL)", ( new byte[] { 0xfd, 0xed, 0x3c, 0x00, 0x00 }, null ) },
            { "LD IY,(IX)", ( new byte[] { 0xfd, 0xed, 0x2c, 0x00, 0x00 }, null ) },
            { "LD IY,(IY)", ( new byte[] { 0xfd, 0xed, 0x34, 0x00, 0x00 }, null ) },
            { "LD IY,(PC)", ( new byte[] { 0xfd, 0xed, 0x24, 0x00, 0x00 }, null ) },
            { "LD IY,(SP)", ( new byte[] { 0xfd, 0xed, 0x04, 0x00, 0x00 }, null ) },
            { "LD SP,(IX)", ( new byte[] { 0xdd, 0xed, 0x36, 0x00 }, null ) },
            { "LD SP,(IY)", ( new byte[] { 0xfd, 0xed, 0x36, 0x00 }, null ) },

            { "LDA HL,(HL)", ( new byte[] { 0xed, 0x3a, 0x00, 0x00 }, null ) },
            { "LDA HL,(IX)", ( new byte[] { 0xed, 0x2a, 0x00, 0x00 }, null ) },
            { "LDA HL,(IY)", ( new byte[] { 0xed, 0x32, 0x00, 0x00 }, null ) },
            { "LDA HL,(PC)", ( new byte[] { 0xed, 0x22, 0x00, 0x00 }, null ) },
            { "LDA HL,(SP)", ( new byte[] { 0xed, 0x02, 0x00, 0x00 }, null ) },
            { "LDA IX,(HL)", ( new byte[] { 0xdd, 0xed, 0x3a, 0x00, 0x00 }, null ) },
            { "LDA IX,(IX)", ( new byte[] { 0xdd, 0xed, 0x2a, 0x00, 0x00 }, null ) },
            { "LDA IX,(IY)", ( new byte[] { 0xdd, 0xed, 0x32, 0x00, 0x00 }, null ) },
            { "LDA IX,(PC)", ( new byte[] { 0xdd, 0xed, 0x22, 0x00, 0x00 }, null ) },
            { "LDA IX,(SP)", ( new byte[] { 0xdd, 0xed, 0x02, 0x00, 0x00 }, null ) },
            { "LDA IY,(HL)", ( new byte[] { 0xfd, 0xed, 0x3a, 0x00, 0x00 }, null ) },
            { "LDA IY,(IX)", ( new byte[] { 0xfd, 0xed, 0x2a, 0x00, 0x00 }, null ) },
            { "LDA IY,(IY)", ( new byte[] { 0xfd, 0xed, 0x32, 0x00, 0x00 }, null ) },
            { "LDA IY,(PC)", ( new byte[] { 0xfd, 0xed, 0x22, 0x00, 0x00 }, null ) },
            { "LDA IY,(SP)", ( new byte[] { 0xfd, 0xed, 0x02, 0x00, 0x00 }, null ) },

            { "LDUD (IX),A", ( new byte[] { 0xdd, 0xed, 0x8e, 0x00 }, null ) },
            { "LDUD (IY),A", ( new byte[] { 0xfd, 0xed, 0x8e, 0x00 }, null ) },
            { "LDUD A,(IX)", ( new byte[] { 0xdd, 0xed, 0x86, 0x00 }, null ) },
            { "LDUD A,(IY)", ( new byte[] { 0xfd, 0xed, 0x86, 0x00 }, null ) },
            { "LDUP (IX),A", ( new byte[] { 0xdd, 0xed, 0x9e, 0x00 }, null ) },
            { "LDUP (IY),A", ( new byte[] { 0xfd, 0xed, 0x9e, 0x00 }, null ) },
            { "LDUP A,(IX)", ( new byte[] { 0xdd, 0xed, 0x96, 0x00 }, null ) },
            { "LDUP A,(IY)", ( new byte[] { 0xfd, 0xed, 0x96, 0x00 }, null ) },

            { "LDW (HL),HL", ( new byte[] { 0xed, 0x2e }, new byte[] { 0xed, 0x3d, 0x00, 0x00 } ) },
            { "LDW (HL),IX", ( new byte[] { 0xdd, 0xed, 0x3d, 0x00, 0x00 }, null ) },
            { "LDW (HL),IY", ( new byte[] { 0xfd, 0xed, 0x3d, 0x00, 0x00 }, null ) },
            { "LDW (IX),HL", ( new byte[] { 0xdd, 0xed, 0x2e, 0x00 }, new byte[] { 0xed, 0x2d, 0x00, 0x00 } ) },
            { "LDW (IX),IX", ( new byte[] { 0xdd, 0xed, 0x2d, 0x00, 0x00 }, null ) },
            { "LDW (IX),IY", ( new byte[] { 0xfd, 0xed, 0x2d, 0x00, 0x00 }, null ) },
            { "LDW (IX),BC", ( new byte[] { 0xdd, 0xed, 0x0e, 0x00 }, null ) },
            { "LDW (IX),DE", ( new byte[] { 0xdd, 0xed, 0x1e, 0x00 }, null ) },
            { "LDW (IX),SP", ( new byte[] { 0xdd, 0xed, 0x3e, 0x00 }, null ) },
            { "LDW (IY),HL", ( new byte[] { 0xfd, 0xed, 0x2e, 0x00 }, new byte[] { 0xed, 0x35, 0x00, 0x00 } ) },
            { "LDW (IY),IX", ( new byte[] { 0xdd, 0xed, 0x35, 0x00, 0x00 }, null ) },
            { "LDW (IY),IY", ( new byte[] { 0xfd, 0xed, 0x35, 0x00, 0x00 }, null ) },
            { "LDW (IY),BC", ( new byte[] { 0xfd, 0xed, 0x0e, 0x00 }, null ) },
            { "LDW (IY),DE", ( new byte[] { 0xfd, 0xed, 0x1e, 0x00 }, null ) },
            { "LDW (IY),SP", ( new byte[] { 0xfd, 0xed, 0x3e, 0x00 }, null ) },
            { "LDW (PC),HL", ( new byte[] { 0xed, 0x25, 0x00, 0x00 }, null ) },
            { "LDW (PC),IX", ( new byte[] { 0xdd, 0xed, 0x25, 0x00, 0x00 }, null ) },
            { "LDW (PC),IY", ( new byte[] { 0xfd, 0xed, 0x25, 0x00, 0x00 }, null ) },
            { "LDW (SP),HL", ( new byte[] { 0xed, 0x05, 0x00, 0x00 }, null ) },
            { "LDW (SP),IX", ( new byte[] { 0xdd, 0xed, 0x05, 0x00, 0x00 }, null ) },
            { "LDW (SP),IY", ( new byte[] { 0xfd, 0xed, 0x05, 0x00, 0x00 }, null ) },
            { "LDW BC,(IX)", ( new byte[] { 0xdd, 0xed, 0x06, 0x00 }, null ) },
            { "LDW BC,(IY)", ( new byte[] { 0xfd, 0xed, 0x06, 0x00 }, null ) },
            { "LDW DE,(IX)", ( new byte[] { 0xdd, 0xed, 0x16, 0x00 }, null ) },
            { "LDW DE,(IY)", ( new byte[] { 0xfd, 0xed, 0x16, 0x00 }, null ) },
            { "LDW HL,(HL)", ( new byte[] { 0xed, 0x26 }, new byte[] { 0xed, 0x3c, 0x00, 0x00 } ) },
            { "LDW HL,(IX)", ( new byte[] { 0xdd, 0xed, 0x26, 0x00 }, new byte[] { 0xed, 0x2c, 0x00, 0x00 } ) },
            { "LDW HL,(IY)", ( new byte[] { 0xfd, 0xed, 0x26, 0x00 }, new byte[] { 0xed, 0x34, 0x00, 0x00 } ) },
            { "LDW HL,(PC)", ( new byte[] { 0xed, 0x24, 0x00, 0x00 }, null ) },
            { "LDW HL,(SP)", ( new byte[] { 0xed, 0x04, 0x00, 0x00 }, null ) },
            { "LDW IX,(HL)", ( new byte[] { 0xdd, 0xed, 0x3c, 0x00, 0x00 }, null ) },
            { "LDW IX,(IX)", ( new byte[] { 0xdd, 0xed, 0x2c, 0x00, 0x00 }, null ) },
            { "LDW IX,(IY)", ( new byte[] { 0xdd, 0xed, 0x34, 0x00, 0x00 }, null ) },
            { "LDW IX,(PC)", ( new byte[] { 0xdd, 0xed, 0x24, 0x00, 0x00 }, null ) },
            { "LDW IX,(SP)", ( new byte[] { 0xdd, 0xed, 0x04, 0x00, 0x00 }, null ) },
            { "LDW IY,(HL)", ( new byte[] { 0xfd, 0xed, 0x3c, 0x00, 0x00 }, null ) },
            { "LDW IY,(IX)", ( new byte[] { 0xfd, 0xed, 0x2c, 0x00, 0x00 }, null ) },
            { "LDW IY,(IY)", ( new byte[] { 0xfd, 0xed, 0x34, 0x00, 0x00 }, null ) },
            { "LDW IY,(PC)", ( new byte[] { 0xfd, 0xed, 0x24, 0x00, 0x00 }, null ) },
            { "LDW IY,(SP)", ( new byte[] { 0xfd, 0xed, 0x04, 0x00, 0x00 }, null ) },
            { "LDW SP,(IX)", ( new byte[] { 0xdd, 0xed, 0x36, 0x00 }, null ) },
            { "LDW SP,(IY)", ( new byte[] { 0xfd, 0xed, 0x36, 0x00 }, null ) },

            { "MEPU (HL)", ( new byte[] { 0xed, 0xae }, new byte[] { 0xed, 0xbd, 0x00, 0x00 } ) },
            { "MEPU (IX)", ( new byte[] { 0xed, 0xad, 0x00, 0x00 }, null ) },
            { "MEPU (IY)", ( new byte[] { 0xed, 0xb5, 0x00, 0x00 }, null ) },
            { "MEPU (PC)", ( new byte[] { 0xed, 0xa5, 0x00, 0x00 }, null ) },
            { "MEPU (SP)", ( new byte[] { 0xed, 0x85, 0x00, 0x00 }, null ) },

            { "MULT A,(HL)", ( new byte[] { 0xed, 0xf0 }, new byte[] { 0xfd, 0xed, 0xd8, 0x00, 0x00 } ) },
            { "MULT A,(IX)", ( new byte[] { 0xdd, 0xed, 0xf0, 0x00 }, new byte[] { 0xfd, 0xed, 0xc8, 0x00, 0x00 } ) },
            { "MULT A,(IY)", ( new byte[] { 0xfd, 0xed, 0xf0, 0x00 }, new byte[] { 0xfd, 0xed, 0xd0, 0x00, 0x00 } ) },
            { "MULT A,(PC)", ( new byte[] { 0xfd, 0xed, 0xc0, 0x00, 0x00 }, null ) },
            { "MULT A,(SP)", ( new byte[] { 0xdd, 0xed, 0xc0, 0x00, 0x00 }, null ) },

            { "MULTU A,(HL)", ( new byte[] { 0xed, 0xf1 }, new byte[] { 0xfd, 0xed, 0xd9, 0x00, 0x00 } ) },
            { "MULTU A,(IX)", ( new byte[] { 0xdd, 0xed, 0xf1, 0x00 }, new byte[] { 0xfd, 0xed, 0xc9, 0x00, 0x00 } ) },
            { "MULTU A,(IY)", ( new byte[] { 0xfd, 0xed, 0xf1, 0x00 }, new byte[] { 0xfd, 0xed, 0xd1, 0x00, 0x00 } ) },
            { "MULTU A,(PC)", ( new byte[] { 0xfd, 0xed, 0xc1, 0x00, 0x00 }, null ) },
            { "MULTU A,(SP)", ( new byte[] { 0xdd, 0xed, 0xc1, 0x00, 0x00 }, null ) },

            { "MULTUW HL,(IX)", ( new byte[] { 0xfd, 0xed, 0xc3, 0x00, 0x00 }, null ) },
            { "MULTUW HL,(IY)", ( new byte[] { 0xfd, 0xed, 0xd3, 0x00, 0x00 }, null ) },
            { "MULTUW HL,(PC)", ( new byte[] { 0xdd, 0xed, 0xf3, 0x00, 0x00 }, null ) },

            { "MULTW HL,(IX)", ( new byte[] { 0xfd, 0xed, 0xc2, 0x00, 0x00 }, null ) },
            { "MULTW HL,(IY)", ( new byte[] { 0xfd, 0xed, 0xd2, 0x00, 0x00 }, null ) },
            { "MULTW HL,(PC)", ( new byte[] { 0xdd, 0xed, 0xf2, 0x00, 0x00 }, null ) },

            { "OR A,(HL)", ( new byte[] { 0xfd, 0xb3, 0x00, 0x00 } , null ) },
            { "OR A,(IX)", ( new byte[] { 0xdd, 0xb6, 0x00 }, new byte[] { 0xfd, 0xb1, 0x00, 0x00 } ) },
            { "OR A,(IY)", ( new byte[] { 0xfd, 0xb6, 0x00 }, new byte[] { 0xfd, 0xb2, 0x00, 0x00 } ) },
            { "OR A,(PC)", ( new byte[] { 0xfd, 0xb0, 0x00, 0x00 }, null ) },
            { "OR A,(SP)", ( new byte[] { 0xdd, 0xb0, 0x00, 0x00 }, null ) },

            { "OUT (C),(HL)", ( new byte[] { 0xfd, 0xed, 0x59, 0x00, 0x00 }, null ) },
            { "OUT (C),(IX)", ( new byte[] { 0xfd, 0xed, 0x49, 0x00, 0x00 }, null ) },
            { "OUT (C),(IY)", ( new byte[] { 0xfd, 0xed, 0x51, 0x00, 0x00 }, null ) },
            { "OUT (C),(PC)", ( new byte[] { 0xfd, 0xed, 0x41, 0x00, 0x00 }, null ) },
            { "OUT (C),(SP)", ( new byte[] { 0xdd, 0xed, 0x41, 0x00, 0x00 }, null ) },

            { "POP (PC)", ( new byte[] { 0xdd, 0xf1, 0x00, 0x00 }, null ) },

            { "PUSH (PC)", ( new byte[] { 0xdd, 0xf5, 0x00, 0x00 }, null ) },

            { "SBC A,(HL)", ( new byte[] { 0x9e }, new byte[] { 0xfd, 0x9b, 0x00, 0x00 } ) },
            { "SBC A,(IX)", ( new byte[] { 0xdd, 0x9e, 0x00 }, new byte[] { 0xfd, 0x99, 0x00, 0x00 } ) },
            { "SBC A,(IY)", ( new byte[] { 0xfd, 0x9e, 0x00 }, new byte[] { 0xfd, 0x9a, 0x00, 0x00 } ) },
            { "SBC A,(PC)", ( new byte[] { 0xfd, 0x98, 0x00, 0x00 }, null ) },
            { "SBC A,(SP)", ( new byte[] { 0xdd, 0x98, 0x00, 0x00 }, null ) },

            { "SUB A,(HL)", ( new byte[] { 0xfd, 0x93, 0x00, 0x00 } , null ) },
            { "SUB A,(IX)", ( new byte[] { 0xdd, 0x96, 0x00 }, new byte[] { 0xfd, 0x91, 0x00, 0x00 } ) },
            { "SUB A,(IY)", ( new byte[] { 0xfd, 0x96, 0x00 }, new byte[] { 0xfd, 0x92, 0x00, 0x00 } ) },
            { "SUB A,(PC)", ( new byte[] { 0xfd, 0x90, 0x00, 0x00 }, null ) },
            { "SUB A,(SP)", ( new byte[] { 0xdd, 0x90, 0x00, 0x00 }, null ) },

            { "SUBW HL,(IX)", ( new byte[] { 0xfd, 0xed, 0xce, 0x00, 0x00 }, null ) },
            { "SUBW HL,(IY)", ( new byte[] { 0xfd, 0xed, 0xde, 0x00, 0x00 }, null ) },
            { "SUBW HL,(PC)", ( new byte[] { 0xdd, 0xed, 0xfe, 0x00, 0x00 }, null ) },

            { "TSET (IX)", ( new byte[] { 0xdd, 0xcb, 0x00, 0x36 }, null ) },
            { "TSET (IY)", ( new byte[] { 0xfd, 0xcb, 0x00, 0x36 }, null ) },

            { "XOR A,(HL)", ( new byte[] { 0xfd, 0xab, 0x00, 0x00 } , null ) },
            { "XOR A,(IX)", ( new byte[] { 0xdd, 0xae, 0x00 }, new byte[] { 0xfd, 0xa9, 0x00, 0x00 } ) },
            { "XOR A,(IY)", ( new byte[] { 0xfd, 0xae, 0x00 }, new byte[] { 0xfd, 0xaa, 0x00, 0x00 } ) },
            { "XOR A,(PC)", ( new byte[] { 0xfd, 0xa8, 0x00, 0x00 }, null ) },
            { "XOR A,(SP)", ( new byte[] { 0xdd, 0xa8, 0x00, 0x00 }, null ) }
        };

        /// <summary>
        /// Instructions that have one variable argument and maybe also one fixed argument.
        /// 
        /// Items in the tuples are:
        /// - 1: Instruction, followed by the fixed argument if present.
        /// - 2: Type of the variable argument.
        /// - 3: Position of the variable argument in the instruction (single, first or second).
        /// - 4: Instruction bytes for the short index version.
        /// - 5: Byte position of the variable argument in the output for the short index version.
        /// - 6: Instruction bytes for the long index version.
        /// - 7: Byte position of the variable argument in the output for the long index version.
		/// 
		/// If the instruction doesn't have two versions (short and long index), 6 is null and 7 is not used.
        /// </summary>
        static readonly (string, CpuInstrArgType, CpuArgPos, byte[], int, byte[], int)[]
            ExclusiveZ280InstructionsWithOneVariableArgument = new (string, CpuInstrArgType, CpuArgPos, byte[], int, byte[], int)[] {
            ("ADC A", CpuInstrArgType.WordInParenthesis, CpuArgPos.Second, new byte[] { 0xdd, 0x8f, 0x00, 0x00 }, 2, null, 0 ), //ADC A,(nn)
            ("ADC A", CpuInstrArgType.HlOffset, CpuArgPos.Second, new byte[] { 0xfd, 0x8b, 0x00, 0x00 }, 2, null, 0 ), //ADC A,(HL+nn)
            ("ADC A", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0x8e, 0x00 }, 2, new byte[] { 0xfd, 0x89, 0x00, 0x00 }, 2 ), //ADC A,(IX+nn)
            ("ADC A", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0x8e, 0x00 }, 2, new byte[] { 0xfd, 0x8a, 0x00, 0x00 }, 2 ), //ADC A,(IY+nn)
            ("ADC A", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xfd, 0x88, 0x00, 0x00 }, 2, null, 0 ), //ADC A,(PC+nn)
            ("ADC A", CpuInstrArgType.SpOffset, CpuArgPos.Second, new byte[] { 0xdd, 0x88, 0x00, 0x00 }, 2, null, 0 ), //ADC A,(SP+nn)

            //Aliases for "ADC A,..." with implicit A
            ("ADC", CpuInstrArgType.WordInParenthesis, CpuArgPos.Single, new byte[] { 0xdd, 0x8f, 0x00, 0x00 }, 2, null, 0 ), //ADC (nn)
            ("ADC", CpuInstrArgType.HlOffset, CpuArgPos.Single, new byte[] { 0xfd, 0x8b, 0x00, 0x00 }, 2, null, 0 ), //ADC (HL+nn)
            ("ADC", CpuInstrArgType.IxOffset, CpuArgPos.Single, new byte[] { 0xdd, 0x8e, 0x00 }, 2, new byte[] { 0xfd, 0x89, 0x00, 0x00 }, 2 ), //ADC (IX+nn)
            ("ADC", CpuInstrArgType.IyOffset, CpuArgPos.Single, new byte[] { 0xfd, 0x8e, 0x00 }, 2, new byte[] { 0xfd, 0x8a, 0x00, 0x00 }, 2 ), //ADC (IY+nn)
            ("ADC", CpuInstrArgType.PcOffset, CpuArgPos.Single, new byte[] { 0xfd, 0x88, 0x00, 0x00 }, 2, null, 0 ), //ADC (PC+nn)
            ("ADC", CpuInstrArgType.SpOffset, CpuArgPos.Single, new byte[] { 0xdd, 0x88, 0x00, 0x00 }, 2, null, 0 ), //ADC (SP+nn)

            ("ADD A", CpuInstrArgType.WordInParenthesis, CpuArgPos.Second, new byte[] { 0xdd, 0x87, 0x04, 0x33 }, 2, null, 0 ), //ADD A,(nn)
            ("ADD A", CpuInstrArgType.HlOffset, CpuArgPos.Second, new byte[] { 0xfd, 0x83, 0x00, 0x00 }, 2, null, 0 ), //ADD A,(HL+nn)
            ("ADD A", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0x86, 0x00 }, 2, new byte[] { 0xfd, 0x81, 0x00, 0x00 }, 2 ), //ADD A,(IX+nn)
            ("ADD A", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0x86, 0x00 }, 2, new byte[] { 0xfd, 0x82, 0x00, 0x00 }, 2 ), //ADD A,(IY+nn)
            ("ADD A", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xfd, 0x80, 0x00, 0x00 }, 2, null, 0 ), //ADD A,(PC+nn)
            ("ADD A", CpuInstrArgType.SpOffset, CpuArgPos.Second, new byte[] { 0xdd, 0x80, 0x00, 0x00 }, 2, null, 0 ), //ADD A,(SP+nn)

            //Aliases for "ADD A,..." with implicit A
            ("ADD", CpuInstrArgType.WordInParenthesis, CpuArgPos.Single, new byte[] { 0xdd, 0x87, 0x04, 0x33 }, 2, null, 0 ), //ADD (nn)
            ("ADD", CpuInstrArgType.HlOffset, CpuArgPos.Single, new byte[] { 0xfd, 0x83, 0x00, 0x00 }, 2, null, 0 ), //ADD (HL+nn)
            ("ADD", CpuInstrArgType.IxOffset, CpuArgPos.Single, new byte[] { 0xdd, 0x86, 0x00 }, 2, new byte[] { 0xfd, 0x81, 0x00, 0x00 }, 2 ), //ADD (IX+nn)
            ("ADD", CpuInstrArgType.IyOffset, CpuArgPos.Single, new byte[] { 0xfd, 0x86, 0x00 }, 2, new byte[] { 0xfd, 0x82, 0x00, 0x00 }, 2 ), //ADD (IY+nn)
            ("ADD", CpuInstrArgType.PcOffset, CpuArgPos.Single, new byte[] { 0xfd, 0x80, 0x00, 0x00 }, 2, null, 0 ), //ADD (PC+nn)
            ("ADD", CpuInstrArgType.SpOffset, CpuArgPos.Single, new byte[] { 0xdd, 0x80, 0x00, 0x00 }, 2, null, 0 ), //ADD (SP+nn)

            ("ADDW HL", CpuInstrArgType.WordInParenthesis, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0xd6, 0x00, 0x00 }, 3, null, 0 ), //ADDW HL,(nn)
            ("ADDW HL", CpuInstrArgType.IxOffsetLong, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xc6, 0x00, 0x00 }, 3, null, 0 ), //ADDW HL,(IX+nn)
            ("ADDW HL", CpuInstrArgType.IyOffsetLong, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xd6, 0x00, 0x00 }, 3, null, 0 ), //ADDW HL,(IY+nn)
            ("ADDW HL", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0xf6, 0x00, 0x00 }, 3, null, 0 ), //ADDW HL,(PC+nn)
            ("ADDW HL", CpuInstrArgType.Word, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xf6, 0x00, 0x00 }, 3, null, 0 ), //ADDW HL,nn

            ("AND A", CpuInstrArgType.WordInParenthesis, CpuArgPos.Second, new byte[] { 0xdd, 0xa7, 0x04, 0x33 }, 2, null, 0 ), //AND A,(nn)
            ("AND A", CpuInstrArgType.HlOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xa3, 0x00, 0x00 }, 2, null, 0 ), //AND A,(HL+nn)
            ("AND A", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xa6, 0x00 }, 2, new byte[] { 0xfd, 0xa1, 0x00, 0x00 }, 2 ), //AND A,(IX+nn)
            ("AND A", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xa6, 0x00 }, 2, new byte[] { 0xfd, 0xa2, 0x00, 0x00 }, 2 ), //AND A,(IY+nn)
            ("AND A", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xa0, 0x00, 0x00 }, 2, null, 0 ), //AND A,(PC+nn)
            ("AND A", CpuInstrArgType.SpOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xa0, 0x00, 0x00 }, 2, null, 0 ), //AND A,(SP+nn)

            ("CALL", CpuInstrArgType.PcOffset, CpuArgPos.Single, new byte[] { 0xfd, 0xcd, 0x00, 0x00 }, 2, null, 0 ), //CALL (PC+nn)
            ("CALL C", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xdc, 0x00, 0x00 }, 2, null, 0 ), //CALL C,(PC+nn)
            ("CALL M", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xfc, 0x00, 0x00 }, 2, null, 0 ), //CALL M,(PC+nn)
            ("CALL NC", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xd4, 0x00, 0x00 }, 2, null, 0 ), //CALL NC,(PC+nn)
            ("CALL NZ", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xc4, 0x00, 0x00 }, 2, null, 0 ), //CALL NZ,(PC+nn)
            ("CALL P", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xf4, 0x00, 0x00 }, 2, null, 0 ), //CALL P,(PC+nn)
            ("CALL PE", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xec, 0x00, 0x00 }, 2, null, 0 ), //CALL PE,(PC+nn)
            ("CALL PO", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xe4, 0x00, 0x00 }, 2, null, 0 ), //CALL PO,(PC+nn)
            ("CALL Z", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xcc, 0x00, 0x00 }, 2, null, 0 ), //CALL Z,(PC+nn)

            ("CP A", CpuInstrArgType.WordInParenthesis, CpuArgPos.Second, new byte[] { 0xdd, 0xbf, 0x00, 0x00 }, 2, null, 0 ), //CP A,(nn)
            ("CP A", CpuInstrArgType.HlOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xbb, 0x00, 0x00 }, 2, null, 0 ), //CP A,(HL+nn)
            ("CP A", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xbe, 0x00 }, 2, new byte[] { 0xfd, 0xb9, 0x00, 0x00 }, 2 ), //CP A,(IX+nn)
            ("CP A", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xbe, 0x00 }, 2, new byte[] { 0xfd, 0xba, 0x00, 0x00 }, 2 ), //CP A,(IY+nn)
            ("CP A", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xb8, 0x00, 0x00 }, 2, null, 0 ), //CP A,(PC+nn)
            ("CP A", CpuInstrArgType.SpOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xb8, 0x00, 0x00 }, 2, null, 0 ), //CP A,(SP+nn)

            //Aliases for "CP A,..." with implicit A
            ("CP", CpuInstrArgType.WordInParenthesis, CpuArgPos.Single, new byte[] { 0xdd, 0xbf, 0x00, 0x00 }, 2, null, 0 ), //CP (nn)
            ("CP", CpuInstrArgType.HlOffset, CpuArgPos.Single, new byte[] { 0xfd, 0xbb, 0x00, 0x00 }, 2, null, 0 ), //CP (HL+nn)
            ("CP", CpuInstrArgType.IxOffset, CpuArgPos.Single, new byte[] { 0xdd, 0xbe, 0x00 }, 2, new byte[] { 0xfd, 0xb9, 0x00, 0x00 }, 2 ), //CP (IX+nn)
            ("CP", CpuInstrArgType.IyOffset, CpuArgPos.Single, new byte[] { 0xfd, 0xbe, 0x00 }, 2, new byte[] { 0xfd, 0xba, 0x00, 0x00 }, 2 ), //CP (IY+nn)
            ("CP", CpuInstrArgType.PcOffset, CpuArgPos.Single, new byte[] { 0xfd, 0xb8, 0x00, 0x00 }, 2, null, 0 ), //CP (PC+nn)
            ("CP", CpuInstrArgType.SpOffset, CpuArgPos.Single, new byte[] { 0xdd, 0xb8, 0x00, 0x00 }, 2, null, 0 ), //CP (SP+nn)

            ("CPW HL", CpuInstrArgType.WordInParenthesis, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0xd7, 0x04, 0x33 }, 3, null, 0 ), //CPW HL,(nn)
            ("CPW HL", CpuInstrArgType.IxOffsetLong, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xc7, 0x00, 0x00 }, 3, null, 0 ), //CPW HL,(IX+nn)
            ("CPW HL", CpuInstrArgType.IyOffsetLong, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xd7, 0x00, 0x00 }, 3, null, 0 ), //CPW HL,(IY+nn)
            ("CPW HL", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0xf7, 0x00, 0x00 }, 3, null, 0 ), //CPW HL,(PC+nn)
            ("CPW HL", CpuInstrArgType.Word, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xf7, 0x04, 0x33 }, 3, null, 0 ), //CPW HL,nn

            ("DEC", CpuInstrArgType.WordInParenthesis, CpuArgPos.Single, new byte[] { 0xdd, 0x3d, 0x00, 0x00 }, 2, null, 0 ), //DEC (nn)
            ("DEC", CpuInstrArgType.HlOffset, CpuArgPos.Single, new byte[] { 0xfd, 0x1d, 0x00, 0x00 }, 2, null, 0 ), //DEC (HL+nn)
            ("DEC", CpuInstrArgType.IxOffset, CpuArgPos.Single, new byte[] { 0xdd, 0x35, 0x00 }, 2, new byte[] { 0xfd, 0x0d, 0x00, 0x00 }, 2 ), //DEC (IX+nn)
            ("DEC", CpuInstrArgType.IyOffset, CpuArgPos.Single, new byte[] { 0xfd, 0x35, 0x00 }, 2, new byte[] { 0xfd, 0x15, 0x00, 0x00 }, 2 ), //DEC (IY+nn)
            ("DEC", CpuInstrArgType.PcOffset, CpuArgPos.Single, new byte[] { 0xfd, 0x05, 0x00, 0x00 }, 2, null, 0 ), //DEC (PC+nn)
            ("DEC", CpuInstrArgType.SpOffset, CpuArgPos.Single, new byte[] { 0xdd, 0x05, 0x00, 0x00 }, 2, null, 0 ), //DEC (SP+nn)

            ("DECW", CpuInstrArgType.WordInParenthesis, CpuArgPos.Single, new byte[] { 0xdd, 0x1b, 0x00, 0x00 }, 2, null, 0 ), //DECW (nn)
            ("DECW", CpuInstrArgType.IxOffsetLong, CpuArgPos.Single, new byte[] { 0xfd, 0x0b, 0x00, 0x00 }, 2, null, 0 ), //DECW (IX+nn)
            ("DECW", CpuInstrArgType.IyOffsetLong, CpuArgPos.Single, new byte[] { 0xfd, 0x1b, 0x00, 0x00 }, 2, null, 0 ), //DECW (IY+nn)
            ("DECW", CpuInstrArgType.PcOffset, CpuArgPos.Single, new byte[] { 0xdd, 0x3b, 0x00, 0x00 }, 2, null, 0 ), //DECW (PC+nn)

            ("DI", CpuInstrArgType.Byte, CpuArgPos.Single, new byte[] { 0xed, 0x77, 0x00 }, 2, null, 0 ), //DI n

            ("DIV HL", CpuInstrArgType.WordInParenthesis, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0xfc, 0x00, 0x00 }, 3, null, 0 ), //DIV HL,(nn)
            ("DIV HL", CpuInstrArgType.HlOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xdc, 0x00, 0x00 }, 3, null, 0 ), //DIV HL,(HL+nn)
            ("DIV HL", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0xf4, 0x00 }, 3, new byte[] { 0xfd, 0xed, 0xcc, 0x00, 0x00 }, 3 ), //DIV HL,(IX+nn)
            ("DIV HL", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0xf4, 0x00 }, 3, null, 0 ), //DIV HL,(IX+n)
            ("DIV HL", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xf4, 0x00 }, 3, new byte[] { 0xfd, 0xed, 0xd4, 0x00, 0x00 }, 3 ), //DIV HL,(IY+nn)
            ("DIV HL", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xf4, 0x00 }, 3, null, 0 ), //DIV HL,(IY+n)
            ("DIV HL", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xc4, 0x00, 0x00 }, 3, null, 0 ), //DIV HL,(PC+nn)
            ("DIV HL", CpuInstrArgType.SpOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0xc4, 0x00, 0x00 }, 3, null, 0 ), //DIV HL,(SP+nn)
            ("DIV HL", CpuInstrArgType.Byte, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xfc, 0x00 }, 3, null, 0 ), //DIV HL,n

            ("DIVU HL", CpuInstrArgType.WordInParenthesis, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0xfd, 0x00, 0x00 }, 3, null, 0 ), //DIVU HL,(nn)
            ("DIVU HL", CpuInstrArgType.HlOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xdd, 0x00, 0x00 }, 3, null, 0 ), //DIVU HL,(HL+nn)
            ("DIVU HL", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0xf5, 0x00 }, 3, new byte[] { 0xfd, 0xed, 0xcd, 0x00, 0x00 }, 3 ), //DIVU HL,(IX+nn)
            ("DIVU HL", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xf5, 0x00 }, 3, new byte[] { 0xfd, 0xed, 0xd5, 0x00, 0x00 }, 3 ), //DIVU HL,(IY+nn)
            ("DIVU HL", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xc5, 0x00, 0x00 }, 3, null, 0 ), //DIVU HL,(PC+nn)
            ("DIVU HL", CpuInstrArgType.SpOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0xc5, 0x00, 0x00 }, 3, null, 0 ), //DIVU HL,(SP+nn)
            ("DIVU HL", CpuInstrArgType.Byte, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xfd, 0x00 }, 3, null, 0 ), //DIVU HL,n

            ("DIVUW DEHL", CpuInstrArgType.WordInParenthesis, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0xdb, 0x00, 0x00 }, 3, null, 0 ), //DIVUW DEHL,(nn)
            ("DIVUW DEHL", CpuInstrArgType.IxOffsetLong, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xcb, 0x00, 0x00 }, 3, null, 0 ), //DIVUW DEHL,(IX+nn)
            ("DIVUW DEHL", CpuInstrArgType.IyOffsetLong, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xdb, 0x00, 0x00 }, 3, null, 0 ), //DIVUW DEHL,(IY+nn)
            ("DIVUW DEHL", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0xfb, 0x00, 0x00 }, 3, null, 0 ), //DIVUW DEHL,(PC+nn)
            ("DIVUW DEHL", CpuInstrArgType.Word, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xfb, 0x00, 0x00 }, 3, null, 0 ), //DIVUW DEHL,nn

            ("DIVW DEHL", CpuInstrArgType.WordInParenthesis, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0xda, 0x00, 0x00 }, 3, null, 0 ), //DIVW DEHL,(nn)
            ("DIVW DEHL", CpuInstrArgType.IxOffsetLong, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xca, 0x00, 0x00 }, 3, null, 0 ), //DIVW DEHL,(IX+nn)
            ("DIVW DEHL", CpuInstrArgType.IyOffsetLong, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xda, 0x00, 0x00 }, 3, null, 0 ), //DIVW DEHL,(IY+nn)
            ("DIVW DEHL", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0xfa, 0x00, 0x00 }, 3, null, 0 ), //DIVW DEHL,(PC+nn)
            ("DIVW DEHL", CpuInstrArgType.Word, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xfa, 0x00, 0x00 }, 3, null, 0 ), //DIVW DEHL,nn

            ("EI", CpuInstrArgType.Byte, CpuArgPos.Single, new byte[] { 0xed, 0x7f, 0x00 }, 2, null, 0 ), //EI n

            ("EPUM", CpuInstrArgType.WordInParenthesis, CpuArgPos.Single, new byte[] { 0xed, 0xa7, 0x04, 0x33 }, 2, null, 0 ), //EPUM (nn)
            ("EPUM", CpuInstrArgType.HlOffset, CpuArgPos.Single, new byte[] { 0xed, 0xbc, 0x00, 0x00 }, 2, null, 0 ), //EPUM (HL+nn)
            ("EPUM", CpuInstrArgType.IxOffsetLong, CpuArgPos.Single, new byte[] { 0xed, 0xac, 0x00, 0x00 }, 2, null, 0 ), //EPUM (IX+nn)
            ("EPUM", CpuInstrArgType.IyOffsetLong, CpuArgPos.Single, new byte[] { 0xed, 0xb4, 0x00, 0x00 }, 2, null, 0 ), //EPUM (IY+nn)
            ("EPUM", CpuInstrArgType.PcOffset, CpuArgPos.Single, new byte[] { 0xed, 0xa4, 0x00, 0x00 }, 2, null, 0 ), //EPUM (PC+nn)
            ("EPUM", CpuInstrArgType.SpOffset, CpuArgPos.Single, new byte[] { 0xed, 0x84, 0x00, 0x00 }, 2, null, 0 ), //EPUM (SP+nn)

            ("EX A", CpuInstrArgType.WordInParenthesis, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0x3f, 0x00, 0x00 }, 3, null, 0 ), //EX A,(nn)
            ("EX A", CpuInstrArgType.HlOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0x1f, 0x00, 0x00 }, 3, null, 0 ), //EX A,(HL+nn)
            ("EX A", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0x37, 0x00 }, 3, new byte[] { 0xfd, 0xed, 0x0f, 0x00, 0x00 }, 3 ), //EX A,(IX+nn)
            ("EX A", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0x37, 0x00 }, 3, null, 0 ), //EX A,(IX+n)
            ("EX A", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0x37, 0x00 }, 3, new byte[] { 0xfd, 0xed, 0x17, 0x00, 0x00 }, 3 ), //EX A,(IY+nn)
            ("EX A", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0x37, 0x00 }, 3, null, 0 ), //EX A,(IY+n)
            ("EX A", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0x07, 0x00, 0x00 }, 3, null, 0 ), //EX A,(PC+nn)
            ("EX A", CpuInstrArgType.SpOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0x07, 0x00, 0x00 }, 3, null, 0 ), //EX A,(SP+nn)

            ("IN (C)", CpuInstrArgType.WordInParenthesis, CpuArgPos.First, new byte[] { 0xdd, 0xed, 0x78, 0x00, 0x00 }, 3, null, 0 ), //IN (nn),(C)
            ("IN (C)", CpuInstrArgType.HlOffset, CpuArgPos.First, new byte[] { 0xfd, 0xed, 0x58, 0x00, 0x00 }, 3, null, 0 ), //IN (HL+nn),(C)
            ("IN (C)", CpuInstrArgType.IxOffsetLong, CpuArgPos.First, new byte[] { 0xfd, 0xed, 0x48, 0x00, 0x00 }, 3, null, 0 ), //IN (IX+nn),(C)
            ("IN (C)", CpuInstrArgType.IyOffsetLong, CpuArgPos.First, new byte[] { 0xfd, 0xed, 0x50, 0x00, 0x00 }, 3, null, 0 ), //IN (IY+nn),(C)
            ("IN (C)", CpuInstrArgType.PcOffset, CpuArgPos.First, new byte[] { 0xfd, 0xed, 0x40, 0x00, 0x00 }, 3, null, 0 ), //IN (PC+nn),(C)
            ("IN (C)", CpuInstrArgType.SpOffset, CpuArgPos.First, new byte[] { 0xdd, 0xed, 0x40, 0x00, 0x00 }, 3, null, 0 ), //IN (SP+nn),(C)

            ("INC", CpuInstrArgType.WordInParenthesis, CpuArgPos.Single, new byte[] { 0xdd, 0x3c, 0x00, 0x00 }, 2, null, 0 ), //INC (nn)
            ("INC", CpuInstrArgType.HlOffset, CpuArgPos.Single, new byte[] { 0xfd, 0x1c, 0x00, 0x00 }, 2, null, 0 ), //INC (HL+nn)
            ("INC", CpuInstrArgType.IxOffset, CpuArgPos.Single, new byte[] { 0xdd, 0x34, 0x00 }, 2, new byte[] { 0xfd, 0x0c, 0x00, 0x00 }, 2 ), //INC (IX+nn)
            ("INC", CpuInstrArgType.IyOffset, CpuArgPos.Single, new byte[] { 0xfd, 0x34, 0x00 }, 2, new byte[] { 0xfd, 0x14, 0x00, 0x00 }, 2 ), //INC (IY+nn)
            ("INC", CpuInstrArgType.PcOffset, CpuArgPos.Single, new byte[] { 0xfd, 0x04, 0x00, 0x00 }, 2, null, 0 ), //INC (PC+nn)
            ("INC", CpuInstrArgType.SpOffset, CpuArgPos.Single, new byte[] { 0xdd, 0x04, 0x00, 0x00 }, 2, null, 0 ), //INC (SP+nn)

            ("INCW", CpuInstrArgType.WordInParenthesis, CpuArgPos.Single, new byte[] { 0xdd, 0x13, 0x00, 0x00 }, 2, null, 0 ), //INCW (nn)
            ("INCW", CpuInstrArgType.IxOffsetLong, CpuArgPos.Single, new byte[] { 0xfd, 0x03, 0x00, 0x00 }, 2, null, 0 ), //INCW (IX+nn)
            ("INCW", CpuInstrArgType.IyOffsetLong, CpuArgPos.Single, new byte[] { 0xfd, 0x13, 0x00, 0x00 }, 2, null, 0 ), //INCW (IY+nn)
            ("INCW", CpuInstrArgType.PcOffset, CpuArgPos.Single, new byte[] { 0xdd, 0x33, 0x00, 0x00 }, 2, null, 0 ), //INCW (PC+nn)

            ("JAF", CpuInstrArgType.OffsetFromCurrentLocationMinusOne, CpuArgPos.Single, new byte[] { 0xdd, 0x28, 0x00 }, 2, null, 0 ), //JAF n

            ("JAR", CpuInstrArgType.OffsetFromCurrentLocationMinusOne, CpuArgPos.Single, new byte[] { 0xdd, 0x20, 0x00 }, 2, null, 0 ), //JAR n

            ("JP", CpuInstrArgType.PcOffset, CpuArgPos.Single, new byte[] { 0xfd, 0xc3, 0x00, 0x00 }, 2, null, 0 ), //JP (PC+nn)
            ("JP C", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xda, 0x00, 0x00 }, 2, null, 0 ), //JP C,(PC+nn)
            ("JP M", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xfa, 0x00, 0x00 }, 2, null, 0 ), //JP M,(PC+nn)
            ("JP NC", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xd2, 0x00, 0x00 }, 2, null, 0 ), //JP NC,(PC+nn)
            ("JP NZ", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xc2, 0x00, 0x00 }, 2, null, 0 ), //JP NZ,(PC+nn)
            ("JP P", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xf2, 0x00, 0x00 }, 2, null, 0 ), //JP P,(PC+nn)
            ("JP PE", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xea, 0x00, 0x00 }, 2, null, 0 ), //JP PE,(PC+nn)
            ("JP PO", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xe2, 0x00, 0x00 }, 2, null, 0 ), //JP PO,(PC+nn)
            ("JP Z", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xca, 0x00, 0x00 }, 2, null, 0 ), //JP Z,(PC+nn)

            ("LD A", CpuInstrArgType.HlOffset, CpuArgPos.First, new byte[] { 0xed, 0x3b, 0x00, 0x00 }, 2, null, 0 ), //LD (HL+nn),A
            ("LD HL", CpuInstrArgType.HlOffset, CpuArgPos.First, new byte[] { 0xed, 0x3d, 0x00, 0x00 }, 2, null, 0 ), //LD (HL+nn),HL
            ("LD IX", CpuInstrArgType.HlOffset, CpuArgPos.First, new byte[] { 0xdd, 0xed, 0x3d, 0x00, 0x00 }, 3, null, 0 ), //LD (HL+nn),IX
            ("LD IY", CpuInstrArgType.HlOffset, CpuArgPos.First, new byte[] { 0xfd, 0xed, 0x3d, 0x00, 0x00 }, 3, null, 0 ), //LD (HL+nn),IY
            ("LD (HL+IX)", CpuInstrArgType.Byte, CpuArgPos.Second, new byte[] { 0xdd, 0x0e, 0x00 }, 2, null, 0 ), //LD (HL+IX),n
            ("LD (HL+IY)", CpuInstrArgType.Byte, CpuArgPos.Second, new byte[] { 0xdd, 0x16, 0x00 }, 2, null, 0 ), //LD (HL+IY),n
            ("LD A", CpuInstrArgType.IxOffset, CpuArgPos.First, new byte[] { 0xdd, 0x77, 0x00 }, 2, new byte[] { 0xed, 0x2b, 0x00, 0x00 }, 2 ), //LD (IX+nn),A
            ("LD HL", CpuInstrArgType.IxOffset, CpuArgPos.First, new byte[] { 0xdd, 0xed, 0x2e, 0x00 }, 3, new byte[] { 0xed, 0x2d, 0x00, 0x00 }, 2 ), //LD (IX+nn),HL
            ("LD IX", CpuInstrArgType.IxOffsetLong, CpuArgPos.First, new byte[] { 0xdd, 0xed, 0x2d, 0x00, 0x00 }, 3, null, 0 ), //LD (IX+nn),IX
            ("LD IY", CpuInstrArgType.IxOffsetLong, CpuArgPos.First, new byte[] { 0xfd, 0xed, 0x2d, 0x00, 0x00 }, 3, null, 0 ), //LD (IX+nn),IY
            ("LD BC", CpuInstrArgType.IxOffset, CpuArgPos.First, new byte[] { 0xdd, 0xed, 0x0e, 0x00 }, 3, null, 0 ), //LD (IX+n),BC
            ("LD DE", CpuInstrArgType.IxOffset, CpuArgPos.First, new byte[] { 0xdd, 0xed, 0x1e, 0x00 }, 3, null, 0 ), //LD (IX+n),DE
            ("LD HL", CpuInstrArgType.IxOffset, CpuArgPos.First, new byte[] { 0xdd, 0xed, 0x2e, 0x00 }, 3, null, 0 ), //LD (IX+n),HL
            ("LD SP", CpuInstrArgType.IxOffset, CpuArgPos.First, new byte[] { 0xdd, 0xed, 0x3e, 0x00 }, 3, null, 0 ), //LD (IX+n),SP
            ("LD (IX+IY)", CpuInstrArgType.Byte, CpuArgPos.Second, new byte[] { 0xdd, 0x1e, 0x00 }, 2, null, 0 ), //LD (IX+IY),n
            ("LD A", CpuInstrArgType.IyOffset, CpuArgPos.First, new byte[] { 0xfd, 0x77, 0x00 }, 2, new byte[] { 0xed, 0x33, 0x00, 0x00 }, 2 ), //LD (IY+nn),A
            ("LD HL", CpuInstrArgType.IyOffset, CpuArgPos.First, new byte[] { 0xfd, 0xed, 0x2e, 0x00 }, 3, new byte[] { 0xed, 0x35, 0x00, 0x00 }, 2 ), //LD (IY+nn),HL
            ("LD IX", CpuInstrArgType.IyOffsetLong, CpuArgPos.First, new byte[] { 0xdd, 0xed, 0x35, 0x00, 0x00 }, 3, null, 0 ), //LD (IY+nn),IX
            ("LD IY", CpuInstrArgType.IyOffsetLong, CpuArgPos.First, new byte[] { 0xfd, 0xed, 0x35, 0x00, 0x00 }, 3, null, 0 ), //LD (IY+nn),IY
            ("LD BC", CpuInstrArgType.IyOffset, CpuArgPos.First, new byte[] { 0xfd, 0xed, 0x0e, 0x00 }, 3, null, 0 ), //LD (IY+n),BC
            ("LD DE", CpuInstrArgType.IyOffset, CpuArgPos.First, new byte[] { 0xfd, 0xed, 0x1e, 0x00 }, 3, null, 0 ), //LD (IY+n),DE
            ("LD HL", CpuInstrArgType.IyOffset, CpuArgPos.First, new byte[] { 0xfd, 0xed, 0x2e, 0x00 }, 3, null, 0 ), //LD (IY+n),HL
            ("LD SP", CpuInstrArgType.IyOffset, CpuArgPos.First, new byte[] { 0xfd, 0xed, 0x3e, 0x00 }, 3, null, 0 ), //LD (IY+n),SP
            ("LD A", CpuInstrArgType.PcOffset, CpuArgPos.First, new byte[] { 0xed, 0x23, 0x00, 0x00 }, 2, null, 0 ), //LD (PC+nn),A
            ("LD HL", CpuInstrArgType.PcOffset, CpuArgPos.First, new byte[] { 0xed, 0x25, 0x00, 0x00 }, 2, null, 0 ), //LD (PC+nn),HL
            ("LD IX", CpuInstrArgType.PcOffset, CpuArgPos.First, new byte[] { 0xdd, 0xed, 0x25, 0x00, 0x00 }, 3, null, 0 ), //LD (PC+nn),IX
            ("LD IY", CpuInstrArgType.PcOffset, CpuArgPos.First, new byte[] { 0xfd, 0xed, 0x25, 0x00, 0x00 }, 3, null, 0 ), //LD (PC+nn),IY
            ("LD A", CpuInstrArgType.SpOffset, CpuArgPos.First, new byte[] { 0xed, 0x03, 0x00, 0x00 }, 2, null, 0 ), //LD (SP+nn),A
            ("LD HL", CpuInstrArgType.SpOffset, CpuArgPos.First, new byte[] { 0xed, 0x05, 0x00, 0x00 }, 2, null, 0 ), //LD (SP+nn),HL
            ("LD IX", CpuInstrArgType.SpOffset, CpuArgPos.First, new byte[] { 0xdd, 0xed, 0x05, 0x00, 0x00 }, 3, null, 0 ), //LD (SP+nn),IX
            ("LD IY", CpuInstrArgType.SpOffset, CpuArgPos.First, new byte[] { 0xfd, 0xed, 0x05, 0x00, 0x00 }, 3, null, 0 ), //LD (SP+nn),IY
            ("LD A", CpuInstrArgType.HlOffset, CpuArgPos.Second, new byte[] { 0xfd, 0x7b, 0x00, 0x00 }, 2, null, 0 ), //LD A,(HL+nn)
            ("LD A", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0x7e, 0x00 }, 2, new byte[] { 0xfd, 0x79, 0x00, 0x00 }, 2 ), //LD A,(IX+nn)
            ("LD A", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0x7e, 0x00 }, 2, new byte[] { 0xfd, 0x7a, 0x00, 0x00 }, 2 ), //LD A,(IY+nn)
            ("LD A", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xfd, 0x78, 0x00, 0x00 }, 2, null, 0 ), //LD A,(PC+nn)
            ("LD A", CpuInstrArgType.SpOffset, CpuArgPos.Second, new byte[] { 0xdd, 0x78, 0x00, 0x00 }, 2, null, 0 ), //LD A,(SP+nn)
            ("LD BC", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0x06, 0x00 }, 3, null, 0 ), //LD BC,(IX+n)
            ("LD BC", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0x06, 0x00 }, 3, null, 0 ), //LD BC,(IY+n)
            ("LD DE", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0x16, 0x00 }, 3, null, 0 ), //LD DE,(IX+n)
            ("LD DE", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0x16, 0x00 }, 3, null, 0 ), //LD DE,(IY+n)
            ("LD HL", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0x26, 0x00 }, 3, new byte[] { 0xed, 0x2c, 0x00, 0x00 }, 2 ), //LD HL,(IX+nn)
            ("LD HL", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0x26, 0x00 }, 3, null, 0 ), //LD HL,(IX+n)
            ("LD HL", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0x26, 0x00 }, 3, new byte[] { 0xed, 0x34, 0x00, 0x00 }, 2 ), //LD HL,(IY+nn)
            ("LD HL", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0x26, 0x00 }, 3, null, 0 ), //LD HL,(IY+n)
            ("LD HL", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xed, 0x24, 0x00, 0x00 }, 2, null, 0 ), //LD HL,(PC+nn)
            ("LD HL", CpuInstrArgType.SpOffset, CpuArgPos.Second, new byte[] { 0xed, 0x04, 0x00, 0x00 }, 2, null, 0 ), //LD HL,(SP+nn)
            ("LD IX", CpuInstrArgType.HlOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0x3c, 0x00, 0x00 }, 3, null, 0 ), //LD IX,(HL+nn)
            ("LD IX", CpuInstrArgType.IxOffsetLong, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0x2c, 0x00, 0x00 }, 3, null, 0 ), //LD IX,(IX+nn)
            ("LD IX", CpuInstrArgType.IyOffsetLong, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0x34, 0x00, 0x00 }, 3, null, 0 ), //LD IX,(IY+nn)
            ("LD IX", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0x24, 0x00, 0x00 }, 3, null, 0 ), //LD IX,(PC+nn)
            ("LD IX", CpuInstrArgType.SpOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0x04, 0x00, 0x00 }, 3, null, 0 ), //LD IX,(SP+nn)
            ("LD IY", CpuInstrArgType.HlOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0x3c, 0x00, 0x00 }, 3, null, 0 ), //LD IY,(HL+nn)
            ("LD IY", CpuInstrArgType.IxOffsetLong, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0x2c, 0x00, 0x00 }, 3, null, 0 ), //LD IY,(IX+nn)
            ("LD IY", CpuInstrArgType.IyOffsetLong, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0x34, 0x00, 0x00 }, 3, null, 0 ), //LD IY,(IY+nn)
            ("LD IY", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0x24, 0x00, 0x00 }, 3, null, 0 ), //LD IY,(PC+nn)
            ("LD IY", CpuInstrArgType.SpOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0x04, 0x00, 0x00 }, 3, null, 0 ), //LD IY,(SP+nn)
            ("LD SP", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0x36, 0x00 }, 3, null, 0 ), //LD SP,(IX+n)
            ("LD SP", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0x36, 0x00 }, 3, null, 0 ), //LD SP,(IY+n)

            ("LDA HL", CpuInstrArgType.WordInParenthesis, CpuArgPos.Second, new byte[] { 0x21, 0x00, 0x00 }, 1, null, 0 ), //LDA HL,(nn)
            ("LDA HL", CpuInstrArgType.HlOffset, CpuArgPos.Second, new byte[] { 0xed, 0x3a, 0x00, 0x00 }, 2, null, 0 ), //LDA HL,(HL+nn)
            ("LDA HL", CpuInstrArgType.IxOffsetLong, CpuArgPos.Second, new byte[] { 0xed, 0x2a, 0x00, 0x00 }, 2, null, 0 ), //LDA HL,(IX+nn)
            ("LDA HL", CpuInstrArgType.IyOffsetLong, CpuArgPos.Second, new byte[] { 0xed, 0x32, 0x00, 0x00 }, 2, null, 0 ), //LDA HL,(IY+nn)
            ("LDA HL", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xed, 0x22, 0x00, 0x00 }, 2, null, 0 ), //LDA HL,(PC+nn)
            ("LDA HL", CpuInstrArgType.SpOffset, CpuArgPos.Second, new byte[] { 0xed, 0x02, 0x00, 0x00 }, 2, null, 0 ), //LDA HL,(SP+nn)
            ("LDA IX", CpuInstrArgType.WordInParenthesis, CpuArgPos.Second, new byte[] { 0xdd, 0x21, 0x00, 0x00 }, 2, null, 0 ), //LDA IX,(nn)
            ("LDA IX", CpuInstrArgType.HlOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0x3a, 0x00, 0x00 }, 3, null, 0 ), //LDA IX,(HL+nn)
            ("LDA IX", CpuInstrArgType.IxOffsetLong, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0x2a, 0x00, 0x00 }, 3, null, 0 ), //LDA IX,(IX+nn)
            ("LDA IX", CpuInstrArgType.IyOffsetLong, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0x32, 0x00, 0x00 }, 3, null, 0 ), //LDA IX,(IY+nn)
            ("LDA IX", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0x22, 0x00, 0x00 }, 3, null, 0 ), //LDA IX,(PC+nn)
            ("LDA IX", CpuInstrArgType.SpOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0x02, 0x00, 0x00 }, 3, null, 0 ), //LDA IX,(SP+nn)
            ("LDA IY", CpuInstrArgType.WordInParenthesis, CpuArgPos.Second, new byte[] { 0xfd, 0x21, 0x00, 0x00 }, 2, null, 0 ), //LDA IY,(nn)
            ("LDA IY", CpuInstrArgType.HlOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0x3a, 0x00, 0x00 }, 3, null, 0 ), //LDA IY,(HL+nn)
            ("LDA IY", CpuInstrArgType.IxOffsetLong, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0x2a, 0x00, 0x00 }, 3, null, 0 ), //LDA IY,(IX+nn)
            ("LDA IY", CpuInstrArgType.IyOffsetLong, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0x32, 0x00, 0x00 }, 3, null, 0 ), //LDA IY,(IY+nn)
            ("LDA IY", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0x22, 0x00, 0x00 }, 3, null, 0 ), //LDA IY,(PC+nn)
            ("LDA IY", CpuInstrArgType.SpOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0x02, 0x00, 0x00 }, 3, null, 0 ), //LDA IY,(SP+nn)

            ("LDUD A", CpuInstrArgType.IxOffset, CpuArgPos.First, new byte[] { 0xdd, 0xed, 0x8e, 0x00 }, 3, null, 0 ), //LDUD (IX+n),A
            ("LDUD A", CpuInstrArgType.IyOffset, CpuArgPos.First, new byte[] { 0xfd, 0xed, 0x8e, 0x00 }, 3, null, 0 ), //LDUD (IY+n),A
            ("LDUD A", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0x86, 0x00 }, 3, null, 0 ), //LDUD A,(IX+n)
            ("LDUD A", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0x86, 0x00 }, 3, null, 0 ), //LDUD A,(IY+n)

            ("LDUP A", CpuInstrArgType.IxOffset, CpuArgPos.First, new byte[] { 0xdd, 0xed, 0x9e, 0x00 }, 3, null, 0 ), //LDUP (IX+n),A
            ("LDUP A", CpuInstrArgType.IyOffset, CpuArgPos.First, new byte[] { 0xfd, 0xed, 0x9e, 0x00 }, 3, null, 0 ), //LDUP (IY+n),A
            ("LDUP A", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0x96, 0x00 }, 3, null, 0 ), //LDUP A,(IX+n)
            ("LDUP A", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0x96, 0x00 }, 3, null, 0 ), //LDUP A,(IY+n)

            ("LDW BC", CpuInstrArgType.WordInParenthesis, CpuArgPos.First, new byte[] { 0xed, 0x43, 0x00, 0x00 }, 2, null, 0 ), //LDW (nn),BC
            ("LDW DE", CpuInstrArgType.WordInParenthesis, CpuArgPos.First, new byte[] { 0xed, 0x53, 0x00, 0x00 }, 2, null, 0 ), //LDW (nn),DE
            ("LDW HL", CpuInstrArgType.WordInParenthesis, CpuArgPos.First, new byte[] { 0x22, 0x00, 0x00 }, 1, null, 0 ), //LDW (nn),HL
            ("LDW IX", CpuInstrArgType.WordInParenthesis, CpuArgPos.First, new byte[] { 0xdd, 0x22, 0x00, 0x00 }, 2, null, 0 ), //LDW (nn),IX
            ("LDW IY", CpuInstrArgType.WordInParenthesis, CpuArgPos.First, new byte[] { 0xfd, 0x22, 0x00, 0x00 }, 2, null, 0 ), //LDW (nn),IY
            ("LDW SP", CpuInstrArgType.WordInParenthesis, CpuArgPos.First, new byte[] { 0xed, 0x73, 0x00, 0x00 }, 2, null, 0 ), //LDW (nn),SP
            ("LDW (HL)", CpuInstrArgType.Word, CpuArgPos.Second, new byte[] { 0xdd, 0x01, 0x00, 0x00 }, 2, null, 0 ), //LDW (HL),nn
            ("LDW HL", CpuInstrArgType.HlOffset, CpuArgPos.First, new byte[] { 0xed, 0x3d, 0x00, 0x00 }, 2, null, 0 ), //LDW (HL+nn),HL
            ("LDW IX", CpuInstrArgType.HlOffset, CpuArgPos.First, new byte[] { 0xdd, 0xed, 0x3d, 0x00, 0x00 }, 3, null, 0 ), //LDW (HL+nn),IX
            ("LDW IY", CpuInstrArgType.HlOffset, CpuArgPos.First, new byte[] { 0xfd, 0xed, 0x3d, 0x00, 0x00 }, 3, null, 0 ), //LDW (HL+nn),IY
            ("LDW HL", CpuInstrArgType.IxOffset, CpuArgPos.First, new byte[] { 0xdd, 0xed, 0x2e, 0x00 }, 3, new byte[] { 0xed, 0x2d, 0x00, 0x00 }, 2 ), //LDW (IX+nn),HL
            ("LDW IX", CpuInstrArgType.IxOffsetLong, CpuArgPos.First, new byte[] { 0xdd, 0xed, 0x2d, 0x00, 0x00 }, 3, null, 0 ), //LDW (IX+nn),IX
            ("LDW IY", CpuInstrArgType.IxOffsetLong, CpuArgPos.First, new byte[] { 0xfd, 0xed, 0x2d, 0x00, 0x00 }, 3, null, 0 ), //LDW (IX+nn),IY
            ("LDW BC", CpuInstrArgType.IxOffset, CpuArgPos.First, new byte[] { 0xdd, 0xed, 0x0e, 0x00 }, 3, null, 0 ), //LDW (IX+n),BC
            ("LDW DE", CpuInstrArgType.IxOffset, CpuArgPos.First, new byte[] { 0xdd, 0xed, 0x1e, 0x00 }, 3, null, 0 ), //LDW (IX+n),DE
            ("LDW HL", CpuInstrArgType.IxOffset, CpuArgPos.First, new byte[] { 0xdd, 0xed, 0x2e, 0x00 }, 3, null, 0 ), //LDW (IX+n),HL
            ("LDW SP", CpuInstrArgType.IxOffset, CpuArgPos.First, new byte[] { 0xdd, 0xed, 0x3e, 0x00 }, 3, null, 0 ), //LDW (IX+n),SP
            ("LDW HL", CpuInstrArgType.IyOffset, CpuArgPos.First, new byte[] { 0xfd, 0xed, 0x2e, 0x00 }, 3, new byte[] { 0xed, 0x35, 0x00, 0x00 }, 2 ), //LDW (IY+nn),HL
            ("LDW IX", CpuInstrArgType.IyOffsetLong, CpuArgPos.First, new byte[] { 0xdd, 0xed, 0x35, 0x00, 0x00 }, 3, null, 0 ), //LDW (IY+nn),IX
            ("LDW IY", CpuInstrArgType.IyOffsetLong, CpuArgPos.First, new byte[] { 0xfd, 0xed, 0x35, 0x00, 0x00 }, 3, null, 0 ), //LDW (IY+nn),IY
            ("LDW BC", CpuInstrArgType.IyOffset, CpuArgPos.First, new byte[] { 0xfd, 0xed, 0x0e, 0x00 }, 3, null, 0 ), //LDW (IY+n),BC
            ("LDW DE", CpuInstrArgType.IyOffset, CpuArgPos.First, new byte[] { 0xfd, 0xed, 0x1e, 0x00 }, 3, null, 0 ), //LDW (IY+n),DE
            ("LDW HL", CpuInstrArgType.IyOffset, CpuArgPos.First, new byte[] { 0xfd, 0xed, 0x2e, 0x00 }, 3, null, 0 ), //LDW (IY+n),HL
            ("LDW SP", CpuInstrArgType.IyOffset, CpuArgPos.First, new byte[] { 0xfd, 0xed, 0x3e, 0x00 }, 3, null, 0 ), //LDW (IY+n),SP
            ("LDW HL", CpuInstrArgType.PcOffset, CpuArgPos.First, new byte[] { 0xed, 0x25, 0x00, 0x00 }, 2, null, 0 ), //LDW (PC+nn),HL
            ("LDW IX", CpuInstrArgType.PcOffset, CpuArgPos.First, new byte[] { 0xdd, 0xed, 0x25, 0x00, 0x00 }, 3, null, 0 ), //LDW (PC+nn),IX
            ("LDW IY", CpuInstrArgType.PcOffset, CpuArgPos.First, new byte[] { 0xfd, 0xed, 0x25, 0x00, 0x00 }, 3, null, 0 ), //LDW (PC+nn),IY
            ("LDW HL", CpuInstrArgType.SpOffset, CpuArgPos.First, new byte[] { 0xed, 0x05, 0x00, 0x00 }, 2, null, 0 ), //LDW (SP+nn),HL
            ("LDW IX", CpuInstrArgType.SpOffset, CpuArgPos.First, new byte[] { 0xdd, 0xed, 0x05, 0x00, 0x00 }, 3, null, 0 ), //LDW (SP+nn),IX
            ("LDW IY", CpuInstrArgType.SpOffset, CpuArgPos.First, new byte[] { 0xfd, 0xed, 0x05, 0x00, 0x00 }, 3, null, 0 ), //LDW (SP+nn),IY
            ("LDW BC", CpuInstrArgType.WordInParenthesis, CpuArgPos.Second, new byte[] { 0xed, 0x4b, 0x00, 0x00 }, 2, null, 0 ), //LDW BC,(nn)
            ("LDW BC", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0x06, 0x00 }, 3, null, 0 ), //LDW BC,(IX+n)
            ("LDW BC", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0x06, 0x00 }, 3, null, 0 ), //LDW BC,(IY+n)
            ("LDW BC", CpuInstrArgType.Word, CpuArgPos.Second, new byte[] { 0x01, 0x00, 0x00 }, 1, null, 0 ), //LDW BC,nn
            ("LDW DE", CpuInstrArgType.WordInParenthesis, CpuArgPos.Second, new byte[] { 0xed, 0x5b, 0x00, 0x00 }, 2, null, 0 ), //LDW DE,(nn)
            ("LDW DE", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0x16, 0x00 }, 3, null, 0 ), //LDW DE,(IX+n)
            ("LDW DE", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0x16, 0x00 }, 3, null, 0 ), //LDW DE,(IY+n)
            ("LDW DE", CpuInstrArgType.Word, CpuArgPos.Second, new byte[] { 0x11, 0x00, 0x00 }, 1, null, 0 ), //LDW DE,nn
            ("LDW HL", CpuInstrArgType.WordInParenthesis, CpuArgPos.Second, new byte[] { 0x2a, 0x00, 0x00 }, 1, null, 0 ), //LDW HL,(nn)
            ("LDW HL", CpuInstrArgType.HlOffset, CpuArgPos.Second, new byte[] { 0xed, 0x3c, 0x00, 0x00 }, 2, null, 0 ), //LDW HL,(HL+nn)
            ("LDW HL", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0x26, 0x00 }, 3, new byte[] { 0xed, 0x2c, 0x00, 0x00 }, 2 ), //LDW HL,(IX+nn)
            ("LDW HL", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0x26, 0x00 }, 3, null, 0 ), //LDW HL,(IX+n)
            ("LDW HL", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0x26, 0x00 }, 3, new byte[] { 0xed, 0x34, 0x00, 0x00 }, 2 ), //LDW HL,(IY+nn)
            ("LDW HL", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0x26, 0x00 }, 3, null, 0 ), //LDW HL,(IY+n)
            ("LDW HL", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xed, 0x24, 0x00, 0x00 }, 2, null, 0 ), //LDW HL,(PC+nn)
            ("LDW HL", CpuInstrArgType.SpOffset, CpuArgPos.Second, new byte[] { 0xed, 0x04, 0x00, 0x00 }, 2, null, 0 ), //LDW HL,(SP+nn)
            ("LDW HL", CpuInstrArgType.Word, CpuArgPos.Second, new byte[] { 0x21, 0x00, 0x00 }, 1, null, 0 ), //LDW HL,nn
            ("LDW IX", CpuInstrArgType.WordInParenthesis, CpuArgPos.Second, new byte[] { 0xdd, 0x2a, 0x00, 0x00 }, 2, null, 0 ), //LDW IX,(nn)
            ("LDW IX", CpuInstrArgType.HlOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0x3c, 0x00, 0x00 }, 3, null, 0 ), //LDW IX,(HL+nn)
            ("LDW IX", CpuInstrArgType.IxOffsetLong, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0x2c, 0x00, 0x00 }, 3, null, 0 ), //LDW IX,(IX+nn)
            ("LDW IX", CpuInstrArgType.IyOffsetLong, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0x34, 0x00, 0x00 }, 3, null, 0 ), //LDW IX,(IY+nn)
            ("LDW IX", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0x24, 0x00, 0x00 }, 3, null, 0 ), //LDW IX,(PC+nn)
            ("LDW IX", CpuInstrArgType.SpOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0x04, 0x00, 0x00 }, 3, null, 0 ), //LDW IX,(SP+nn)
            ("LDW IX", CpuInstrArgType.Word, CpuArgPos.Second, new byte[] { 0xdd, 0x21, 0x00, 0x00 }, 2, null, 0 ), //LDW IX,nn
            ("LDW IY", CpuInstrArgType.WordInParenthesis, CpuArgPos.Second, new byte[] { 0xfd, 0x2a, 0x00, 0x00 }, 2, null, 0 ), //LDW IY,(nn)
            ("LDW IY", CpuInstrArgType.HlOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0x3c, 0x00, 0x00 }, 3, null, 0 ), //LDW IY,(HL+nn)
            ("LDW IY", CpuInstrArgType.IxOffsetLong, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0x2c, 0x00, 0x00 }, 3, null, 0 ), //LDW IY,(IX+nn)
            ("LDW IY", CpuInstrArgType.IyOffsetLong, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0x34, 0x00, 0x00 }, 3, null, 0 ), //LDW IY,(IY+nn)
            ("LDW IY", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0x24, 0x00, 0x00 }, 3, null, 0 ), //LDW IY,(PC+nn)
            ("LDW IY", CpuInstrArgType.SpOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0x04, 0x00, 0x00 }, 3, null, 0 ), //LDW IY,(SP+nn)
            ("LDW IY", CpuInstrArgType.Word, CpuArgPos.Second, new byte[] { 0xfd, 0x21, 0x00, 0x00 }, 2, null, 0 ), //LDW IY,nn
            ("LDW SP", CpuInstrArgType.WordInParenthesis, CpuArgPos.Second, new byte[] { 0xed, 0x7b, 0x00, 0x00 }, 2, null, 0 ), //LDW SP,(nn)
            ("LDW SP", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0x36, 0x00 }, 3, null, 0 ), //LDW SP,(IX+n)
            ("LDW SP", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0x36, 0x00 }, 3, null, 0 ), //LDW SP,(IY+n)
            ("LDW SP", CpuInstrArgType.Word, CpuArgPos.Second, new byte[] { 0x31, 0x00, 0x00 }, 1, null, 0 ), //LDW SP,nn

            ("MEPU", CpuInstrArgType.WordInParenthesis, CpuArgPos.Single, new byte[] { 0xed, 0xaf, 0x00, 0x00 }, 2, null, 0 ), //MEPU (nn)
            ("MEPU", CpuInstrArgType.HlOffset, CpuArgPos.Single, new byte[] { 0xed, 0xbd, 0x00, 0x00 }, 2, null, 0 ), //MEPU (HL+nn)
            ("MEPU", CpuInstrArgType.IxOffsetLong, CpuArgPos.Single, new byte[] { 0xed, 0xad, 0x00, 0x00 }, 2, null, 0 ), //MEPU (IX+nn)
            ("MEPU", CpuInstrArgType.IyOffsetLong, CpuArgPos.Single, new byte[] { 0xed, 0xb5, 0x00, 0x00 }, 2, null, 0 ), //MEPU (IY+nn)
            ("MEPU", CpuInstrArgType.PcOffset, CpuArgPos.Single, new byte[] { 0xed, 0xa5, 0x00, 0x00 }, 2, null, 0 ), //MEPU (PC+nn)
            ("MEPU", CpuInstrArgType.SpOffset, CpuArgPos.Single, new byte[] { 0xed, 0x85, 0x00, 0x00 }, 2, null, 0 ), //MEPU (SP+nn)

            ("MULT A", CpuInstrArgType.WordInParenthesis, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0xf8, 0x00, 0x00 }, 3, null, 0 ), //MULT A,(nn)
            ("MULT A", CpuInstrArgType.HlOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xd8, 0x00, 0x00 }, 3, null, 0 ), //MULT A,(HL+nn)
            ("MULT A", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0xf0, 0x00 }, 3, new byte[] { 0xfd, 0xed, 0xc8, 0x00, 0x00 }, 3 ), //MULT A,(IX+nn)
            ("MULT A", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0xf0, 0x00 }, 3, null, 0 ), //MULT A,(IX+n)
            ("MULT A", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xf0, 0x00 }, 3, new byte[] { 0xfd, 0xed, 0xd0, 0x00, 0x00 }, 3 ), //MULT A,(IY+nn)
            ("MULT A", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xf0, 0x00 }, 3, null, 0 ), //MULT A,(IY+n)
            ("MULT A", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xc0, 0x00, 0x00 }, 3, null, 0 ), //MULT A,(PC+nn)
            ("MULT A", CpuInstrArgType.SpOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0xc0, 0x00, 0x00 }, 3, null, 0 ), //MULT A,(SP+nn)
            ("MULT A", CpuInstrArgType.Byte, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xf8, 0x00 }, 3, null, 0 ), //MULT A,n

            ("MULTU A", CpuInstrArgType.WordInParenthesis, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0xf9, 0x00, 0x00 }, 3, null, 0 ), //MULTU A,(nn)
            ("MULTU A", CpuInstrArgType.HlOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xd9, 0x00, 0x00 }, 3, null, 0 ), //MULTU A,(HL+nn)
            ("MULTU A", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0xf1, 0x00 }, 3, new byte[] { 0xfd, 0xed, 0xc9, 0x00, 0x00 }, 3 ), //MULTU A,(IX+nn)
            ("MULTU A", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0xf1, 0x00 }, 3, null, 0 ), //MULTU A,(IX+n)
            ("MULTU A", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xf1, 0x00 }, 3, new byte[] { 0xfd, 0xed, 0xd1, 0x00, 0x00 }, 3 ), //MULTU A,(IY+nn)
            ("MULTU A", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xf1, 0x00 }, 3, null, 0 ), //MULTU A,(IY+n)
            ("MULTU A", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xc1, 0x00, 0x00 }, 3, null, 0 ), //MULTU A,(PC+nn)
            ("MULTU A", CpuInstrArgType.SpOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0xc1, 0x00, 0x00 }, 3, null, 0 ), //MULTU A,(SP+nn)
            ("MULTU A", CpuInstrArgType.Byte, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xf9, 0x00 }, 3, null, 0 ), //MULTU A,n

            ("MULTUW HL", CpuInstrArgType.WordInParenthesis, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0xd3, 0x00, 0x00 }, 3, null, 0 ), //MULTUW HL,(nn)
            ("MULTUW HL", CpuInstrArgType.IxOffsetLong, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xc3, 0x00, 0x00 }, 3, null, 0 ), //MULTUW HL,(IX+nn)
            ("MULTUW HL", CpuInstrArgType.IyOffsetLong, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xd3, 0x00, 0x00 }, 3, null, 0 ), //MULTUW HL,(IY+nn)
            ("MULTUW HL", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0xf3, 0x00, 0x00 }, 3, null, 0 ), //MULTUW HL,(PC+nn)
            ("MULTUW HL", CpuInstrArgType.Word, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xf3, 0x00, 0x00 }, 3, null, 0 ), //MULTUW HL,nn

            ("MULTW HL", CpuInstrArgType.WordInParenthesis, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0xd2, 0x00, 0x00 }, 3, null, 0 ), //MULTW HL,(nn)
            ("MULTW HL", CpuInstrArgType.IxOffsetLong, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xc2, 0x00, 0x00 }, 3, null, 0 ), //MULTW HL,(IX+nn)
            ("MULTW HL", CpuInstrArgType.IyOffsetLong, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xd2, 0x00, 0x00 }, 3, null, 0 ), //MULTW HL,(IY+nn)
            ("MULTW HL", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0xf2, 0x00, 0x00 }, 3, null, 0 ), //MULTW HL,(PC+nn)
            ("MULTW HL", CpuInstrArgType.Word, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xf2, 0x00, 0x00 }, 3, null, 0 ), //MULTW HL,nn

            ("OR A", CpuInstrArgType.WordInParenthesis, CpuArgPos.Second, new byte[] { 0xdd, 0xb7, 0x04, 0x33 }, 2, null, 0 ), //OR A,(nn)
            ("OR A", CpuInstrArgType.HlOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xb3, 0x00, 0x00 }, 2, null, 0 ), //OR A,(HL+nn)
            ("OR A", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xb6, 0x00 }, 2, new byte[] { 0xfd, 0xb1, 0x00, 0x00 }, 2 ), //OR A,(IX+nn)
            ("OR A", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xb6, 0x00 }, 2, new byte[] { 0xfd, 0xb2, 0x00, 0x00 }, 2 ), //OR A,(IY+nn)
            ("OR A", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xb0, 0x00, 0x00 }, 2, null, 0 ), //OR A,(PC+nn)
            ("OR A", CpuInstrArgType.SpOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xb0, 0x00, 0x00 }, 2, null, 0 ), //OR A,(SP+nn)

            //Aliases for "OR A,..." with implicit A
            ("OR", CpuInstrArgType.WordInParenthesis, CpuArgPos.Single, new byte[] { 0xdd, 0xb7, 0x04, 0x33 }, 2, null, 0 ), //OR (nn)
            ("OR", CpuInstrArgType.HlOffset, CpuArgPos.Single, new byte[] { 0xfd, 0xb3, 0x00, 0x00 }, 2, null, 0 ), //OR (HL+nn)
            ("OR", CpuInstrArgType.IxOffset, CpuArgPos.Single, new byte[] { 0xdd, 0xb6, 0x00 }, 2, new byte[] { 0xfd, 0xb1, 0x00, 0x00 }, 2 ), //OR (IX+nn)
            ("OR", CpuInstrArgType.IyOffset, CpuArgPos.Single, new byte[] { 0xfd, 0xb6, 0x00 }, 2, new byte[] { 0xfd, 0xb2, 0x00, 0x00 }, 2 ), //OR (IY+nn)
            ("OR", CpuInstrArgType.PcOffset, CpuArgPos.Single, new byte[] { 0xfd, 0xb0, 0x00, 0x00 }, 2, null, 0 ), //OR (PC+nn)
            ("OR", CpuInstrArgType.SpOffset, CpuArgPos.Single, new byte[] { 0xdd, 0xb0, 0x00, 0x00 }, 2, null, 0 ), //OR (SP+nn)

            ("OUT (C)", CpuInstrArgType.WordInParenthesis, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0x79, 0x00, 0x00 }, 3, null, 0 ), //OUT (C),(nn)
            ("OUT (C)", CpuInstrArgType.HlOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0x59, 0x00, 0x00 }, 3, null, 0 ), //OUT (C),(HL+nn)
            ("OUT (C)", CpuInstrArgType.IxOffsetLong, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0x49, 0x00, 0x00 }, 3, null, 0 ), //OUT (C),(IX+nn)
            ("OUT (C)", CpuInstrArgType.IyOffsetLong, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0x51, 0x00, 0x00 }, 3, null, 0 ), //OUT (C),(IY+nn)
            ("OUT (C)", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0x41, 0x00, 0x00 }, 3, null, 0 ), //OUT (C),(PC+nn)
            ("OUT (C)", CpuInstrArgType.SpOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0x41, 0x00, 0x00 }, 3, null, 0 ), //OUT (C),(SP+nn)

            ("POP", CpuInstrArgType.WordInParenthesis, CpuArgPos.Single, new byte[] { 0xdd, 0xd1, 0x00, 0x00 }, 2, null, 0 ), //POP (nn)
            ("POP", CpuInstrArgType.PcOffset, CpuArgPos.Single, new byte[] { 0xdd, 0xf1, 0x00, 0x00 }, 2, null, 0 ), //POP (PC+nn)

            ("PUSH", CpuInstrArgType.WordInParenthesis, CpuArgPos.Single, new byte[] { 0xdd, 0xd5, 0x00, 0x00 }, 2, null, 0 ), //PUSH (nn)
            ("PUSH", CpuInstrArgType.PcOffset, CpuArgPos.Single, new byte[] { 0xdd, 0xf5, 0x00, 0x00 }, 2, null, 0 ), //PUSH (PC+nn)
            ("PUSH", CpuInstrArgType.Word, CpuArgPos.Single, new byte[] { 0xfd, 0xf5, 0x00, 0x00 }, 2, null, 0 ), //PUSH nn

            ("SBC A", CpuInstrArgType.WordInParenthesis, CpuArgPos.Second, new byte[] { 0xdd, 0x9f, 0x00, 0x00 }, 2, null, 0 ), //SBC A,(nn)
            ("SBC A", CpuInstrArgType.HlOffset, CpuArgPos.Second, new byte[] { 0xfd, 0x9b, 0x00, 0x00 }, 2, null, 0 ), //SBC A,(HL+nn)
            ("SBC A", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0x9e, 0x00 }, 2, new byte[] { 0xfd, 0x99, 0x00, 0x00 }, 2 ), //SBC A,(IX+nn)
            ("SBC A", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0x9e, 0x00 }, 2, new byte[] { 0xfd, 0x9a, 0x00, 0x00 }, 2 ), //SBC A,(IY+nn)
            ("SBC A", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xfd, 0x98, 0x00, 0x00 }, 2, null, 0 ), //SBC A,(PC+nn)
            ("SBC A", CpuInstrArgType.SpOffset, CpuArgPos.Second, new byte[] { 0xdd, 0x98, 0x00, 0x00 }, 2, null, 0 ), //SBC A,(SP+nn)

            //Aliases for "SBC A,..." with implicit A
            ("SBC", CpuInstrArgType.WordInParenthesis, CpuArgPos.Single, new byte[] { 0xdd, 0x9f, 0x00, 0x00 }, 2, null, 0 ), //SBC (nn)
            ("SBC", CpuInstrArgType.HlOffset, CpuArgPos.Single, new byte[] { 0xfd, 0x9b, 0x00, 0x00 }, 2, null, 0 ), //SBC (HL+nn)
            ("SBC", CpuInstrArgType.IxOffset, CpuArgPos.Single, new byte[] { 0xdd, 0x9e, 0x00 }, 2, new byte[] { 0xfd, 0x99, 0x00, 0x00 }, 2 ), //SBC (IX+nn)
            ("SBC", CpuInstrArgType.IyOffset, CpuArgPos.Single, new byte[] { 0xfd, 0x9e, 0x00 }, 2, new byte[] { 0xfd, 0x9a, 0x00, 0x00 }, 2 ), //SBC (IY+nn)
            ("SBC", CpuInstrArgType.PcOffset, CpuArgPos.Single, new byte[] { 0xfd, 0x98, 0x00, 0x00 }, 2, null, 0 ), //SBC (PC+nn)
            ("SBC", CpuInstrArgType.SpOffset, CpuArgPos.Single, new byte[] { 0xdd, 0x98, 0x00, 0x00 }, 2, null, 0 ), //SBC (SP+nn)

            ("SC", CpuInstrArgType.Word, CpuArgPos.Single, new byte[] { 0xed, 0x71, 0x00, 0x00 }, 2, null, 0 ), //SC nn

            ("SUB A", CpuInstrArgType.WordInParenthesis, CpuArgPos.Second, new byte[] { 0xdd, 0x97, 0x04, 0x33 }, 2, null, 0 ), //SUB A,(nn)
            ("SUB A", CpuInstrArgType.HlOffset, CpuArgPos.Second, new byte[] { 0xfd, 0x93, 0x00, 0x00 }, 2, null, 0 ), //SUB A,(HL+nn)
            ("SUB A", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0x96, 0x00 }, 2, new byte[] { 0xfd, 0x91, 0x00, 0x00 }, 2 ), //SUB A,(IX+nn)
            ("SUB A", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0x96, 0x00 }, 2, new byte[] { 0xfd, 0x92, 0x00, 0x00 }, 2 ), //SUB A,(IY+nn)
            ("SUB A", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xfd, 0x90, 0x00, 0x00 }, 2, null, 0 ), //SUB A,(PC+nn)
            ("SUB A", CpuInstrArgType.SpOffset, CpuArgPos.Second, new byte[] { 0xdd, 0x90, 0x00, 0x00 }, 2, null, 0 ), //SUB A,(SP+nn)

            //Aliases for "SUB A,..." with implicit A
            ("SUB", CpuInstrArgType.WordInParenthesis, CpuArgPos.Single, new byte[] { 0xdd, 0x97, 0x04, 0x33 }, 2, null, 0 ), //SUB (nn)
            ("SUB", CpuInstrArgType.HlOffset, CpuArgPos.Single, new byte[] { 0xfd, 0x93, 0x00, 0x00 }, 2, null, 0 ), //SUB (HL+nn)
            ("SUB", CpuInstrArgType.IxOffset, CpuArgPos.Single, new byte[] { 0xdd, 0x96, 0x00 }, 2, new byte[] { 0xfd, 0x91, 0x00, 0x00 }, 2 ), //SUB (IX+nn)
            ("SUB", CpuInstrArgType.IyOffset, CpuArgPos.Single, new byte[] { 0xfd, 0x96, 0x00 }, 2, new byte[] { 0xfd, 0x92, 0x00, 0x00 }, 2 ), //SUB (IY+nn)
            ("SUB", CpuInstrArgType.PcOffset, CpuArgPos.Single, new byte[] { 0xfd, 0x90, 0x00, 0x00 }, 2, null, 0 ), //SUB (PC+nn)
            ("SUB", CpuInstrArgType.SpOffset, CpuArgPos.Single, new byte[] { 0xdd, 0x90, 0x00, 0x00 }, 2, null, 0 ), //SUB (SP+nn)

            ("SUBW HL", CpuInstrArgType.WordInParenthesis, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0xde, 0x00, 0x00 }, 3, null, 0 ), //SUBW HL,(nn)
            ("SUBW HL", CpuInstrArgType.IxOffsetLong, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xce, 0x00, 0x00 }, 3, null, 0 ), //SUBW HL,(IX+nn)
            ("SUBW HL", CpuInstrArgType.IyOffsetLong, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xde, 0x00, 0x00 }, 3, null, 0 ), //SUBW HL,(IY+nn)
            ("SUBW HL", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xed, 0xfe, 0x00, 0x00 }, 3, null, 0 ), //SUBW HL,(PC+nn)
            ("SUBW HL", CpuInstrArgType.Word, CpuArgPos.Second, new byte[] { 0xfd, 0xed, 0xfe, 0x00, 0x00 }, 3, null, 0 ), //SUBW HL,nn

            ("TSET", CpuInstrArgType.IxOffset, CpuArgPos.Single, new byte[] { 0xdd, 0xcb, 0x00, 0x36 }, 2, null, 0 ), //TSET (IX+n)
            ("TSET", CpuInstrArgType.IyOffset, CpuArgPos.Single, new byte[] { 0xfd, 0xcb, 0x00, 0x36 }, 2, null, 0 ), //TSET (IY+n)

            ("XOR A", CpuInstrArgType.WordInParenthesis, CpuArgPos.Second, new byte[] { 0xdd, 0xaf, 0x00, 0x00 }, 2, null, 0 ), //XOR A,(nn)
            ("XOR A", CpuInstrArgType.HlOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xab, 0x00, 0x00 }, 2, null, 0 ), //XOR A,(HL+nn)
            ("XOR A", CpuInstrArgType.IxOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xae, 0x00 }, 2, new byte[] { 0xfd, 0xa9, 0x00, 0x00 }, 2 ), //XOR A,(IX+nn)
            ("XOR A", CpuInstrArgType.IyOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xae, 0x00 }, 2, new byte[] { 0xfd, 0xaa, 0x00, 0x00 }, 2 ), //XOR A,(IY+nn)
            ("XOR A", CpuInstrArgType.PcOffset, CpuArgPos.Second, new byte[] { 0xfd, 0xa8, 0x00, 0x00 }, 2, null, 0 ), //XOR A,(PC+nn)
            ("XOR A", CpuInstrArgType.SpOffset, CpuArgPos.Second, new byte[] { 0xdd, 0xa8, 0x00, 0x00 }, 2, null, 0 ), //XOR A,(SP+nn)

            //Aliases for "XOR A,..." with implicit A
            ("XOR", CpuInstrArgType.WordInParenthesis, CpuArgPos.Single, new byte[] { 0xdd, 0xaf, 0x00, 0x00 }, 2, null, 0 ), //XOR (nn)
            ("XOR", CpuInstrArgType.HlOffset, CpuArgPos.Single, new byte[] { 0xfd, 0xab, 0x00, 0x00 }, 2, null, 0 ), //XOR (HL+nn)
            ("XOR", CpuInstrArgType.IxOffset, CpuArgPos.Single, new byte[] { 0xdd, 0xae, 0x00 }, 2, new byte[] { 0xfd, 0xa9, 0x00, 0x00 }, 2 ), //XOR (IX+nn)
            ("XOR", CpuInstrArgType.IyOffset, CpuArgPos.Single, new byte[] { 0xfd, 0xae, 0x00 }, 2, new byte[] { 0xfd, 0xaa, 0x00, 0x00 }, 2 ), //XOR (IY+nn)
            ("XOR", CpuInstrArgType.PcOffset, CpuArgPos.Single, new byte[] { 0xfd, 0xa8, 0x00, 0x00 }, 2, null, 0 ), //XOR (PC+nn)
            ("XOR", CpuInstrArgType.SpOffset, CpuArgPos.Single, new byte[] { 0xdd, 0xa8, 0x00, 0x00 }, 2, null, 0 ), //XOR (SP+nn)

            // Zero-index versions of the indexed instructions with two variable arguments
            ("LD (IX)", CpuInstrArgType.Byte, CpuArgPos.Second, new byte[] { 0xdd, 0x36, 0, 0 }, 3, new byte[] { 0xfd, 0x0e, 0, 0, 0 }, 4), // LD (IX),n
            ("LD (IY)", CpuInstrArgType.Byte, CpuArgPos.Second, new byte[] { 0xfd, 0x36, 0, 0 }, 3, new byte[] { 0xfd, 0x16, 0, 0, 0 }, 4), // LD (IY),n
            ("LD (PC)", CpuInstrArgType.Byte, CpuArgPos.Second, new byte[] { 0xfd, 0x06, 0, 0, 0 }, 4, null, 0), // LD (PC),n
            ("LD (SP)", CpuInstrArgType.Byte, CpuArgPos.Second, new byte[] { 0xdd, 0x06, 0, 0, 0 }, 4, null, 0), // LD (SP),n

            ("LDW (PC)", CpuInstrArgType.Word, CpuArgPos.Second, new byte[] { 0xdd, 0x31, 0, 0, 0, 0 }, 4, null, 0), // LDW (PC),nn
        };

        static readonly (string, byte[], ushort)[] im3instructionData = new (string, byte[], ushort)[] { (null, new byte[] { 0xed, 0x4e }, 3) };


        /* These are handled as special cases within the assembler code.

        //First argument goes in position 2, second argument comes after that.
        static readonly (string, CpuInstrArgType, CpuInstrArgType, byte[])[]
            Z280InstructionsWithTwoVariableArguments = new (string, CpuInstrArgType, CpuInstrArgType, byte[])[] {
                ("LD", CpuInstrArgType.WordInParenthesis, CpuInstrArgType.Byte, new byte[] { 0xdd, 0x3e, 0, 0, 0 }), // LD (nn),n
                ("LD", CpuInstrArgType.HlOffset, CpuInstrArgType.Byte, new byte[] { 0xfd, 0x1e, 0, 0, 0 }), // LD (HL+nn),n
                ("LD", CpuInstrArgType.IxOffset, CpuInstrArgType.Byte, new byte[] { 0xdd, 0x36, 0, 0 }), // LD (IX+n),n
                ("LD", CpuInstrArgType.IxOffsetLong, CpuInstrArgType.Byte, new byte[] { 0xfd, 0x0e, 0, 0, 0 }), // LD (IX+nn),n
                ("LD", CpuInstrArgType.IyOffset, CpuInstrArgType.Byte, new byte[] { 0xfd, 0x36, 0, 0 }), // LD (IY+n),n
                ("LD", CpuInstrArgType.IyOffsetLong, CpuInstrArgType.Byte, new byte[] { 0xfd, 0x16, 0, 0, 0 }), // LD (IY+nn),n
                ("LD", CpuInstrArgType.PcOffset, CpuInstrArgType.Byte, new byte[] { 0xfd, 0x06, 0, 0, 0 }), // LD (PC+nn),n
                ("LD", CpuInstrArgType.SpOffset, CpuInstrArgType.Byte, new byte[] { 0xdd, 0x06, 0, 0, 0 }), // LD (SP+nn),n
                ("LDW", CpuInstrArgType.WordInParenthesis, CpuInstrArgType.Word, new byte[] { 0xdd, 0x11, 0, 0, 0, 0 }), // LDW (nn),nn
                ("LDW", CpuInstrArgType.PcOffset, CpuInstrArgType.Word, new byte[] { 0xdd, 0x31, 0, 0, 0, 0 }), // LDW (PC+nn),nn
            };
        */
    }
}
