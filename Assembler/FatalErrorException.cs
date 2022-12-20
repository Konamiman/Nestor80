namespace Konamiman.Nestor80.Assembler
{
    internal class FatalErrorException : Exception
    {
        public FatalErrorException(AssemblyError error)
        {
            this.Error = error;
        }

        public AssemblyError Error { get; }
    }
}
