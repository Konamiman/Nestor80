namespace Konamiman.Nestor80.Assembler.Expressions
{
    public class StoreAsByte
    {
        private StoreAsByte()
        {
        }

        public static StoreAsByte Instance => new();

        public override string ToString() => "Store as byte";
    }
}
