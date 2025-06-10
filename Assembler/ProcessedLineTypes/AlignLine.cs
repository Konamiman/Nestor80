namespace Konamiman.Nestor80.Assembler.Output
{
    // An alignment works effectively as a define space line, so we recycle the DefineSpaceLine class
    // repurposing the "Size" property to hold the actual calculated address alignment value.
    public class AlignLine : DefineSpaceLine
    {
        public ushort DeclaredAlignmentValue { get; set; }

        public override string ToString() => $"{base.ToString()}, {DeclaredAlignmentValue} (actual: {Size}), {Value}";
    }
}
