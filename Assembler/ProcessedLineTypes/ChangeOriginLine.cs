namespace Konamiman.Nestor80.Assembler.Output
{
    public class ChangeOriginLine : ProcessedSourceLine, IChangesLocationCounter
    {
        public AddressType NewLocationArea { get; set; }
        public ushort NewLocationCounter { get; set; }

        public string SdccAreaName { get; set; }

        public override string ToString() => $"{base.ToString()}, {(IsSdccBuild ? SdccAreaName : NewLocationArea)} {NewLocationCounter}";
    }
}
