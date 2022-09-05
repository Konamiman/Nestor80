namespace Konamiman.Nestor80.Assembler.Output
{
    public abstract class ProcessedSourceLine
    {
        public ProcessedSourceLine(string line, int effectiveLength = 0, string label = null, string operand = null)
        {
            this.Line = line;
            this.EffectiveLineLength = effectiveLength;
            this.Label = label;
            this.Operand = operand;
        }

        public string Line { get; }

        public int EffectiveLineLength { get; internal set; }

        private string _Label = null;
        public string Label { 
            get => _Label;
            set
            {
                EffectiveLabel = value?.TrimEnd(':');
                LabelIsPublic = value?.EndsWith("::") ?? false;
                _Label = value;
            }
        }

        public string EffectiveLabel { get; private set; }

        public bool LabelIsPublic { get; private set; }

        private string _Operand;
        public string Operand
        {
            get => _Operand;
            init
            {
                _Operand = value?.ToUpper();
            }
        }

        public override string ToString() => Label + " " + Operand is null ? "" : Operand + " ";
    }
}
