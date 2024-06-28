namespace Konamiman.Nestor80.Assembler.Output
{
    public class ChangeOriginLine : ProcessedSourceLine, IChangesLocationCounter
    {
        public AddressType NewLocationArea { get; set; }
        public ushort NewLocationCounter { get; set; }

        public string SdasAreaName { get; set; }

        public override string ToString() => $"{base.ToString()}, {(IsSdasBuild ? SdasAreaName : NewLocationArea)} {NewLocationCounter}";
    }
}
