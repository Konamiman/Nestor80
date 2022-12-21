# Nestor80

Nestor80 is [Z80](https://en.wikipedia.org/wiki/Zilog_Z80) assembler written in C#
and "fully" compatible with the [Microsoft MACRO-80](https://en.wikipedia.org/wiki/Microsoft_MACRO-80) assembler.

## Feature highlight

* **Multiplatform**. Runs on any machine/OS that supports [the .NET 6 runtime](https://dotnet.microsoft.com/en-us/download/dotnet/6.0); of course this includes Windows, Linux and macOS.
* Almost fully **compatible with [Microsoft MACRO-80](https://en.wikipedia.org/wiki/Microsoft_MACRO-80)** for Z80 code (Nestor80 can't assemble 8080 code). Most of the incompatibilites are for obscure or undocumented features.
* Can produce **absolute and relocatable binary files**. Relocatable files conform to the format used by Microsoft LINK-80.
* Use **unlimited length, arbitrary character encoding symbols** in your code:

```
Ñoñería: ;This is a valid symbol
このラベルは誇張されていますがNestor80がいかに強力であるかを示しています equ 34 ;This too!
```

  _Public and external symbols are still limited to ASCII-only and up to 6 characters in length, this is a limitation of the relocatable file format used by LINK-80._

* **Detailed, comprehensive error reporting**:

![](docs/img/ConsoleErrors.png)

* **Modern string handling**: it's possible to choose the encoding to be used when converting text strings (supplied as arguments to `DEFB` instructions) to sequences of bytes, and all of the [.NET escape sequences](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/strings/#string-escape-sequences) are allowed in strings.

```
  .STRENC 850
HELLO_SP:
  defb "¡Hola en español, esto es fantástico!"

  .STRENC shift_jis
HELLO_JP:
  defb "日本語でこんにちは、これは素晴らしいです！"

  .STRENC default
JOKE:  
  defb " A tab,\ta line feed\nand a form feed\fwalk into a bar...\0"

```

* **User-triggered warnings and errors**, with support for **expression interpolation**:

```
if $ gt 7FFFh
.error ROM page boundary crossed, current location pointer is {$:H4}h
endif
```

* **Nested INCLUDEd files** (MACRO-80 allows only one level for `INCLUDE`) and of course, **support for arbitrary paths** for included files.