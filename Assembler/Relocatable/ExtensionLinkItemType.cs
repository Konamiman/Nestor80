namespace Konamiman.Nestor80.Assembler.Relocatable
{
    //Special link item 'H':
    //Expects a file named like B plus .REL, text file having a number and a label,
    //the number is the start address of data area (which is placed anyway before program area),
    //the label is for I don't know what.

    public enum ExtensionLinkItemType
    {
        ArithmeticOperator = 0x41,
        ReferenceExternal = 0x42,
        Address = 0x43
    }
}
