using System.Text;

namespace Konamiman.Nestor80.Assembler
{
    public partial class Assembler
    {
        private AssemblyConfiguration configuration;

        private AssemblyState state;

        public Assembler(AssemblyConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public AssemblyError[] Assemble()
        {
            try {
                state = new AssemblyState { Configuration = configuration };

                DoPass1();
                if(!state.HasErrors) {
                    state.SwitchToPass2();
                    DoPass2();
                }
            }
            catch(FatalErrorException ex) {
                state.AddError(ex.Error);
            }
            catch(Exception ex) {
                state.AddError(
                    code: AssemblyErrorCode.UnexpectedError,
                    message: $"Unexpected error: ({ex.GetType().Name}) {ex.Message}"
                );
            }
            return state.GetErrors();
        }

        private void DoPass2()
        {
            throw new NotImplementedException();
        }

        private void DoPass1()
        {
            var sourceStream = new StreamReader(configuration.SourceStream, configuration.SourceStreamEncoding, true, 4096);

            var lines = new List<string>();
            int lineLength;

            while(true) {
                var sourceLine = sourceStream.ReadLine();
                if(sourceLine == null) break;
                if((lineLength = sourceLine.Length) > configuration.MaxLineLength) {
                    sourceLine = sourceLine.Substring(0, configuration.MaxLineLength);
                    state.AddError(
                        AssemblyErrorCode.SourceLineTooLong,
                        $"Line is too long ({lineLength} bytes), actual line processed: {sourceLine.Trim()}"
                    );
                }
                lines.Add(sourceLine); //TODO: Process line
                state.IncreaseLineNumber();
            }
        }
    }
}
