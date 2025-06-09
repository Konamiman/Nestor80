# Writing relocatable code

Nestor80 allows to write [absolute and relocatable code](LanguageReference.md#absolute-and-relocatable-code). This document explains how the process of writing and linking relocatable code works.

⚠ This documents explains concepts related to relocatable code as a generic concept, but focuses on the MACRO-80 compatible relocatable file format whenever specific examples are provided and when the linking process is detailed. Nestor80 can also generate relocatable files that are compatible with the [SDCC](https://sdcc.sourceforge.net/) compiler; see ["SDCC file format support"](SdccFileFormatSupport.md) for specific information about this capability.

Writing relocatable code involves using the Linkstor80 tool and optionally also the Libstor80 tool additionally to Nestor80 (all three are part of the Nestor80 project). Another option is to use LINK-80 and LIB-80 instead, which are part of the original MACRO-80 package; in that case you might also want to take a look at:

* [The original MACRO-80, LINK-80 and LIB-80 user manual](MACRO-80.txt)
* [The M80dotNet project](https://github.com/konamiman/M80dotNet), a convenient way to use LINK-80 and LIB-80 in modern machines (same system requirements as Nestor80).

You'll need to pass the `--link-80-compatibility` argument to Nestor80 when generating relocatable files if you want to use LINK-80 and LIB-80 instead of Linkstor80 and Libstor80 for the linking process. This argument instructs Nestor80 to generate relocatable files using the old LINK-80 compatible relocatable file format, see ["Relocatable file format"](RelocatableFileFormat.md) for more details. The rest of this document assumes that you will use Linkstor80 and Libstor80.

This document doesn't detail all the arguments available for Linkstor80 and Libstor80, you can run the tools with a `-h` argument to get comprehensive help on the usage of these tools.

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

At linking time all the code for each segment in each program is combined together; thus all the content intended for the code segment is combined, the same for the data segment, and the same for each COMMON block. COMMON blocks allow sharing code and data between different programs: while the code and data segments of each program will be linked separately, common blocks of the same name will share the same memory area in the final linked program, regardless of which relocatable program defines them.

The absolute segment isn't actually relocatable: it's used for cases in which code must be assembled at a fixed memory address, bypassing the relocation process performed at linking time.


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

Now it's linking time. We run Linkstor80 like this:

```
LK80 --code 0100h --data 0120h --output-file SIMPLE.COM SIMPLE.REL
```

You can run Linkstor80 with the `-h` argument for the full command line syntax, but what this command means is "open SIMPLE.REL and link it assuming that code segment starts at address 0100h and data segment starts at address 0120h".

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
* There's a gap between the end of the code segment content and the start of the data segment, the linker fills it with zeros by default (you can specify a different byte value to use for filling gaps by passing the `--fill` argument to Linkstor80).

⚠ For compatiblity with LINK-80 the initial code address if no `--code` argument is passed is 0103h.


### Using the absolute segment

To illustrate the usage of the absolute segment let's do a small change to our `SIMPLE.ASM` program. Replace the `dseg` line with the following:

```
aseg
org 0120h
```

Then assemble it with `N80 SIMPLE.ASM`, but the linker command line is `LK80 --code 0100h --output-file SIMPLE.COM SIMPLE.REL` this time (since the data segment isn't being used now). The generated `SIMPLE.COM` file is equivalent to the one from the previous example.


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
LK80 --code 0100h --data 0120h --output-file PROG.COM PRINT.REL PROG.REL
```

Linkstor80 "connects the dots" and matches the public `PRINT` symbol exposed by `PRINT.REL` with the external reference of the same name in `PROG.REL`. The final program file generated, `PROG.COM`, is identical to the `SIMPLE.COM` of the first example.


## Segment start addresses with multiple programs

You may have noticed something that in principle seems weird regarding how relocatable addresses are linked. In the previous example, both the `PRINT` label from `PRINT.ASM` and the `PROGRAM` label from `PROG.ASM` refer to "code segment relative 0000h" address in their respective relocatable files. Then how can the linking process work? Wouldn't both resolve to address 0100h and thus one would overwrite the other?

The answer is that what the linker actually does is to treat the relocatable addresses in each of the involved programs (each of the `.REL` files passed to the LINK-80 command line) as **relative to the final start address of each program**. This will be the same as the address passed with `--code` or `--data` to LINK-80 only for the first of the programs.

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

When both programs are assembled and linked with `LK80 --code 0100h --output-file PROG.COM PROG.REL SUM.REL`, the resulting `PROG.COM` file is equivalent to this:

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


## Rules for code and data organization in the linked program

We have seen how the linker processes all the passed relocatable files and converts the contained relocatable programs into absolute code at different parts of the memory used by the final binary program, but what are the exact rules followed to decide where each piece goes?

Linkstor80 has three working modes:

* **Data before code**. In this mode the programs are placed in memory as follows: first the data segment, right after the last address used by the previous program or at the address provided by a preceding `--code` argument; and immediately following, the code segment. This is the initial mode when Linkstor80 starts.

* **Code before data**. Same as "Data before code" but in the reverse order: the code segment comes before the data segment.

* **Separate code and data**. The code segment of the next processed program is placed right after the end of the code segment of the previously processed program, or at the address provided by a preceding `--code` argument; the data segment of the next processed program is placed right after the end of the data segment of the previously processed program, or at the address provided by a preceding `--data` argument.

Beware: in "Data before code" mode, `--code` arrguments specify the starting address of the next program's _data_ segment, **not** the next program's _code_ segment; this might seem counterintuitive at first.

"Data before code" and "Code before data" modes can be switched back and forth with the `--data-before-code` and `--code-before-data` arguments in the linking sequence, respectively. The "Separate code and data" mode is entered by specifying a `--data` argument. Once the "Separate code and data" mode is entered it is **not** possible to go back to any of the other two modes. This behavior is compatible with LINK-80 (except that LINK-80 doesn't have the "Code before data" mode).

Let's go through an example. Create the following program in a file named `PROG1.ASM`:

```
cseg
db "!PROG_1_CODE!"

dseg
db "?PROG_1_DATA?"
```

Assemble it with `N80 PROG1.ASM` to get a relocatable file named `PROG1.REL`. Then repeat this process with other six similar programs, `PROG2.ASM` to `PROG7.ASM`, in which the `_1_` in the code is changed to the appropriate number.

Then trigger the link process as follows:

```
LK80 --output-file PROG.BIN --code 0 PROG1.REL PROG2.REL --code 0040h PROG3.REL \
     --code-before-data PROG4.REL --data-before-code PROG5.REL \
     --code 0090h --data 00B0h PROG6.REL PROG7.REL
```

The resulting binary file, `PROG.BIN`, will have the following contents (dots represent zero bytes):

```
0000 ?PROG_1_DATA?!PR
0010 OG_1_CODE!?PROG_
0020 2_DATA?!PROG_2_C
0030 ODE!............
0040 ?PROG_3_DATA?!PR
0050 OG_3_CODE!!PROG_
0060 4_CODE!?PROG_4_D
0070 ATA??PROG_5_DATA
0080 ?!PROG_5_CODE!..
0090 !PROG_6_CODE!!PR
00A0 OG_7_CODE!......
00B0 ?PROG_6_DATA??PR
00C0 OG_7_DATA?
```

Notice how:

* For `PROG1` and `PROG2` the mode is "Data before code" and thus data segment is placed before code segment for each program.
* For `PROG3` we specify an explicit start address with `--code`, thus bypassing the default "right after the previous program" rule.
* For `PROG4` we switch to "Code before data" mode, then for `PROG5` we go back to "Data before code" mode.
* For `PROG6` we switch to "Separate code and data mode", thus the code segments of `PROG6` and `PROG7` go together, and the same for their data segments.

The default start code address used by Linkstor80 if no `--code` argument is specified is 0103h for compatibility with LINK-80 (a jump instruction is supposed to go at address 0100h, the entry point address of CP/M programs).


## Location of COMMON blocks

When using common blocks the following rules apply:

* The first appearance of a common block with a given name in a program fixes the address in which the block will be located. This address is right before the data segment of that program.
* The next time the common block with the same name appears in a program, it's linked in the same fixed memory area (`ORG` statements need to be used to prevent data overlap between programs).
* If a common block of a given name is defined in multiple programs, the first definition must be the largest one.

Let's see an example. Create the following programs:

```
;COMMONS1.ASM

common /FOO/
db "-PROG_1_FOO-"
ds 20 ;To turn this instance of the block into the largest one

cseg
db "!PROG_1_CODE!"

dseg
db "?PROG_1_DATA?"
```

```
;COMMONS2.ASM

common /FOO/
org 10h ;To avoid overlap with the data from the block in COMMONS1
db "-PROG_2_FOO-"

common /BAR/
db "-PROG_2_BAR-"
ds 20 ;To turn this instance of the block into the largest one

cseg
db "!PROG_2_CODE!"

dseg
db "?PROG_2_DATA?"
```

```
;COMMONS3.ASM

common /BAR/
org 10h ;To avoid overlap with the data from the block in COMMONS2
db "-PROG_3_BAR-"

common /FIZZ/
db "-PROG_3_FIZZ-"

cseg
db "!PROG_3_CODE!"

dseg
db "?PROG_3_DATA?"

```

Assemble these with `N80 COMMONS1.ASM` etc, then link with:

```
LK80 --output-file COMMONS.BIN --code 0 COMMONS1.REL COMMONS2.REL --code-before-data --code 0080h COMMONS3.REL
```

This is how the resulting `COMMONS.BIN` file will look like (dots represent zero bytes):

```
0000 -PROG_1_FOO-....
0010 -PROG_2_FOO-....
0020 ?PROG_1_DATA?!PR
0030 OG_1_CODE!-PROG_
0040 2_BAR-....-PROG_
0050 3_BAR-....?PROG_
0060 2_DATA?!PROG_2_C
0070 ODE!............
0080 !PROG_3_CODE!-PR
0090 OG_3_FIZZ-?PROG_
00A0 3_DATA?
```

Notice how:

* Common blocks of the same name are combined together, even though they are defined through multiple programs.
* The first appearance of a common block fixes its address.
* The fixed address of a common block is right before the data segment of the program in which it first appears.


## Aligning code

Sometimes you'll need part of your code or data to be aligned at a given boundary in memory, for example you may want the start of a data table to be located at an address that is a multiple of 256. While you can achieve that by simply using the `ALIGN` instruction in your code when building an absolute file, in the case of relocatable code you'll need to rely on the linker for that.

To that end Linkstor80 provides two arguments:

* `--align-code <alignment>`: Instructs the linker to start the linking of the code segment for the next program (if the "separate code and data" linking mode is active) or the entire next program (if one of the "code before data" or "data before code" modes is active) in the first address that is greater than the last address used by the previous program (the last code segment or data segment address, depending on the linking mode) _and_ is a multiple of the `<alignment>` argument provided.

* `--align-data <alignment>`: This argument can be used only in "separate code and data" mode and is similar to `--align-code`, but it applies to the data segment only.

Let's see an example using the `PROG1.ASM` and `PROG2.ASM` files that we saw in the "Rules for code and data organization in the linked program" section. Assuming you have already assembled them with N80, run the following to link them using the code alignment feature:

```
LK80 --output-file PROG.BIN --code 0 PROG1.REL --align-code 0020h PROG2.REL
```

This is how the resulting `PROG.BIN` file will look like (dots represent zero bytes):

```
0000 ?PROG_1_DATA?!PR
0010 OG_1_CODE!......
0020 ?PROG_2_DATA?!PR
0030 OG_2_CODE!
```

As you can see the first program contents end at address 0019h, then we instruct the linker to start the next program at the first address after that one that is a multiple of 16. The first address for which that is true is 0020h, so that's where the second program is linked.

Switching to the "code before data" mode has a similar result:

```
LK80 --output-file PROG.BIN --code-before-data --code 0 \
     PROG1.REL --align-code 0020h PROG2.REL
```

Result:

```
0000 !PROG_1_CODE!?PR
0010 OG_1_DATA?......
0020 !PROG_2_CODE!?PR
0030 OG_2_DATA?
```

Now let's take a look at what happens when we switch to "separate code and data" mode and use both `--align-code` and `--align-data`:

```
LK80 --output-file PROG.BIN --code 0 --data 0040h PROG1.REL \
     --align-code 0010h --align-data 0020h PROG2.REL
```

Result:

```
0000 !PROG_1_CODE!...
0010 !PROG_2_CODE!...
0020 ................
0030 ................
0040 ?PROG_1_DATA?...
0050 ................
0060 ?PROG_2_DATA?
```

We see the two alignments in place here:

* The first program's code ends at address 000Ch, and then we instruct the linker to start linking the next program's code at the next address that is a multiple of 16. That address is 0010h.
* Similarly, the first program's data ends at address 004Ch, and then we instruct the linker to start linking the next program's data at the next address that is a multiple of 32. That address is 0060h.


## Combining programs into libraries

The Libstor80 tool can be used to combine multiple relocatable files into one single library file, which can then be used with LINK-80 instead of the individual relocatable files.

For example, assume that you have a collection of mathematical routines, each in its own file like `SUM.REL`, `MULT.REL` and `DIV.REL`. You may use them with LINK-80 like this:

```
LK80 --output-file PROG.COM PROG.REL SUM.REL MULT.REL DIV.REL
```

Instead, you can use LIB-80 to combine the routines into a single `MATH.REL` library like this:

```
LB80 create MATH.LIB SUM.REL MULT.REL DIV.REL
```

...and then use it directly in LINK-80:

```
LK80 --output-file PROG.COM PROG.REL MATH.REL
```

⚠ You may think that Linkstor80 will only take the required programs from the library, as it happens when using libraries in other languages like C; for example if `PROG` only uses the `MULT` routine then the contents of `SUM.REL` and `DIV.REL` wouldn't be included in the generated `PROG.COM`. Unfortunately that's not the case: the whole `MATH.REL` will be included in the final program in all cases. Linkstor80 doesn't actually know which of the relocatable files it processes is "main program" as opposed to "code libraries", thus it treats all the files equally and this implies including the complete contents of all the processed files in the output. This behavior is compatible with LINK-80.

