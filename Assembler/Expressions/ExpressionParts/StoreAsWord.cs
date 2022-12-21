namespace Konamiman.Nestor80.Assembler.Expressions.ExpressionParts
{
    public class StoreAsWord : IExpressionPart
    {
        private StoreAsWord()
        {
        }

        public static StoreAsWord Instance => new();

        public override string ToString() => "Store as word";
    }
}
