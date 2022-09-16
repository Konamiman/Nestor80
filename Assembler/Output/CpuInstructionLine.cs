namespace Konamiman.Nestor80.Assembler.Output
{
    public class CpuInstructionLine : ProcessedSourceLine, IProducesOutput
    {
        public CpuType Cpu { get; set; }

        public byte[] OutputBytes { get; set; }

        public RelocatableOutputPart[] RelocatableParts { get; set; }

        public AddressType NewLocationArea { get; set; }

        public ushort NewLocationCounter { get; set; }

        public string FirstArgumentTemplate { get; set; }

        public string SecondArgumentTemplate { get; set; }

        public bool IsInvalid { get; set; }

        public override string ToString() => $"{base.ToString()} {FirstArgumentTemplate}, {SecondArgumentTemplate}";
    }
}
