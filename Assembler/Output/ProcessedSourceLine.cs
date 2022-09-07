namespace Konamiman.Nestor80.Assembler.Output
{
    public abstract class ProcessedSourceLine
    {
        public string Line { get; set; }

        public int EffectiveLineLength { get; set; }

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

        public bool LabelIsPublic { get; set; }

        private string _Opcode = null;
        public string Opcode
        {
            get => _Opcode;
            set
            {
                _Opcode = value?.ToUpper();
            }
        }

        public override string ToString() => Label + " " + (Opcode is null ? "" : Opcode + " ");
    }
}
