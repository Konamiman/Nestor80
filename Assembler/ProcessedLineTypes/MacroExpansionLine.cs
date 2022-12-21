using Konamiman.Nestor80.Assembler.Infrastructure;

namespace Konamiman.Nestor80.Assembler.Output
{
    public class MacroExpansionLine : LinesContainerLine
    {
        public MacroType MacroType { get; set; }

        public string Name { get; set; }

        public string Placeholder { get; set; }

        public int RepetitionsCount { get; set; }

        public string[] Parameters { get; set; }

        public override string ToString()
        {
            if(MacroType is MacroType.ReptWithCount) {
                return $"{base.ToString()} {RepetitionsCount}";
            }
            else {
                return $"{(MacroType is MacroType.Named ? $"MACRO EXP {Name}" : base.ToString())} {string.Join(',', Parameters ?? Array.Empty<string>())}";
            }
        }

    }
}
