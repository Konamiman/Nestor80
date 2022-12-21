﻿namespace Konamiman.Nestor80
{
    /// <summary>
    /// Base class for the relocatable items defined by the Link80 relocatable file format.
    /// </summary>
    public abstract class RelocatableOutputPart
    {
        public int Index { get; set; }

        public bool IsByte { get; set; }

        public override string ToString() => $"@{Index}, {(IsByte ? "byte" : "word")}";
    }
}
