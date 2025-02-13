﻿using Konamiman.Nestor80.Assembler.Errors;

namespace Konamiman.Nestor80.N80
{
    internal partial class Program
    {
        static readonly string bannerText = """
            Nestor80 - The Z80 and Z280 assembler for the 21th century
            (c) Konamiman 2025

            """;

        static readonly string simpleHelpText = """
            Usage: N80 <source file> [<output file>] [<arguments>]
                   N80 -v|--version
                   N80 -h|--help [<argument name>]
                   N80 --list-encodings
            """;

        /// <summary>
        /// Full help text, displayed when the program is run with the -h or --help argument.
        /// </summary>
        static readonly string extendedHelpText = $"""

            <source file>
                Full path of the Z80 assembly source file to process,
                can be absolute or relative to the current directory.
            
            <output file>
                Full path of the assembled absolute or relocatable file to generate.
                
                If omitted, a file with the same name of the source file and
                .BIN or .REL extension (unless a different extension is specified with 
                --output-file-extension) will be generated in the current directory.
            
                If it's a directory (instead of a file), the output file will be
                generated in that directory, and the file name will be as when
                the argument is omitted.
            
                If the path is relative it will be considered relative to
                the current directory.
            
                If the path starts with '$/' it will be considered relative to
                the directory of the source file.
            
                If the path is just '$' then the file will be generated in
                the directory of the source file, and the file name will be
                as when the argument is omitted.

            Arguments can be specified as follows (and are combined in that order):
            
            - A N80_ARGS environment variable (can be disabled with --no-env-args).
            - A .N80 file in the same directory of the input file.
              (can be disabled with --no-default-file-args); see --arguments-file
              for the file contents format.
            - The command line.
            - Argument files (with --argument-file).
            
            If you need to include a space as part of an argument (e.g. a directory name) 
            in N80_ARGS or in an arguments file, escape it with a backslash (so "\ "
            to represent " "). Escaping for command line arguments depends on your shell.

            Available arguments:

            -abe, --allow-bare-expressions
                Treat a line containing one or more bare expressions as a DB line
                (e.g. the line 'FOO: 1,2,3,4' will be equivalent to 'FOO: db 1,2,3,4').
                You may need this when compiling old code intended for MACRO-80, but
                using bare expressions is otherwise not recommended because it masks
                mistyped instructions as "symbol not found" errors.

                Note that you need this argument too if you want to use named macros
                before they are defined.

            -adp, --accept-dot-prefix
                Accept a dot (.) as prefix for non-CPU instructions that don't have
                already a dot in front of its name, for example ".ORG" will be
                accepted as an alias for "ORG". This may be useful when assembling
                code written for other assemblers.

            -af, --arguments-file <file path>
                Read additional arguments from the specified file. The arguments are
                processed immediately. Recursivity is not supported (additional
                --arguments-file arguments aren't allowed inside an arguments file).

                If the path is relative it will be considered relative to
                the current directory.
            
                If the path starts with '$/' it will be considered relative to
                the directory of the source file.

                Arguments inside the file can be all in one single line or
                spread across multiple lines. Lines starting with ; or #
                (not counting leading spaces) will be ignored.

                This option can't be undone (there's not "--no-arguments-file" argument),
                but if necessary you can use --reset-config to start over.
            
            -arl, --allow-relative-labels
                Enable support for relative labels.

                When enabled, labels with a name starting with a dot are considered relative to
                the last non-relative label defined, whose name takes as a prefix. Example:

                FOO:
                .loop:
                ;do something
                djnz .loop

                The code that will actually be assembled when relative labels are enabled is:

                FOO:
                FOO.loop:
                ;do something
                djnz FOO.loop

                Support for relative labels can also be enabled in code with the .RELAB instruction.

            -bt, --build-type abs|rel|sdcc|auto
                The type of output to build: "abs" is for absolute, "rel" is for M80 relocatable,
                "sdcc" is for SDCC relocatable. Default is auto.
            
                In auto mode the build type will be set as absolute or SDCC relocatable if an ORG
                statement or an AREA statement, respectively, is found in the code before a CPU
                instruction, a label defined as public with "::", or any of the following instructions:
                ASEG, CSEG, DSEG, COMMON, DB, DW, DS, DC, DM, DZ, PUBLIC, EXTRN, .REQUEST;
                otherwise the build type will be set as M80 relocatable.

                The --direct-output-write argument will also set the build type to absolute.

            -cid, --clear-include-directories
                Clear the list of the extra directories where relative INCLUDEd files
                will be searched for.

            -co, --color-output
                Display assembly process messages and errors in color (default).

            -cpu, --default-cpu <cpu>
                Set the target CPU of the source code (default is Z80).
                The target CPU can also be changed in code with the .CPU instruction.

            -dfa, --default-file-args
                Read arguments from the .N80 file in the directory of the input file (default).
                This argument is ignored when found inside an arguments file (.N80 or any other).

            -dhp, --discard-hash-prefix
                Discard any hash (#) character prefixing expressions before evaluating them,
                for example "dw #1234" will be equivalent to "dw 1234". This is iseful when assembling
                source code written for the SDAS assembler, which requires numeric literals
                to be prefixed with #.

            -do, --do-output
                Generate an output file (this is the default behavior).
                This argument is the opposite of --no-output.
            
            -dow, --direct-output-write
                This argument has effect only when the build type is absolute
                (and in fact, it sets the build type to absolute by itself).
            
                Nestor80 has two strategies to generate the output file when the build type is absolute:
            
                - Memory map: Create a 64K memory map and fill it with the output bytes according to the
                  ORG instructions found; then dump the filled memory between the minimum and the maximum 
                  memory addresses used, with a maximum of 64KBytes.
                - Direct output file write: Just write the output sequentially to the output file,
                  which doesn't have a maximum length. A side effect is that ORG instructions are treated
                  as equivalent to .PHASE.
            
                "Memory map" is the default strategy, this argument will cause Nestor80 to switch to the 
                "Direct output file write" strategy.
            
                Example:
            
                org 100
                db 1,2,3,4
                org 110
                db 5,6,7,8
                org 102
                db 9,10
            
                Output using memory map: 1,2,9,10,0,0,0,0,0,0,5,6,7,8
                Output using direct file write: 1,2,3,4,5,6,7,8,9,10

            -ds, --define-symbols <symbol>[=<value>][,<symbol>[=<value>][,...]]
                Predefine symbols for the assembled program.
                Values can be decimal numbers, or hexadecimal numbers with a 'h' suffix.
                The default value if no <value> is provided is FFFFh. 
                The symbols will be created as if they had been defined with DEFL,
                therefore they can be redefined in the source code using the same instruction.

                Example: -ds symbol1,symbol2=1234,symbol3=ABCDh

            -eol, --end-of-line auto|cr|lf|crlf
                Character sequence to use as line separator for the text files generated
                during the assembly process. The default value is 'auto', which means using
                the standard end of line sequence of the system in which Nestor80 is running.
                Currently this setting affects the generation of listing files and
                SDCC relocatable files.
                
            -id, --include-directory <directory path>
                By default relative paths referenced in INCLUDE and INCBIN instructions will
                be considered to be relative to the current directory or to the directory
                of the file currently being processed. This argument allows to
                specify an extra directory where INCLUDEd files will be searched for;
                use the argument multiple times to add more than one directory.
                Directories are scanned in the order they are declared.

                If <directory path> isn't absolute it will be considered relative
                to the current directory.

                If <directory path> starts with '$/' it will be considered relative to
                the directory of the source file.

                If <directory path> is just '$' then it's the directory of the source file
                (in case you want to use that directory even from inside INCLUDE files
                that are in a different directory).

            -ids, --initialize-defs
                This argument has effect only when the build type is MACRO-80 relocatable.
                It will cause areas defined with "DEFS <size>" statements to be initialized
                with zeros, that is, these will be treated as equivalent to "DEFS <size>,0".

            -ie, --input-encoding <encoding>
                Text encoding of the source file, it can be an encoding name or a codepage number.
                Run N80 with the --list-encodings argument to get a list of available encodings.
                Default is UTF-8.

            -l, --listing-file [<file path>]
                Generate a listing file.
            
                If <file path> is omitted, a file with the same name of the source file
                and .LST extension (unless a different extension is specified with 
                --listing-file-extension) will be generated in the current directory.
            
                If <file path> is a directory (instead of a file), the listing file
                will be generated in that directory, and the file name will be as when
                the argument is omitted.
            
                If <file path> is relative it will be considered relative to
                the current directory.
            
                If <file path> starts with '$/' it will be considered relative to
                the directory of the source file.
            
                If <file path> is just '$' then the file will be generated in
                the directory of the source file, and the file name will be
                as when the argument is omitted.

            -l8c, --link-80-compatibility
                Keep compatibility with LINK-80 (only relevant when the build type is relocatable):
                public and external symbols will be limited to 6 characters with only ASCII letters
                allowed, and the set of arithmetic operators allowed for expressions involving
                external references will be limited to: +, -, unary -, *, /, HIGH, LOW, MOD, NOT.

            -lbpl, --listing-bytes-per-line <count>
                How many bytes to print in one single line of text for instructions that generate
                an arbitrary number of bytes (typically DB or DW) when generating the listing file;
                for instructions that generate more bytes multiple lines of text will be generated.
                Allowed values are 2 to 256, the default value is 4.

            -le, --listing-file-encoding <encoding>
                Text encoding for the generated listing file, it can be an encoding name
                or a codepage number. Run N80 with the --list-encodings argument to get 
                a list of available encodings. Default is UTF-8.

            -lfc, --listing-false-conditionals
                Include conditional block that evaluate to false when generating the listing file
                (this is the default). This can also be controlled in the source code by using
                the .TFCOND, .LFCOND and .SFCOND instructions.

            -lic, --listing-include-code
                Include the processed source code when generating the listing file
                (this is the default).

            -lis, --listing-include-symbols
                Include the generted symbols, macro names and SDCC area names when generating
                the listing file (this is the default).

            -lmbi, --listing-max-bytes-per-instruction <count>
                The maximum number of bytes to print for instructions that generate an arbitrary
                number of bytes (typically DB or DW) when generating the listing file;
                for instructions that generate more bytes, the excess bytes will be left out
                of the listing, and "..." will be added after the last listed byte.
                Allowed values are 1 to 65535, the default value is 128.

            -lmsl, --listing-max-symbol-length <length>
                The maximum length that will be printed for each symbol name when generating
                the symbols list of the listing file; longer symbol names will be truncated
                to this length and get "..." appended at the end". Allowed values are 4 to 256,
                the default value is 16.

            -lspl, --listing-symbols-per-line <count>
                The number of symbols that will be included in one single line of text
                when generating the symbols list of the listing file. Allowed values are 1 to 256,
                the default value is 4.

            -lus, --listing-uppercase-symbols
                Uppercase the symbol names, macro names and SDCC are names when printing them
                in the listing file (mimics the behavior of MACRO-80).

            -lx, --listing-file-extension [.]<extension>
                The extension for the generated listing file. This value is used only when the
                name of the listing file is chosen as the same name of the input file
                (because no listing file path is specified, or because the specified path
                is a directory). Default is .LST

            -me, --max-errors <count>
                Stop the assembly process after reaching the specified number of errors
                (not including warnings, and the process will still stop on the first fatal error).
                0 means "infinite". Default: {DEFAULT_MAX_ERRORS}.

            -nabe, --no-allow-bare-expressions
                Don't treat a line containing a list of comma-separated expressions as a DB line
                (so e.g. 'FOO: 1,2,3,4' will throw an error). This is the default behavior.

            -nadp, --no-accept-dot-prefix
                Don't accept a dot (.) as prefix for non-CPU instructions that don't have
                already a dot in front of its name. This is the default behavior.

            -narl, --no-allow-relative-labels
                Disable support for relative labels (default). See --allow-relative-labels for an
                explanation about relative labels.
            
                Support for relative labels can also be disabled in code with the .XRELAB instruction.

            -nco, --no-color-output
                Don't display assembly process messages and errors in color.

            -ndfa, --no-default-file-args
                Don't read arguments from the .N80 file in the directory of the input file.
                This argument is ignored when found inside an arguments file (.N80 or any other).

            -ndhp, --no-discard-hash-prefix
                Don't discard hash (#) characters prefixing expressions before evaluating them,
                so the hash character will act as a prefix for hexadecimal numbers,
                for example "dw #1234" will be equivalent to "dw 1234h". This is the default behavior.

            -ndow, --no-direct-output-write
                Use the "memory map" strategy, instead of rge "direct output file write" strategy,
                for generating the output file (this is the default behavior). See --direct-output-write.

            -nds, --no-define-symbols
                Forget all symbols that had been predefined with --define-symbols.

            -nea, --no-env-args
                Don't read arguments from the N80_ARGS environment variable.
                This argument is ignored when found inside N80_ARGS or an arguments file
                (.N80 or any other).

            -nfe, --no-output-file-extension
                Forget any previous --output-file-extension argument supplied and use the default
                extension for the output file name (if the output file name is automatically selected,
                see --output-file-extension).
            
            -nids, --no-initialize-defs
                This argument has effect only when the build type is relocatable. It will cause areas
                defined with "DEFS <size>" statements to not be initialized (the DEFS instruction
                will be treated as equivalent to "ORG $+<size>"). This is the default behavior.

            -nl, --no-listing-file
                Don't generate a listing file (default).

            -nl8c, --no-link-80-compatibility
                Don't keep compatibility with LINK-80 (only relevant when the build type is relocatable):
                there will be no restrictions to the length and the letter characters used for public and
                external symbols, and all the arithmetidc operators can be used in expressions involving
                external references. This is the default behavior.
            
            -nlfc, --no-listing-false-conditionals
                Don't include conditional block that evaluate to false when generating the listing
                file. This can also be controlled in the source code by using the .TFCOND, .LFCOND
                and .SFCOND instructions.

            -nlic, --no-listing-include-code
                Don't include the processed source code when generating the listing file.

            -nlis, --no-listing-include-symbols
                Don't include the generated symbols and macro names when generating the listing file.

            -nlus, --no-listing-uppercase-symbols
                Don't uppercase the symbol names when printing them in the listing file
                (keep the original casing from the source code). This is the default.

            -nlx, --no-listing-file-extension
                Forget any previous --listing-file-extension argument supplied and use the default
                extension for the listing file name (if the listing file name is automatically selected,
                see --listing-file-extension).

            -no, --no-output
                Process the input file but don't generate the output file (the <output file> argument,
                if specified, is ignored). This argument is the opposite of --do-output.

            -nsb, --no-show-banner
                Don't display the program title and copyright notice banner.

            -nsie, --no-source-in-errors
                Don't include the offending source code line in error messages (default).

            -nsw, --no-silence-warnings [<code>[,<code>[,...]]]
                Remove the specified warning codes from the list of warnings to silence
                as specified with --silence-warnings. If no codes are provided, 
                clear the list (so display all warnings again).

            -nsad, --no-show-assembly-duration
                Don't display the time took by the assembly process and the entire process.

            -nsap, --no-silence-assembly-print
                Display messages generated during assembly via .PRINTX, .PRINT, .PRINT1
                and .PRINT2 instructions (default).
            
            -nsx, --no-string-escapes
                Disallows escape sequences in the strings in source code.
                See "--string-escapes" for the escaping format.

                Escape sequences can also be turned on and off by using the .STRESC ON/OFF
                instruction directly in code. You may want to disallow escape sequences
                when compiling old sources that were intended to be assembled with MACRO-80.

            -nuse, --no-unknown-symbols-external
                Don't treat symbols that are still unknown at the end of pass 2 as external symbol
                references (throw "Unknown symbol" errors instead). This is the default behavior.

            -ofc, --output-file-case upper|lower|orig
                Whether the casing of the output file name will be converted to lower case,
                converted to upper case, or kept as the input file name (this is the default).
                This argument is used only when the name of the output file is chosen as 
                the same name of the output file (because no output file path is specified,
                or because the specified path is a directory).

                The default .BIN or .REL extension added to the output file name is affected by
                this argument, but an extension explicitly supplied with --output-file-extension
                is not. Example:

                N80 FILE.ASM --output-file-case lower --> generates file.bin or file.rel
                N80 FILE.ASM --output-file-case lower --output-file-extension COM --> generates file.COM

                This settings affects the casing of the generated listing file too
                if a --listing-file argument is supplied.

            -ofe, --output-file-extension [.]<extension>
                The extension for the generated output file. This value is used only when the
                name of the output file is chosen as the same name of the input file
                (because no output file path is specified, or because the specified path
                is a directory). Default is .BIN when the build type is absolute and .REL
                when the build type is relocatable.

            -rc, --reset-config
                Reset all the assembly configuration back to default values
                (in other words: ignore all the previous arguments except input and output files).

            -sad, --show-assembly-duration
                Display the time took by the assembly process and the entire process
                (only on successful completion).

            -sap, --silence-assembly-print
                Don't display messages generated during assembly via .PRINTX, .PRINT, .PRINT1
                and .PRINT2 instructions.

            -sb, --show-banner
                Display the program title and copyright notice banner (default).

            -se, --string-encoding <encoding>
                Default text encoding to be used to convert the strings found in the source code
                to sequences of bytes, it can be an encoding name or a codepage number.
                Run N80 with the --list-encodings argument to get a list of available encodings.
                Default is ASCII.

                This encoding can also be change directly in the source code by using the
                .STRENC instruction. In this case the special encoding name "default" (or "def")
                can be used to go back to the default encoding that was specified with this argument
                (or to ASCII if the argument was provided).

            -sie, --source-in-errors
                When displaying an error message include a copy of the complete source code line
                that generated the error. Only the first {AssemblyError.MAX_STORED_SOURCE_TEXT_LENGTH} characters of the line will be shown.

            -sw, --silence-warnings [<code>[,<code>[,...]]]
                Don't display the warnings with the specified codes.
                If no codes are specified, don't display any warning at all.
                Use --verbosity with a level of at least 2 to see the codes
                of the generated warnings.

            -sx, --string-escapes
                Allows escape sequences in the strings in source code (this is the default).

                When escape sequences are turned on, double quote (") delimited strings
                can contain any of the escape sequences supported by .NET, e.g.
                \r or \unnnn; note that the backslash and the double quote characters
                themselves will always need to be escaped (\" or \\);
                e.g. db "The \"perfect\" code\r\nis this one".
                
                When escape sequences are turned off, the only escape sequence allowed in 
                double quote delimited strings will be doubling the double quotes;
                e.g. db "The ""perfect"" code".

                Single quote (') delimited strings only support escaping the single quotes
                (by doubling them) regardless of string escaping being allowed or not;
                e.g. db 'The "perfect" code can''t be wrong'.

                Escape sequences can also be turned on and off by using the .STRESC ON/OFF
                instruction directly in code. You may want to disallow escape sequences
                when compiling old sources that were intended to be assembled with MACRO-80.

            -use, --unknown-symbols-external
                Treat any symbol that is still unknown at the end of pass 2 as an external
                symbol reference. This argument doesn't have any effect when the build type
                is absolute.

            -vb, --verbosity <level>
                Selects the verbosity of the status messages shown during the assembly process.
                The information shown for each level is as follows (each level includes the
                information from all the previous levels):
            
                0: Nothing.
                1: Input and output file paths, assembly succeeded or not, output file size (default).
                2: Pass 2 started, build type automatically selected.
                   Also warnings will include their code (useful for --silence-warnings),
                   and warnings that were already printed in pass 1 will be printed in pass 2
                   again (except when they are inside an IF1 block or similar).
                3: All the command line arguments from all the sources, all the directories
                   for INCLUDE and INCBIN, all the predefined symbols, the entire configuration
                   resulting from applying all the arguments.
            
                To silence other types of output see: --silence-warnings, --no-show-banner,
                --silence-assembly-print, --no-show-assembly-duration.

            Nestor80 exit codes are:

                0: Success
                1: Invalid arguments
                2: Error opening or reading the input file
                3: Error creating or writing to the output file
                4: Error creating or writing to the listing file
                5: Assembly errors were thrown
                6: An assembly fatal error was thrown

            Full documentation (and donation links, wink wink):
            https://github.com/Konamiman/Nestor80
            """;
    }
}