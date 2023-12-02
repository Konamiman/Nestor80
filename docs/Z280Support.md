# Z280 support

Nestor80 can assemble [Z280](https://en.wikipedia.org/wiki/Zilog_Z280) instructions when a `.cpu Z280` assembler instruction is used in the source code and when `-cpu Z280` is added to the command line arguments when executing Nestor80. All the instructions listed in the _Z280 MPU Microprocessor Unit_ technical manual are supported.

This document explains the extra features that Nestor80 provides when assembling Z280 code.


## Disabling privileged and I/O instructions

The Z280 processor support a "user mode" in which certain control instructions, denominated _privileged instructions_, aren't allowed (a _trap_, or "internal interruption", is triggered when a privileged instruction is found at runtime). Optionally, I/O instructions (the instructions that read or write from the ports address space) can be considered as privileged by setting a special configuration flag.

Nestor80 has a mechanism to disallow the core privileged instructions and optionally also the I/O instructions when assembling Z280 code, so that when one of such instructions are found an error is thrown and the assembly process fails. This is useful when asembling Z280 code intended to run in user mode.

The mechanism is implemented as two special symbols that can be defined with the standard `EQU` and `DEFL` instructions, and with the Nestor80 `--define-symbols` command line argument:

* `_Z280.AllowPrivileged`: when this symbol exists and has a value **equal to zero**, privileged instructions won't be allowed. The core privileged instructions are: `DI`, `EI`, `HALT`, `IM`, `LDCTL`, `LDUD`, `LDUP`, `RETI`, `RETIL`, `RETN`, `LD A,I`, `LD I,A`, `LD A,R`, `LD R,A`.
* `_Z280.IoPrivileged`: when this symbol exists and has a value **different from zero**, I/O instructions will be considered as privileged, and thus they won't be allowed if `_Z280.AllowPrivileged` exists and equals zero. The I/O instructions are: `IN`, `IND`, `INDR`, `INDW`, `INDRW`, `INI`, `INIR`, `INIW`, `INIRW`, `INW`, `OTDR`, `OTDRW`, `OTIR`, `OTIRW`, `OUT`, `OUTD`, `OUTDW`, `OUTI`, `OUTIW`, `OUTW`, `TSTI`

As it's the case of regular symbols, the names are case-insensitive.

Example:

```
.cpu z280

di
in a,(c)

_Z280.AllowPrivileged: defl 0

di ;This will throw an error
in a,(c)

_Z280.IoPrivileged: defl 1

di ;This will throw an error
in a,(c) ;This will throw an error
```


## Auto, short and long index mode

There are some Z280 instructions that have an argument of the form `(IX+n)` or `(IY+n)` and provide two different instructions (with different instruction bytes) for `n` being an 8 bit number and for `n` being a 16 bit number. For example:

```
ld a,(ix+55h)   ;Produces DD 7E 55
ld a,(ix+1122h) ;Produces FD 79 22 11
```

The short version of the instruction is often, but not always, inherited from the Z80 instruction set.

This poses an interesting question: which instruction version should be used when the index fits in one byte? Always the short version? After all, `FD 79 55 00` would also be a valid representation for `ld a,(ix+55h)`, and in some cases the long index version might be convenient if the code is expected to modify itself. And what about values that are relocatable, external, or unknown in pass 1? The assembler needs to decide in pass 1 which instruction version to use and the decision needs to be kept in pass 2.

To solve this, Nestor80 has three different _Z280 index modes_, one of them being always active when assembling Z280 code. The active index mode is selected by defining a symbol named `_Z280.IndexMode`. 


### Auto index mode

This is the default mode, it's active when the `_Z280.IndexMode` doesn't exist and also when it exists and has a value of `0`. In this mode, the short or the long version of the `(IX+n)` and `(IY+n)` instructions is selected as follows:

* **If** the expression for `n` evaluates to an absolute value, **and** this value fits in one byte (the value is between -128 and 127), **then** the short version of the instruction is used.
* In all other cases the long version of the instruction is selected. This includes:
  * The expression evaluates to an absolute value, but the value doesn't fit in one byte.
  * The expression evaluates to a relocatable value.
  * The expression includes external symbols.

This mode is appropriate for most programs, so use it if you are unsure.


### Short index mode

This mode is active when the `_Z280.IndexMode` symbol exists and has a value of `1`. In this mode the short version of the instruction is always selected. This implies that if the expression for `n` evaluates to a value that doesn't fit in one byte, an error will be thrown, either at assembly time (for expressions that evaluate to an absolute value) or at linking time (for expressions that evaluate to a relocatable value or contain external symbol references).

This mode is appropriate when program space needs to be minimized and you are sure that values not fitting in one byte won't be used for `(IX+n)` and `(IY+n)` instructions in the program.


### Long index mode

This mode is active when the `_Z280.IndexMode` symbol exists has a value of `2`. In this mode the long  version of the instruction is always selected. So for example the `ld a,(ix+55h)` instruction will produce the sequence of bytes `FD 79 05 00`.

This mode might be useful for programs that modify themselves, so that you might have an instruction that is written as `(IX+0)` but that 0 is replaced with an arbitrary value at runtime.


### Example

Here's a listing file that illustrates how Nestor80 generates code for an instruction that has short and long index versions, depending on the configured index mode:

```
                          cpu Z280
                          
  0000'                   RELOC:
                          
  0000                    _z280.indexmode: defl 0  ;Auto index mode
                          
  0000'   DD 77 34        ld (ix+34h),a
  0003'   ED 2B 22 11     ld (ix+1122h),a
  0007'   ED 2B 0000'     ld (ix+RELOC),a
  000B'   ED 2B 0000*     ld (ix+EXTERNAL##),a
                          
  0001                    _z280.indexmode: defl 1  ;Short index mode
                          
  000F'   DD 77 34        ld (ix+34h),a
                          ;ld (ix+1122h),a  --  Would cause "Invalid argument: value out of range for IX instruction" error
  0012'   DD 77 00'       ld (ix+RELOC),a
  0015'   DD 77 00*       ld (ix+EXTERNAL##),a
                          
  0002                    _z280.indexmode: defl 2  ;Long index mode
                          
  0018'   ED 2B 34 00     ld (ix+34h),a
  001C'   ED 2B 22 11     ld (ix+1122h),a
  0020'   ED 2B 0000'     ld (ix+RELOC),a
  0024'   ED 2B 0000*     ld (ix+EXTERNAL##),a
```


### Limitations of the auto index mode

There are a couple of cases where seemingly weird errors could arise when assembling Z280 code in the auto index mode, caused by the fact that the assembly process takes two passes.

Consider the following example:

```
.cpu Z280

org 100h

ld (ix+34h),a

_Z280.IndexMode: defl 2

LABEL:

end
```

This code will throw the following: _ERROR: in line 9: Label LABEL has different values in pass 1 (ASEG 0103h) and in pass 2 (0104h)_. What happened here?

1. In pass 1 the `_Z280.IndexMode` symbol is initially undefined, thus the index mode implicitly in use is "auto". The `ld (ix+34h),a` instruction is assembled as the short version, which takes 3 bytes; then `LABEL` gets the value 103h.
2. Index mode is then changed to "long" by the `_Z280.IndexMode: defl 2` line.
3. Pass 2 starts with `_Z280.IndexMode` still having a value of 2, thus the index mode is "long" and `ld (ix+34h),a` takes now 4 bytes, so the value that `LABEL` gets is now 104h.

The solution for this issue is simple: if you modify the value of `_Z280.IndexMode` throughout your code, initialize it to a known value at the beginning:

```
_Z280.IndexMode: defl 0  ;Problem solved!

.cpu Z280

org 100h

ld (ix+34h),a

_Z280.IndexMode: defl 2

LABEL:

end
```

The second problem doesn't have such an easy solution. Consider this code:

```
_Z280.IndexMode: defl 0

.cpu Z280

org 100h

ld (ix+SHORT_VALUE),a

SHORT_VALUE: equ 34h

LABEL:

end
```

This too will throw _ERROR: in line 11: Label LABEL has different values in pass 1 (ASEG 0104h) and in pass 2 (0103h)_. What happens now is:

1. In pass 1 Nestor80 finds the `ld (ix+SHORT_VALUE),a` instruction, and it doesn't know the value of `SHORT_VALUE`. Being in "auto" index mode, it resorts to using the long version of the instruction, which takes 4 bytes. `LABEL` then gets the value 104h.
1. In pass 2 the value of `SHORT_VALUE` is known and it fits in one byte, thus the "auto" index mode dictates that the short version of the instruction must be used. That version takes 3 bytes, so `LABEL` gets the value 103h.

To solve this you need to modify your code in one of these ways:

1. Use the long index mode.
2. Don't reference symbols that are defined after the instruction in the index expression of `(IX+n)` and `(IY+n)` instructions (if you place the symbol definition before the instruction that uses it, the problem disappears).
