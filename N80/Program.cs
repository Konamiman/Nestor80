using Konamiman.Nestor80.Assembler;
using System.Diagnostics;
using System.Text;

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
x:
dseg
y:
cseg
ld e,type x+y
ld a,foo##+34
ld b,type foo##
ld c,type 34+foo##
ld d,type x
end

ld a,foobarcios1##
ld b,foobarcios2##
db 0
foobarcios3:
db 1
foobarcios4::
end

db 0
if1
db 1
foo:
else
foo:
endif
jp foo
end

FOO equ 1
db 34
db FOO
db BAR
db FIZZ##
BAR equ 2
end

ds 16
jp fizz
foo:
.phase 100h
jp bar
bar:
db 0
jp fizz
.dephase
fizz:
end

dw 1+(BAR## SHL 8)
end

db 1+FOO-BAR*FIZZ##
FOO equ 1
BAR:
end

ld a,12h
jp 1234h

FOO:
ld a,FOO
jp FOO
end

db FOO+BAR
end

foo equ 12h
db foo,bar,fizz+1
fizz equ 5566h
bar equ 77h
end

db 0
db 0
include foo/bar.asm
db 1
foocios equ 1
ld a,barcios
.warn Warn in main code
end

.print {2+2}
.warn ;oo {1+1}
.error {3+3}
.fatal {4+4}

.print hello!
.warn Oops, some warning
.error Yikes, some error
.fatal Aaargh, fatal!!

db 0
  if2
    db 1
    if1
      db 2
    else
      db 3
    endif
    db 4
  else
    db 5
  endif
db 6
end

ifdif <a>,<A>
db 0
endif

ifdif <a>,<a>
db 0
endif
end

ifidn <a>,<a>
db 0
endif

ifidn <a>,<>
db 1
endif

ifidn <a>,<A>
db 1
endif

ifidn <a>
endif

ifidn <a>,
endif

ifidn <a>,<a
endif

end

ifb <>
db 0
endif

ifb < >
db 1
endif

ifnb <>
db 2
endif

ifnb < >
db 2
endif

ifb
endif

ifb <
endif

end

iff 0
db 34
endif
foo equ 1
bar:
extrn fizz
ifdef foo
db 0
endif
ifdef bar
db 1
endif
ifdef fizz
db 2
endif
ifndef NOPE
db 3
endif
end

db 0
  if 0
    db 1
    if 1
      db 2
    else
      db 3
    endif
    db 4
  else
    db 5
  endif
db 6
end

if 1
db 1
bar:
foo: else
fizz: db 2
buzz: endif
end

CSEG
end

;public FOO
;end

;extrn FOO
;end

;ld a,(FOO##)
;end

FOO::
end

ds 1
end

db 0
end

dseg
foo:
end

org 100h
foo:
end

.cpu r800
mulub
.z80
mulub a,b
ex af,af
ex af,af'
end

ds 0F0h
foo:
db 0
foo2:
resete equ 18h
jp (hl)
jp 34
jp foo
jp bar
ld a,34
ld a,foo
ld a,bar
rst 18h
rst resete
rst bar
ld a,(ix+34)
ld a,(ix+foo)
ld a,(ix+bar)
ld (ix+34),a
ld (ix+foo),a
ld (ix+bar),a
ld (ix+34),89
ld (ix+34),foo
ld (ix+34),bar
ld (ix+foo),34
ld (ix+foo),bar
ld (ix+bar),34
ld (ix+bar),foo
ld (ix+bar),fizz
ld (ix+foo),foo2
bar:
fizz::
end


ld (ix+1),bar
end


bit 7,a
end

ld a,(34)
end

jr c,34
end


inc (ix+255)
end

jp FOO
FOO:
jp FOO
end

jp (hl)
;inc hl
hl equ 1
jp (hl)

ret

aseg
;ds 16
;jr 0
foo:
;jr foo
jr bar
bar:
end

;inc a
;inc hl
;dec (hl)
;rst 18h
;inc (ix+34)
;dec (ix+130)
jp 1234h



BC:
a equ 1

end
nop foo
ret
end

FOO equ 12abh
BAR equ 7
.print ola ke ase
.print1 Esto es FOO: {fizz} {foo+1:b0}, {foo:b-1} {foo+1:d8}, {foo+1:b}, {foo+1:B20}, {foo+1:h}, {foo+1:H}, {foo+1:h7} en default
.print2 Esto es BAR: {bar+1} en default

end

.LIST
.XLIST
.TFCOND
.SFCOND
.LFCOND
end

.request foo,bar,,@fizz,á,
end

dz ""ABC""
.strenc utf-16
defz ""ABC""
.strenc ascii
defz ""ABC""
end


db 1
FOO:
BAR equ 2
extrn FIZZ
end

.comment \
title defl 1
title Hola ke ase
foo: title defl title+1
bar: title Hola ke ase mas
page 50
fizz: page 70
page equ 80
buzz: page equ 90
end


FOO equ 1234h
BAR equ 7
.print1 Esto es FOO: {foo+1} en default
.print1 Esto es BAR: {bar+1} en default
.print1 Esto es FOO: {d:foo+1} en dec
.print1 Esto es FOO: {D:foo+1} en DEC
.print1 Esto es BAR: {d:bar+1} en dec
.print1 Esto es BAR: {D:bar+1} en DEC
.print1 Esto es FOO: {h:foo+1} en hex
.print1 Esto es FOO: {H:foo+1} en HEX
.print1 Esto es BAR: {h:bar+1} en hex
.print1 Esto es BAR: {H:bar+1} en HEX
.print1 Esto es FOO: {b:foo+1} en bin
.print1 Esto es FOO: {B:foo+1} en BIN
.print1 Esto es BAR: {b:bar+1} en bin
.print1 Esto es BAR: {B:bar+1} en BIN
end

.printx
.printx Ola ke ase
.printx /mola/
.printx /bueno, me delimito/ y tal
  .printx /a/
  .printx //
end

\
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
                OutputStringEncoding = "ascii",
                AllowEscapesInStrings = true,
                //BuildType = BuildType.Absolute,
                GetStreamForInclude = (name) => {
                    string code;
                    if(name == "foo/bar.asm") {
                        code = "db 34\r\n.warn Warn in include 1\r\ninclude bar/fizz.asm\r\ndb 89\r\n";
                        //code = "include foo/bar.asm\r\n";
                    }
                    else {
                        code = ".warn Warn in include 2\r\ndw 2324h\r\n";
                    }
                    return new MemoryStream(Encoding.ASCII.GetBytes(code));
                }
            };

            AssemblySourceProcessor.PrintMessage += AssemblySourceProcessor_PrintMessage;
            AssemblySourceProcessor.AssemblyErrorGenerated += AssemblySourceProcessor_AssemblyErrorGenerated;
            AssemblySourceProcessor.BuildTypeAutomaticallySelected += AssemblySourceProcessor_BuildTypeAutomaticallySelected;

            var result = AssemblySourceProcessor.Assemble(code, config);
            //var result = AssemblySourceProcessor.Assemble(sourceStream, Encoding.GetEncoding("iso-8859-1"), config);
        }

        private static void AssemblySourceProcessor_BuildTypeAutomaticallySelected(object? sender, (int, BuildType) e)
        {
            Debug.WriteLine($"In line {e.Item1} build type was automatically selected as {e.Item2.ToString().ToUpper()}");
        }

        private static void AssemblySourceProcessor_AssemblyErrorGenerated(object? sender, Assembler.Output.AssemblyError e)
        {
            Debug.WriteLine(e.ToString());
        }

        private static void AssemblySourceProcessor_PrintMessage(object? sender, string e)
        {
            Debug.WriteLine(e);
        }
    }
}