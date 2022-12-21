using Konamiman.Nestor80.Assembler.Errors;

namespace Konamiman.Nestor80.Assembler.Expressions
{
    internal class InvalidExpressionException : Exception
    {
        public InvalidExpressionException(string message, AssemblyErrorCode errorCode = AssemblyErrorCode.InvalidExpression) : base(message)
        {
            ErrorCode = errorCode;
        }

        public AssemblyErrorCode ErrorCode { get; }
    }
}
