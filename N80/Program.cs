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

            var code =
@"
    org 1
foo::
foo:

dseg ;1
  dseg;1
  dseg ,1

    org 1

ñokis::

    public çaço

    BAR:
    BAR:
    db EXT##


   ; Foo
  BLANK_NO_LABEL:  
  COMMENT_LABEL:  ;Bar
  PUBLIC::
DEBE: defb 34
    INVA-LID:

EXTRN EXT2

  db 1, 2+2 ,,FOO*5, 'Hola', EXT##, BAR+2, FOO*7, EXT2

    org

    dseg ,TAL
DSEG1: db 0
    ;org 10 , cual
DSEG2: db 1
    org 1
    org DSEG3
DSEG3:
";

            var config = new AssemblyConfiguration() {
                DefaultProgramName = "SOURCE",
                Print = (s) => Debug.WriteLine(s),
                MaxLineLength = 2000
            };

            var result = AssemblySourceProcessor.Assemble(code, config);
        }
    }
}