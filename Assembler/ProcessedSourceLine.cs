namespace Konamiman.Nestor80.Assembler
{
    /// <summary>
    /// Base class for the classes tha represent already processed source code lines.
    /// The assembly process results in one instance of the appropriate class for each and every source line processed,
    /// no exceptions, including blank lines and lines that are inside a false conditional block.
    /// </summary>
    public abstract class ProcessedSourceLine
    {
        /// <summary>
        /// The original source code line with the form feed characters removed.
        /// </summary>
        public string Line { get; set; }

        /// <summary>
        /// The line length not including the comment if present.
        /// </summary>
        public int EffectiveLineLength { get; set; } = -1;
      
        /// <summary>
        /// The line label if present.
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// The line CPU opcode or pseudo-operator instruction, if present, uppercased.
        /// </summary>
        private string _Opcode = null;
        public string Opcode
        {
            get => _Opcode;
            set
            {
                _Opcode = value?.ToUpper();
            }
        }

        /// <summary>
        /// How many form feed characters (FFh) were present in the original source code line.
        /// </summary>
        public int FormFeedsCount { get; set; }

        public override string ToString() => Label + " " + (Opcode is null ? "" : Opcode + " ");
    }
}
