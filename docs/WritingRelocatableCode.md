# Writing relocatable code

Nestor80 allows to write [absolute and relocatable code](LanguageReference.md#absolute-and-relocatable-code). This document explains how the process of writing and linking relocatable code works.

Writing relocatable code involces using the LINK-80 tool and optionally the LIB-80 tool additionally to Nestor80, thus you might also want to take a look at:

* [The original MACRO-80, LINK-80 and LIB-80 user manual](MACRO-80.txt)
* [The M80dotNet project](https://github.com/konamiman/M80dotNet), a convenient way to use LINK-80 and LIB-80 in modern machines (same system requirements as Nestor80).

This document uses [the same conventions as the Nestor80 language reference guide](LanguageReference.md#document-conventions).


## The basics

For programs of low and moderate complexity that don't have dependencies on libraries or other code sharing mechanisms, assembling in absolute mode is usually appropriate. However for more complex projects you may find yourself in the need of:

1. Separating your source code in smaller files, assembling them independently (possibly using a [makefile](https://en.wikipedia.org/wiki/Make_(software))) and then combining the result into a final binary (absolute) file. See for example [the makefile for Nextor](https://github.com/Konamiman/Nextor/blob/v2.1/source/kernel/Makefile)).

2. Creating reusable code libraries and incorporating them in your programs by using [static linking](https://en.wikipedia.org/wiki/Static_library).

In both cases the final memory addresses in which each program part will end up being loaded are unknown at assembly time and decided at linking time. That's why each program part needs to be assembled as _relocatable_.

> A relocatable file is a "pre-assembled" file following [a special format](RelocatableFileFormat.md) in which all the references to internal program addresses are stored as values that are relative to the starting memory address for the program. A relocatable file can optionally declare public [symbols](LanguageReference.md#symbols) and contain references to external symbols.

A _public symbol_ is a symbol whose (relative) value is exposed by the program, and an _external reference_ is a reference to a symbol that needs to be supplied by a different program (which declares it as a public symbol). The linking process solves the "puzzle" by matching public symbols with external references from all the involved programs, and finally "relocating" all the code into their final absolute addresses, thus generating the final absolute binary file.


## Memory segments

All the code in a relocatable file lives in a _segment_ or logical memory area. The following segments are defined:

* The absolute segment
* The code segment
* The data segment
* Named COMMON blocks

Despite the "code" and "data" names, these segments can contain any kind of content: code, data or both; these names are pretty much just a convention.

During the assembly process there's always one of these segments that is considered the active segment (it's the code segment at the beginning of the process) and that's where the assembled code is assigned. The active segment can be changed with the [`ASEG`](LanguageReference.md#aseg-), [`CSEG`](LanguageReference.md#cseg-), [`DSEG`](LanguageReference.md#dseg-) and [`COMMON`](LanguageReference.md#common-) instructions.

The absolute segment isn't actually relocatable: it's used for cases in which code must be assembled at a fixed memory address, bypassing the relocation process performed at linking time.

COMMON blocks exist for compatibility with MACRO-80, which in turn pretty much supported them for compatibility with the (at the time) popular languages Cobol and Fortran. For Z80 assembly code you'll usually only use the code and data segments.

At linking time all the code for each segment in each program is combined together


