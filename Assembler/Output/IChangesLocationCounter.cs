namespace Konamiman.Nestor80.Assembler.Output
{
    public interface IChangesLocationCounter
    {
        AddressType NewLocationArea { get; set; }

        ushort NewLocationCounter { get; set; }
    }
}
