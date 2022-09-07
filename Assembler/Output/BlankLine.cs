namespace Konamiman.Nestor80.Assembler.Output
{
    public class BlankLine : ProcessedSourceLine
    {
        public BlankLine() : base()
        {
            Line = "";
            EffectiveLineLength = 0;
        }
        
        public override string ToString() => base.ToString() + "(blank)";
    }
}
