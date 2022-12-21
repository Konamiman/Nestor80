namespace Konamiman.Nestor80.Assembler.Output
{
    /// <summary>
    /// Interface for the line types that change the location counter.
    /// </summary>
    public interface IChangesLocationCounter
    {
        AddressType NewLocationArea { get; set; }

        ushort NewLocationCounter { get; set; }
    }
}
