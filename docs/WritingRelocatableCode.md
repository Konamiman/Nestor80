# Writing relocatable code

Nestor80 allows to write [absolute and relocatable code](LanguageReference.md#absolute-and-relocatable-code). This document explains how the process of writing and linking relocatable code works.

Writing relocatable code involves using the LINK-80 tool and optionally the LIB-80 tool additionally to Nestor80, thus you might also want to take a look at:

* [The original MACRO-80, LINK-80 and LIB-80 user manual](MACRO-80.txt)
* [The M80dotNet project](https://github.com/konamiman/M80dotNet), a convenient way to use LINK-80 and LIB-80 in modern machines (same system requirements as Nestor80).

This document uses [the same conventions as the Nestor80 language reference guide](LanguageReference.md#document-conventions).


## The basics

For programs of low and moderate complexity that don't have dependencies on libraries or other code sharing mechanisms, assembling in absolute mode is usually appropriate. However for more complex projects you may find yourself in the need of:

1. Dividing your source code in smaller files, assembling them independently (possibly using a [makefile](https://en.wikipedia.org/wiki/Make_(software))) and then combining the result into a final binary (absolute) file. See for example [the makefile for Nextor](https://github.com/Konamiman/Nextor/blob/v2.1/source/kernel/Makefile)).

2. Creating reusable code libraries and incorporating them in your programs by using [static linking](https://en.wikipedia.org/wiki/Static_library).

In both cases the final memory addresses in which each program part will end up being loaded are unknown at assembly time and decided at linking time. That's why each program part needs to be assembled as _relocatable_.

> A relocatable file is a "pre-assembled" file following [a special format](RelocatableFileFormat.md) in which all the references to internal program addresses are stored as values that are relative to the starting memory address for the program. A relocatable file can optionally declare public [symbols](LanguageReference.md#symbols) and contain references to external symbols.

A _public symbol_ is a symbol whose value (relative or absolute) is exposed by the program, and an _external reference_ is a reference to a symbol that needs to be supplied by a different program (which declares it as a public symbol). The linking process solves the "puzzle" by matching public symbols with external references from all the involved programs, and finally "relocating" all the code into their final absolute addresses, thus generating the final absolute binary file.

Thus in a relocatable code we can find three types of items:

1. **Absolute bytes:** these need to be included verbatim, without any changes, in the final linked file. That's the case of CPU instruction opcodes, program data, and fixed memory address references (e.g. for BIOS entry points).
2. **Relocatable addresses:** references to addresses within the program, whose final value won't be known until the linking process calculates them.
3. **Link items:** these are used to store other structured types of data to be used during the linking process, for example public symbol declarations and external symbol references.


### Memory segments

All the code in a relocatable file lives in a _segment_ or logical memory area. The following segments are defined:

* The absolute segment
* The code segment
* The data segment
* Named COMMON blocks

⚠ Despite the "code" and "data" names, these segments can contain any kind of content: code, data or both; these names are pretty much just an arbitrary convention.

During the assembly process there's always one of these segments that is considered the _active segment_ (it's the code segment at the beginning of the process) which is where the assembled code and data is assigned. The active segment can be changed with the [`ASEG`](LanguageReference.md#aseg-), [`CSEG`](LanguageReference.md#cseg-), [`DSEG`](LanguageReference.md#dseg-) and [`COMMON`](LanguageReference.md#common-) instructions.

The absolute segment isn't actually relocatable: it's used for cases in which code must be assembled at a fixed memory address, bypassing the relocation process performed at linking time.

💡 COMMON blocks exist for compatibility with MACRO-80, which in turn pretty much supported them for compatibility with the (at the time) popular languages Cobol and Fortran. For Z80 assembly code you'll usually only use the code and data segments.

At linking time all the code for each segment in each program is combined together; thus all the content intended for the code segment is combined, the same for the data segment, and the same for each COMMON block.


## A simple example

Let's start with a simple program, `SIMPLE.ASM`, that just prints a "Hello!" message in [MSX-DOS](https://www.msx.org/wiki/Category:MSX-DOS):

```
.CONOUT: equ 0002h
DOS: equ 0005h

  cseg ;Not actually needed
       ;(assembly starts with the code segment active)

  ld hl,HELLO
  call PRINT
  ret

PRINT:
  ld a,(hl)
  or a
  ret z
  push hl
  ld e,a
  ld c,.CONOUT
  call DOS
  pop hl
  inc hl
  jp PRINT

  dseg

HELLO: db "Hello!\0"
```

This is a pseudocode representation of the `SIMPLE.REL` file that's generated by running `N80 SIMPLE.ASM` (take a look at [the exact file format](RelocatableFileFormat.md) if you are curious):

```
<switch to code segment>

ld hl,<data segment relative 0000h>
call <code segment relative 0007h>
ret

ld a,(hl)
or a
ret z
push hl
ld e,a
ld c,0002h
call 0005h
pop hl
inc hl
jp <code segment relative 0007h>

<switch to data segment>

db 48h,65h,6Ch,6Ch,6Fh,21h,00h
```

Interesting things to note:

* The entire program is assigned to the code segment, while the string to be printed is assigned to the data segment. This is the usual thing to do in relocatable pograms but it's not mandatory (either segment can contain both code and/or data).
* The `.CONOUT` and `DOS` constants disappear since they are local symbols (not defined as public), and their usages get replaced with their absolute values (since they are not relocatable address references).
* The `PRINT` and `HELLO` labels also disappear because they are non-public symbols, but this time they get replaced by the relative values resulting from assuming that the code and data segment, respectively, start at address 0 (if that program was assembled as absolute code with `ORG 0`, the `PRINT` label would have the value 0007h).

Now it's linking time. We run LINK-80 like this:

```
L80 /p:0100,/d:0120,SIMPLE,SIMPLE/N/E
```

You can refer to [the original MACRO-80, LINK-80 and LIB-80 user manual](MACRO-80.txt) for the command line syntax, but what this command means is "open SIMPLE.REL and link it assuming that code segment starts at address 0100h and data segment starts at address 0120h".

This will generate a `SIMPLE.COM` file that is equivalent to the following absolute program:

```
org 0100h

ld hl,0120h
call 0107h
ret

ld a,(hl)
or a
ret z
push hl
ld e,a
ld c,0002h
call 0005h
pop hl
inc hl
jp 0107h

ds 0120h-$,0

db 48h,65h,6Ch,6Ch,6Fh,21h,00h
```

Notice how:

* References to `PRINT` get now the absolute value 0107h (start of code segment + relative symbol value) and similarly, references to `HELLO` get the absolute value 0120h.
* There's a gap between the end of the code segment content and the start of the data segment, the linker fills it with zeros.

⚠ An annoying limitation of LINK-80 is that it assumes that the linked program will be a CP/M executable and thus the starting memory address for the generated absolute program file is fixed to 0100h (so e.g. if you had passed `/p:0105` to LINK-80 in the example above, it would have appended 5 zero bytes of the start of the file) and can't be changed. To overcome this limitation you can use the `/X` switch to tell LINK-80 to generate an [Intel HEX file](https://en.wikipedia.org/wiki/Intel_HEX) instead of a binary file, but then you need an extra tool like [hex2bin](https://github.com/Keidan/hex2bin) to convert it to the final binary file.


### Using the absolute segment

To illustrate the usage of the absolute segment let's do a small change to our `SIMPLE.ASM` program. Replace the `dseg` line with the following:

```
aseg
org 0120h
```

Then assemble it with `N80 SIMPLE.ASM`, but the linker command line is `L80 /p:0100,SIMPLE,SIMPLE/N/E` this time (since the data segment isn't being used now). The generated `SIMPLE.COM` file is equivalent to the one from the previous example.


## Example with public and external symbols

The above example was intentionally simple to help demonstrating the relevant concepts, but in real life it doesn't make much sense to write one such a simple single program as relocatable. The real value of relocatable coding comes when combining two or more programs that declare public and external symbols.

Let's start by converting the `PRINT` routine into a reusable `PRINT.ASM` file as follows:

```
.CONOUT: equ 0002h
DOS: equ 0005h

  cseg

public PRINT

PRINT:
  ld a,(hl)
  or a
  ret z
  push hl
  ld e,a
  ld c,.CONOUT
  call DOS
  pop hl
  inc hl
  jp PRINT
```

We assemble it with `N80 PRINT.ASM` and you get a `PRINT.REL` that is very similar to the above `SIMPLE.REL`, minus the printed string and with an extra link item that says "I'm exposing a public PRINT symbol whose value is &lt;code segment relative 0000h&gt;".

Now we create the actual program, `PROG.ASM`, like this:

```
  cseg

  extrn PRINT

PROGRAM:
  ld hl,HELLO
  call PRINT
  ret

  dseg

HELLO: db "Hello!\0"
```

We assemble it with `N80 PROGR.ASM`. This time the generated `PROG.REL` file gets the `call PRINT` line converted to a link item that is equivalent to `call <reference to external symbol PRINT>`.

With both relocatable files at hand we trigger the linking process with:

```
L80 /p:0100,/d:0120,PROG,PRINT,PROG/N/E
```

LINK-80 "connects the dots" and matches the public `PRINT` symbol exposed by `PRINT.REL` with the external reference of the same name in `PROG.REL`. The final program file generated, `PROG.COM`, is identical to the `SIMPLE.COM` of the first example.


## Segment start addresses with multiple programs

You may have noticed something that in principle seems weird regarding how relocatable addresses are linked. In the previous example, both the `PRINT` label from `PRINT.ASM` and the `PROGRAM` label from `PROG.ASM` refer to "code segment relative 0000h" address in their respective relocatable files. Then how can the linking process work? Wouldn't both resolve to address 0100h and thus one would overwrite the other?

The answer is that what the linker actually does is to treat the relocatable addresses in each of the involved programs (each of the `.REL` files passed to the LINK-80 command line) as **relative to the final start address of each program**. This will be the same as the address passed with `/p` or `/d` to LINK-80 only for the first of the programs.

Let's see a simple example that also illustrates the fact that the `ORG` instruction refers to relative addresses, not absolute addresses, when used in relocatable files; for simplicty the example involves only the code segment this time but the same concept applies to the data segment as well. Assume we have these two files:

```
;SUM.ASM

  cseg

  org 10h

  public SUM

SUM:
  add a,b
  ret
```

```
;PROG.ASM

  cseg

  org 10h

  extrn SUM
PROGRAM:
  ld a,1
  ld b,2
  call SUM
  ret
```

When both programs are assembled and linked with `L80 /p:0100,PROG,SUM,PROG/N/E`, the resulting `PROG.COM` file is equivalent to this:

```
org 100h

ds 10h,0

ld a,1
ld b,2
call 0128h
ret

;PC = 0118h here

ds 10h,0

add a,b
ret
```

`PROGRAM` gets resolved to address 0110h in the final program, and given that the size of the code in `PROG.ASM` is 8 bytes, `SUM` gets resolved to 0128h (the base address for the code segment is 0118h when the linker starts procesing `SUM.REL`).


## Defaults for code and data segment locations

Both the `/p` and `/d` LINK-80 command line switches are optional. This is how the initial addresses of the code and data segments are decided when one or both of these switches are missing:

* No `/p` and no `/d`: data segment starts at 0103h, code segment is placed immediately _after_ the data segment.
* Only `/d` present: code segment starts at 0103h.
* Only `/p` present: data segment is placed immediately _before_ the code segment.

In the first two cases the actual content starts at 0103h so that a jump to the actual program start address can be put at 0100h (the entry point address of CP/M programs).

💡 Worth noting too that `/p` and `/d` can be used multiple times in one single linking process. For example `/p:0100,PROG1,PROG2,/p:4000h,PROG3` will link `PROG1.REL` at address 0100h, `PROG2.REL` immediately after that, and `PROG3.REL` at address 4000h (the space between the end of `PROG2` and the start of `PROG3` will be filled with zeros).


## Location of COMMON blocks

You will have noticed that there's no LINK-80 command line switch to indicate the location of COMMON blocks. These are always placed before the data segment, in the order in which they appear in the source code. For example:

```
;COMMONS.ASM

dseg

db 7,8,9

common /FOO/

db 1,2,3

common /BAR/

db 4,5,6
```

When linked with `L80 /d:100,COMMONS,COMMONS/N/E` this will generate a `COMMONS.COM` file that contains the byte sequence 1,2,3,4,5,6,7,8,9.


## Combining programs into libraries

The LIB-80 tool can be used to combine multiple relocatable files into one single library files, which can then be used with LINK-80 instead of the individual relocatable files.

For example, assume that you have a collection of mathematical routines, each in its own file like `SUM.REL`, `MULT.REL` and `DIV.REL`. You may use them with LINK-80 like this:

```
L80 PROG,SUM,MULT,DIV,PROG/N/E
```

Instead, you can use LIB-80 to combine the routines into a single `MATH.REL` library like this:

```
LIB80 MATH=SUM,MULT,DIV/E
```

...and then use it directly in LINK-80:

```
L80 PROG,MATH,PROG/N/E
```

⚠ You may think that LINK-80 will only take the required programs from the library, as it happens when using libraries in other languages like C; for example if `PROG` only uses the `MULT` routine then the contents of `SUM.REL` and `DIV.REL` wouldn't be included in the generated `PROG.COM`. Unfortunately that's not the case: the whole `MATH.REL` will be included in the final program in all cases. LINK-80 doesn't actually know which of the relocatable files it processes is "main program" as opposed to "code libraries", thus it treats all the files equally and this implies including the complete contents of all the processed files in the output.

