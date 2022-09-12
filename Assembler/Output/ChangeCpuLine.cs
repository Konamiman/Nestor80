namespace Konamiman.Nestor80.Assembler.Output
{
    public class ChangeCpuLine : ProcessedSourceLine
    {
        public CpuType Cpu { get; set; }

        public override string ToString() => $"{base.ToString()}, {Cpu}";
    }
}
