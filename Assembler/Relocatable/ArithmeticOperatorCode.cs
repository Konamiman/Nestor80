namespace Konamiman.Nestor80.Assembler.Relocatable
{
    /// <summary>
    /// Codes for the "arithmetic operator" extension link item as supported by LINK-80.
    /// </summary>
    public enum ArithmeticOperatorCode
    {
        StoreAsByte = 1,
        StoreAsWord = 2,
        High = 3,
        Low = 4,
        Not = 5,
        UnaryMinus = 6,
        Minus = 7,
        Plus = 8,
        Multiply = 9,
        Divide = 10,
        Mod = 11
    }
}
