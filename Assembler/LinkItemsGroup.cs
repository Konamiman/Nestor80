namespace Konamiman.Nestor80
{
    public class LinkItemsGroup : RelocatableOutputPart
    {
        public LinkItem[] LinkItems { get; set; }

        public override string ToString() => $"{base.ToString()}, {string.Join(" | ", LinkItems.Select(i => i.ToString()))}";
    }
}
