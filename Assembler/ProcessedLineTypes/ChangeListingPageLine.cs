namespace Konamiman.Nestor80.Assembler.Output
{
    public class ChangeListingPageLine : ProcessedSourceLine
    {
        public bool IsMainPageChange { get; set; }

        public int NewPageSize { get; set; }

        public override string ToString() => $"{base.ToString()}, {NewPageSize}{(IsMainPageChange ? ", main page change" : "")}";
    }
}
