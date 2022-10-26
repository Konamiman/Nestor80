namespace Konamiman.Nestor80.Assembler
{
    internal class InstructionPendingSelection
    {
        public byte[] InstructionBytes { get; set; }

        public ushort SelectorValue { get; set; }
    }
}
