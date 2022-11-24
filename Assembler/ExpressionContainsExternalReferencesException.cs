namespace Konamiman.Nestor80.Assembler
{
    internal class ExpressionContainsExternalReferencesException : InvalidExpressionException
    {
        public ExpressionContainsExternalReferencesException(string message) : base(message)
        {
        }
    }
}
