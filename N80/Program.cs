using Konamiman.Nestor80.Assembler;
using System.Diagnostics;
using System.Reflection;

namespace Konamiman.Nestor80.N80
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var sourceFileName = Path.Combine(Assembly.GetExecutingAssembly().Location, @"../../../../../SOURCE.MAC");
            var sourceStream = new FileStream(sourceFileName, FileMode.Open, FileAccess.Read);

            /*
            var outputFileName = Path.Combine(Assembly.GetExecutingAssembly().Location, @"../../../../../SOURCE.REL");
            var ourputStream = new FileStream(outputFileName, FileMode.Create, FileAccess.Write);
            */

            var code =
@"
    BAR:
    BAR:


   ; Foo
  BLANK_NO_LABEL:  
  COMMENT_LABEL:  ;Bar
  PUBLIC::
DEBE: db 34
    INVA-LID:

  db 1, 2+2 ,,FOO*5, 'Hola', BAR+2,
";

            var config = new AssemblyConfiguration() {
                DefaultProgramName = "SOURCE",
                Print = (s) => Debug.WriteLine(s),
                MaxLineLength = 2000
            };

            var result = AssemblySourceProcessor.Assemble(code, config);

            /*
            var assembler = new AssemblySourceProcessor(config);
            var result = assembler.Assemble();
            */
        }
    }
}