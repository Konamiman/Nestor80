# SDCC relocatable file format support

Nestor80 can create relocatable files that conform to the format generated by the SDAS assembler and interpreted by the SDLS linker. These tools are used internally by [the SDCC compiler](https://sdcc.sourceforge.net/), thus Nestor80 can be used to build relocatable files that can be be combined with C code to produce a full executable program (there's an example of this at the end of this document).

["Writing relocatable code"](WritingRelocatableCode.md) explains generic concepts about what "relocatable code" is and how linkers convert it into full programs. This document contains specific information regarding the SDCC file format and how Nestor80 supports it.

💡 The MACRO-80 relocatable file format is in general more flexible than the SDCC relocatable file format. When producing relocatable files it's recomended to target the MACRO-80 file format unless you need compatiblity with SDCC or you want to use multiple named code areas.


## File format

SDCC relocatable files are text files where program bytes, labels, addresses and relocation information pieces are encoded in (more or less) human-readable form. The exact format is detailed in [the SDCC linker documentation](asmlnk.txt) (search a section titled "Object Module Format"); of course you can also open any of these files in a regular text editor if you want to see how they look like.

Nestor80 always generates the SDCC relocatable files conforming to the "XL3" format specifier (hexadecimal numbers, low-endian for multi-byte values, 24-bit addressing for byte values) which is the same format of the relocatable files generated by SDAS.

The file encoding of the SDCC relocatable files generated by Nestor80 is UTF-8 without byte order mark (this is required by SDAS). Lines are terminated with the system default end of line sequence unless a different one is specified by passing an `--end-of-line` argument to Nestor80.


## Areas

When building a MACRO-80 relocatable file your code can be in one of the `ASEG`, `CSEG` or `DSEG` area, or in a `COMMON` block, as explained in ["Relocatable file format"](WritingRelocatableCode.md#memory-segments).

When building a SDCC relocatable file, however, the code lives in named areas, each of which can be defined as absolute or relocatable and concatenable or overlay; see [`AREA`](LanguageReference.md#area-area-) for details. An area named `_CODE` (of type relocatable and concatenable) always exists, and additional areas can be defined as needed.


## Limitations in instructions

The following instructions aren't allowed or have limitations when building a SDCC relocatable file:

* [`ASEG`](LanguageReference.md#aseg-), [`CSEG`](LanguageReference.md#cseg-), [`DSEG`](LanguageReference.md#dseg-), [`COMMON`](LanguageReference.md#common-) and [`.REQUEST`](LanguageReference.md#request-) can't be used: they are replaced by [`AREA`](LanguageReference.md#area-area-).
* [`.REQUEST`](LanguageReference.md#request-) can't be used and there isn't any equivalent instruction.
* [`DEFS`](LanguageReference.md#defs-ds): the `<value>` argument can't be used, and the Nestor80 argument `--initialize-defs` won't have any effect.
* [`ORG`](LanguageReference.md#org) can be used only inside absolute areas.


## Limitations in relocatable expressions

While the MACRO-80 relocatable file format is prepared to accommodate relocatable expressions (those that contain relocatable symbols or external symbol references, and thus must be evaluated at linking time) that contain any kind of arithmetic operators, that's not the case of the SDCC relocatable file format. This format is simpler in that regard, and it limits what can be included in these expressions to the following list of items:

* Only one relocatable or external symbol.
* Numeric constants or references to absolute symbols.
* Binary addition and substraction operators.
* Unary minus operators, as long as they apply to numeric constants or absolute symbols only.
* One single `HIGH` or `LOW` operator, as long as it applies to the entirety of the rest of the expression.

So in other words, if `symbol` is a relocatable or external symbol and `number` is a numeric constant or an absolute symbol (or a combination of those, added and substracted together), then the following expressions are allowed:

```
symbol
symbol+number
symbol-number
HIGH(symbol)
HIGH(symbol+number)
HIGH(symbol-number)
LOW(symbol)
LOW(symbol+number)
LOW(symbol-number)
```

Also equivalent expressions like `symbol+number-number`, `-number+symbol+number`, etc.

See ["Expressions"](LanguageReference.md#expressions) for more details about the Nestor80 support for expressions in assembler code.


## A simple example

Here we'll see an example of how to generate a standard binary file for [MSX](https://en.wikipedia.org/wiki/MSX) computers using Nestor80 and [SDCC](https://sdcc.sourceforge.net/) 4.0 or newer. These files can be loaded from the [MSX-BASIC](https://github.com/Konamiman/MSX2-Technical-Handbook/blob/master/md/Chapter2.md) environment using the `BLOAD"file"` command, or loaded and executed with the `BLOAD"file",R` command.

The structure of such binary files is as follows:

```
  ;header:
  db 0xFE
  dw start_address
  dw end_address
  dw execution_address

  ;code
```

When a `BLOAD` command is executed MSX-BASIC will load the first `end_address-start_address` bytes from the file (not including the header itself) at `start_address`, and if the `,R` switch is appended to the command, it will jump to `execution_address`.

The first step in our example is writing the code for the header, let's put the following in a file named `crt0_msx_basic.asm`:

```
  area _HEADER (ABS)

start: equ 0xC000

  org start-7

  db 0xFE
  dw init
  dw end
  dw init

init:
  call gsinit
  jp _main##

  area _GSINIT
gsinit:
  ld bc,l__INITIALIZER##
  ld a,b
  or c
  ret z
  ld de, s__INITIALIZED##
  ld hl, s__INITIALIZER##
  ldir
  ret

  ; End of the file

  area _END
end:
```

There are a few things to explain here:

* "crt0" is the standard SDCC terminology to designate header files that are platform-dependant and whose content always goes before any other code or data in the compiled programs.
* `_HEADER` is the name of a relocatable code area that SDCC defines. Any code in this area will be placed before anything else in the program.
* `_GSINIT` is also an area defined by SDCC, the code here is expected to initialize the global variables defined in the C code; here "initialization" means copying them from the `_INITIALIZER` area (which could be in ROM) to the `_INITIALIZED` area (which is expected to be in RAM). There's also a `_GSFINAL` area, but we don't need to reference it here.
* SDCC will link any areas not defined by itself after the other areas, so at the end of the resulting file; and that's exactly what (very conveniently) will happen with the `_END` area.
* The SDCC linker defines two symbols for each area defined: `s__NAME`, whose value is the actual starting address of the area; and `l__NAME`, whose value is the size of the area.
* We need to choose the memory area where the file will be loaded and executed, here we randomly chose 0xC000 (this address contains RAM in the MSX-BASIC environment).

This is the command to assemble this file with Nestor80, the resulting file will be `crt0_msx_basic.REL`:

```
N80 crt0_msx_basic.asm
```

Nestor80 will automatically set the build type to SDCC relocatable thanks to `area _HEADER (ABS)` being the first line of the code, but you could also have added `--built-type sdcc` to the Nestor80 command line.

Now we'll create a file named `print.asm` with the following content:

```
;MSX-BIOS routine to print one character
;Input: A = character
;Modifies no registers
CHPUT: equ 0x00A2

  area _CODE

;void print(char* text)
_print::
  ld a,(hl)
  or a
  ret z
  call CHPUT
  inc hl
  jr _print
```

Again, assemble it with `N80 print.asm` to get a `print.REL` file. Worth noting:

* By convention, SDCC will translate C function names to public assembler symbols whose name is the function name with a `_` prefix. Thus this funcion will be visible with the name `print` from C code.
* In the new register-based calling convention used by SDCC a single two-byte value will be passed to functions in the HL register, thus the C signature for this method (also considering that it doesn't return any value) will be `void print(char* text)`.

Now it's finally time to write some C code! Create a file named `hello.c` with the following content:

```C
// Normally this would go in a "print.h" file.
void print(char* text);

// This (or rather, a pointer to this) is what gets copied
// from _INITIALIZER to _INITIALIZED.
char* message = "Hello!";

void main() {
    print(message);
}
```

...and run SDCC like this:

```
sdcc --code-loc 0xC006 --data-loc 0 -mz80 --no-std-crt0 crt0_msx_basic.REL print.REL hello.c
```

Things to note:

* `--code-loc` tells SDCC the address where the code beyond the crt0 file (so the one coming from `print.REL` and `hello.c` itself) will be located. This code will go right after the code in the `_HEADER` area, which starts at 0xC000 and is just 6 bytes long (`call gsinit` and `jp _main##`); so we use 0xC006.
* `--data-loc 0` instructs SDCC to put the data for global variables right after the code. Without this SDCC will create a 16KByte file with the data at the end.
* With `--no-std-crt0` we tell SDCC to not prefix the code with the standard header for Z80 programs (which assumes that a CP/M program is being compiled).
* Relocatable files specified before the C file to compile will cause the linker to process these files, in the same order, before the file resulting from compiling the C code.

This will have created a `hello.ihx` file in [Intel HEX format](https://en.wikipedia.org/wiki/Intel_HEX) that we need to convert to binary format. If you are on Linux (including [WSL](https://learn.microsoft.com/en-us/windows/wsl/about)) you can use `objcopy` (part of the [`binutils`](https://en.wikipedia.org/wiki/GNU_Binutils) package) like this:

```
objcopy -I ihex -O binary hello.ihx hello.bin
```

On Windows you can use [hex2bin](https://hex2bin.sourceforge.net/):

```
hex2bin -e bin hello.ihx
```

And that's it: the resulting file, `hello.bin`, will print a "Hello!" message when executed in a MSX-BASIC environment with `BLOAD"hello.bin",R`.


## To summarize...

The important concept to learn here is that you can use Nestor80 to create reusable relocatable files (like the `print.REL` in the example) that you can integrate with your C programs. This is good because Nestor80 has powerful features that aren't available in SDAS (like phased blocks, custom error messages, proper character encodings for strings, modules and relative labels) and uses a familiar syntax for Z80 code, e.g. `(IX+n)` instead of SDAS' `n(IX)`.

See [the SDCC user guide](https://sdcc.sourceforge.net/doc/sdccman.pdf) for more information on how to compile C code in general and how to write reusable relocatable files in particular.
