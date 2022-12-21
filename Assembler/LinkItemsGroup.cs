﻿namespace Konamiman.Nestor80
{
    /// <summary>
    /// Represents a group of "link items" as defined by the Link80 relocatable file format.
    /// </summary>
    public class LinkItemsGroup : RelocatableOutputPart
    {
        public LinkItem[] LinkItems { get; set; }

        public override string ToString() => $"{base.ToString()}, {string.Join(" | ", LinkItems.Select(i => i.ToString()))}";
    }
}
