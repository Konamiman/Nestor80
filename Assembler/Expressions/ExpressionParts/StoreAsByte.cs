namespace Konamiman.Nestor80.Assembler.Expressions.ExpressionParts
{
    public class StoreAsByte : IExpressionPart
    {
        private StoreAsByte()
        {
        }

        public static StoreAsByte Instance => new();

        public override string ToString() => "Store as byte";
    }
}
