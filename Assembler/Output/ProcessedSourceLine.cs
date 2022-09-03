namespace Konamiman.Nestor80.Assembler.Output
{
    public abstract class ProcessedSourceLine
    {
        public ProcessedSourceLine(string line, int effectiveLength, string label = null)
        {
            this.Line = line;
            this.EffectiveLineLength = effectiveLength;
            this.Label = label;
        }

        public string Line { get; }

        public int EffectiveLineLength { get; }

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
    }
}
