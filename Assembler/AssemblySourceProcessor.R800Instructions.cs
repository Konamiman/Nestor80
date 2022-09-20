namespace Konamiman.Nestor80.Assembler
{
    public partial class AssemblySourceProcessor
    {
        static readonly Dictionary<string, CpuInstruction[]> R800Instructions = new(StringComparer.OrdinalIgnoreCase) {
            { "MULUB", new CpuInstruction[] {
                new CpuInstruction("MULUB", "A", "A", new byte[] { 0xed, 0xf9 }),
                new CpuInstruction("MULUB", "A", "B", new byte[] { 0xed, 0xc1 }),
                new CpuInstruction("MULUB", "A", "C", new byte[] { 0xed, 0xc9 }),
                new CpuInstruction("MULUB", "A", "D", new byte[] { 0xed, 0xd1 }),
                new CpuInstruction("MULUB", "A", "E", new byte[] { 0xed, 0xd9 }),
                new CpuInstruction("MULUB", "A", "H", new byte[] { 0xed, 0xe1 }),
                new CpuInstruction("MULUB", "A", "L", new byte[] { 0xed, 0xe9 }),
            } },
            { "MULUW", new CpuInstruction[] {
                new CpuInstruction("MULUB", "HL", "BC", new byte[] { 0xed, 0xc3 }),
                new CpuInstruction("MULUB", "HL", "DE", new byte[] { 0xed, 0xd3 }),
                new CpuInstruction("MULUB", "HL", "HL", new byte[] { 0xed, 0xe3 }),
                new CpuInstruction("MULUB", "HL", "SP", new byte[] { 0xed, 0xf3 }),

            } }
        };
    }
}