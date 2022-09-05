namespace Konamiman.Nestor80.Assembler.Output
{
    public class BlankLine : ProcessedSourceLine
    {
        public BlankLine(string label) : base("", 0, label)
        {
        }
        
        public override string ToString() => base.ToString() + "(blank)";
    }
}
