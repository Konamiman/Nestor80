namespace Konamiman.Nestor80
{
    /// <summary>
    /// Types of "link items" as defined by the Link80 relocatable file format.
    /// </summary>
    public enum LinkItemType: byte
    {
        EntrySymbol = 0,
        SelectCommonBlock = 1,
        ProgramName = 2,
        RequestLibrarySearch = 3,
        ExtensionLinkItem = 4,
        DefineCommonSize = 5,
        ChainExternal = 6,
        DefineEntryPoint = 7,
        ExternalMinusOffset = 8,
        ExternalPlusOffset = 9,
        DataAreaSize = 10,
        SetLocationCounter = 11,
        ChainAddress = 12,
        ProgramAreaSize = 13,
        EndProgram = 14,
        EndFile = 15
    }
}
