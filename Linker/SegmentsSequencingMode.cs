namespace Konamiman.Nestor80.Linker
{
    internal enum SegmentsSequencingMode
    {
        None,
        DataBeforeCode,
        CodeBeforeData,
        CombineSameSegment
    }
}
