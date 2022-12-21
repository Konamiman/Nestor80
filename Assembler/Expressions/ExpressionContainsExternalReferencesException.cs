namespace Konamiman.Nestor80.Assembler.Expressions
{
    internal class ExpressionContainsExternalReferencesException : InvalidExpressionException
    {
        public ExpressionContainsExternalReferencesException(string message) : base(message)
        {
        }
    }
}
