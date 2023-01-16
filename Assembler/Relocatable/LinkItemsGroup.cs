namespace Konamiman.Nestor80.Assembler.Relocatable
{
    /// <summary>
    /// Represents a group of "link items" as defined by the LINK-80 relocatable file format.
    /// </summary>
    public class LinkItemsGroup : RelocatableOutputPart
    {
        public LinkItem[] LinkItems { get; set; }

        public override string ToString() => $"{base.ToString()}, {string.Join(" | ", LinkItems.Select(i => i.ToString()))}";
    }
}
