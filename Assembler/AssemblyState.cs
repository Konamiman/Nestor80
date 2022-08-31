using System.Text;
using Konamiman.Nestor80.Assembler.Output;

namespace Konamiman.Nestor80.Assembler
{
    internal class AssemblyState
    {
        private List<AssemblyError> Errors;

        public AssemblyConfiguration Configuration { get; init; }

        public Encoding SourceStreamEncoding { get; init; }

        public bool InPass2 { get; private set; }

        public bool InPass1 => !InPass2;

        public bool HasErrors => Errors.Any(e => !e.IsWarning);

        public int CurrentLineNumber { get; private set; } = 1;

        public AssemblyState()
        {
            InPass2 = false;
            Errors = new List<AssemblyError>();
        }

        public void SwitchToPass2()
        {
            InPass2 = true;
            CurrentLineNumber = 1;
        }

        public void IncreaseLineNumber()
        {
            CurrentLineNumber++;
        }

        public void AddError(AssemblyError error)
        {
            Errors.Add(error);
        }

        public void AddError(AssemblyErrorCode code, string message, bool withLineNumber = true)
        {
            AddError(new AssemblyError(code, message, withLineNumber ? CurrentLineNumber : null ));
        }

        public AssemblyError[] GetErrors() => Errors.ToArray();
    }
}
