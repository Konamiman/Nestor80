using Konamiman.Nestor80.Assembler;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Konamiman.Nestor80.Assembler.Expressions;

namespace Konamiman.Nestor80.N80
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            //var x = Encoding.GetEncodings().Select(e => new { e.CodePage, e.Name }).OrderBy(x=>x.CodePage).ToArray();

            //var sourceFileName = Path.Combine(Assembly.GetExecutingAssembly().Location, @"../../../../../SOURCE.MAC");
            //var sourceFileName = @"L:\home\konamiman\Nestor80\COMMENTS.MAC";
            //var sourceStream = new FileStream(sourceFileName, FileMode.Open, FileAccess.Read);

            var code =
@"
title
title ;
title Foo, bar; fizz, buzz
subttl
subttl ;
subttl eso mismo; digo yo
$title
$title('
$title('')
$title ('')
$title('mola mucho') ; y tanto
$title ('mola mucho') ; y tanto
$title('molamil')
page
page break
page foo
page 9
page 1234h
end


db +
db 2+
db 2 eq
db 2+(
end

name(
name('foo')
name('bar') ;bar!
 name ('fizz')
 name ('buzz') ;buzz!
name
name('')
name('1')

end

.z80
.cpu z80
;.8080
.cpu unknownx

db 0
end

db 0,0,0
foo:
.radix foo
.radix bar
.radix 1
.radix 17
.radix 1+1
db 1010
.radix 17-1
db 80
bar equ 3
end

ds
ds foo
ds 34
ds 89,bar
ds 12,99
ds 44,1234
ds ""AB""
end


dc
dc 1
dc 'ABC'+1
dc 'ABC',
dc 'ABC'
.strenc 850
dc 'áBC'
dc )(

end

db
dw
dw 'AB', ""CD"", 1, 1234h, '', ""\r\n""
end

db ""\r\n""
db '\r\n'

.stresc
.stresc pepe
.stresc off
db ""\r\n""
.stresc on
db ""\r\n""


end

.strenc

.strenc 28591
db 'á'
.strenc iso-8859-1
db 'á'
.strenc 850
db 'á'
.strenc ibm850
db 'á'
.strenc default
db 'á'
end

public foo
extrn foo
end

db foo

FOO defl 2
db foo
foo defl foo+1
db foo
foo aset foo+1
db foo
foo set foo+1
db foo

end

db 1

FOO: .COMMENT abc

db 2
xxx
;Mooola
ddd
xxxaxxx

db 3
end

db foo
public foo
foo:

db bar
extrn bar

end

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
                MaxLineLength = 2000,
                OutputStringEncoding = "ascii",
                AllowEscapesInStrings = true,
            };

            var result = AssemblySourceProcessor.Assemble(code, config);
            //var result = AssemblySourceProcessor.Assemble(sourceStream, Encoding.GetEncoding("iso-8859-1"), config);
        }
    }
}