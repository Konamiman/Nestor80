namespace Konamiman.Nestor80.Assembler.Output
{
    internal class ChangeStringEncodingLine : ProcessedSourceLine
    {
        public string EncodingNameOrCodePage { get; set; }

        public bool IsDefault { get; set; }

        public bool IsSuccessful { get; set; }

        public override string ToString()
        {
            var s = base.ToString() + ", " + EncodingNameOrCodePage;
            if(IsDefault) s += ", default";
            if(!IsSuccessful) s += ", failed";

            return s;
        }
    }
}
