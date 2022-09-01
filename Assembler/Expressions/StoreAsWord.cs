namespace Konamiman.Nestor80.Assembler.Expressions
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
