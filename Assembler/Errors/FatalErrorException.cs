namespace Konamiman.Nestor80.Assembler.Errors
{
    /// <summary>
    /// When a fatal error is found in code we need to stop the source processing
    /// immediately, for that this dedicated exception is thrown.
    /// </summary>
    internal class FatalErrorException : Exception
    {
        public FatalErrorException(AssemblyError error)
        {
            Error = error;
        }

        public AssemblyError Error { get; }
    }
}
