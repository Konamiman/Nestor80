namespace Konamiman.Nestor80.N80
{
    internal partial class Program
    {
        static readonly string bannerText = """
            Nestor80 - The Z80 assembler for the 21th century
            (c) Konamiman 2023

            """;

        static readonly string versionText = "1.0";

        static readonly string simpleHelpText = """
            Usage: N80 <source file> [<output file>] [<arguments>]
                   N80 -v|--version
                   N80 -h|--help
            """;

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
            
                If the path is just '$' then the file will be geenerated in
                the directory of the source file, and the file name will be
                as when the argument is omitted.

            Arguments can be specified as follows (and are combined in that order):
            
            - A N80_ARGS environment variable (can be disabled with --no-env-args)
            - A .N80 file in the same directory of the input file
              (can be disabled with --no-default-file-args); see --arguments-file
              for the file contents format.
            - The command line
            - Argument files (with --argument-file)
            
            If you need to include a space as part of an argument (e.g. a directory name) 
            in N80_ARGS or in an arguments file, escape it with a backslash (so "\ "
            to represent " "). Escaping for command line arguments depends on your shell.

            Available arguments:

            -abe, --allow-bare-expressions
                Treat a line containing one or more bare expressions as a DB line
                (e.g. the line 'FOO: 1,2,3,4' will be equivalent to 'FOO: db 1,2,3,4').
                You may need this when compiling old code intended for Macro80, but
                using bare expressions is otherwise not recommended because it masks
                mistyped instructions as "symbol not found" errors.

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
            
            -bt, --build-type abs|rel|auto
                The type of output to build. Default is auto.
            
                In auto mode the build type will be set as automatic if an ORG statement is found
                in the code before a CPU instruction, a label defined as public with "::", or any
                of the following instructions: CSEG, DSEG, COMMON, DB, DW, DS, DC, DM, DS,
                PUBLIC, EXTRN, .REQUEST; otherwise the build type will be set as relocatable.

                The --org-as-phase argument will also set the build type to absolute.

            -cid, --clear-include-directories
                Clear the list of the directories where relative INCLUDEd files
                will be searched for. If this argument isn't followed by any
                instance of --include-directory, any INCLUDE instruction
                referencing a relative file will fail.

            -co, --color-output
                Display assembly process messages and errors in color (default).

            -cpu, --default-cpu
                Set the target CPU of the source code (default is Z80).
                The target CPU can also be changed in code with the .CPU instruction.

            -dfa, --default-file-args
                Read arguments from the .N80 file in the directory of the input file (default).
                This argument is ignored when found inside an arguments file (.N80 or any other).
            
            -ds, --define-symbols <symbol>[=<value>][,<symbol>[=<value>][,...]]
                Predefine symbols for the assembled program.
                The default value if no <value> is provided is FFFFh. 
                The symbols will be created as if they had been defined with DEFL,
                therefore they can be redefined in the source code using the same instruction.

                Example: -ds symbol1,symbol2=1234,symbol3=ABCDh

            -id, --include-directory <directory path>
                By default relative paths referenced in INCLUDE instructions will be
                considered to be relative to the input file. This argument allows to
                specify an extra directory where INCLUDEd files will be searched for;
                use the argument multiple times to add more than one directory.
                Directories are scanned in the order they are declared.

                If <directory path> isn't absolute it will be considered relative
                to the current directory.

                If <directory path> starts with '$/' it will be considered relative to
                the directory of the source file.

                If <directory path> is just '$' then it's the directory of the source file
                (already included by default, but you may need to re-add it if you have
                removed it with --clear-include-directories).

            -ids, --initialize-defs
                This argument has effect only when the build type is relocatable. It will cause areas
                defined with "DEFS <size>" statements to be initialized with zeros, that is, these
                will be treated as equivalent to "DEFS <size>,0".

            -ie, --input-encoding <encoding>
                Text encoding of the source file, it can be an encoding name or a codepage number.
                Default is UTF-8.

            -me, --max-errors <count>
                Stop the assembly process after reaching the specified number of errors
                (not including warnings, and the process will still stop on the first fatal error).
                0 means "infinite". Default: {DEFAULT_MAX_ERRORS}.

            -nabe, --no-allow-bare-expressions
                Don't treat a line containing a list of comma-separated expressions as a DB line
                (so e.g. 'FOO: db 1,2,3,4' will throw an error). This is the default behavior.

            -no, --no-output
                Process the input file but don't generate the output file.

            -nco, --no-color-output
                Don't display assembly process messages and errors in color.

            -ndfa, --no-default-file-args
                Don't read arguments from the .N80 file in the directory of the input file.
                This argument is ignored when found inside an arguments file (.N80 or any other).

            -nds, --no-define-symbols
                Forget all symbols that had been predefined with --define-symbols.

            -nea, --no-env-args
                Don't read arguments from the N80_ARGS environment variable.
                This argument is ignored when found inside N80_ARGS or an arguments file
                (.N80 or any other).

            -nids, --no-initialize-defs
                This argument has effect only when the build type is relocatable. It will cause areas
                defined with "DEFS <size>" statements to not be initialized (the DEFS instruction
                will be treated as equivalent to "ORG $+<size>"). This is the default behavior.

            -noap, --no-org-as-phase
                Don't treat ORG statements as .PHASE statements (default).

            -nsb, --no-show-banner
                Don't display the program title and copyright notice banner.

            -nsw, --no-silence-warnings [<code>[,<code>[,...]]]
                Remove the specified warning codes from the list of warnings to silence
                as specified with --silence-warnings. If no codes are provided, 
                clear the list (so display all warnings again).

            -nsad, --no-show-assembly-duration
                Don't display the time that took the assembly process and the entire process.

            -nsap, --no-silence-assembly-print
                Display messages generated during assembly via .PRINTX, .PRINT, .PRINT1
                and .PRINT2 instructions (default).

            -nsx, --no-string-escapes
                Disallows escape sequences in the strings in source code.
                See "--string-escapes" for the escaping format.

                Escape sequences can also be turned on and off by using the .STRESC ON/OFF
                instruction directly in code. You may want to disallow escape sequences
                when compiling old sources that were intended to be assembled with Macro80.

            -oap, --org-as-phase
                Treat ORG statements as .PHASE statements. This argument has effect only
                when the build type is absolute (and in fact, it sets the build type
                to absolute by itself).

                The effect of this argument is that all the generated output will be written
                consecutively to the output file, regardless of location in memory.
                Example:

                org 100
                db 1,2,3,4
                org 110
                db 5,6,7,8
                org 102
                db 9,10

                Output without this argument: 1,2,9,10,0,0,0,0,0,0,5,6,7,8
                Output with this argument: 1,2,3,4,5,6,7,8,9,10

            -ofe, --output-file-extension [.]<extension>
                The extension for the generated output file. This value is used only when the
                name of the output file is chosen as the same name of the output file
                (because no output file path is specified, or because the specified path
                is a directory). Default is .BIN when the build type is absolute and .REL
                when the build type is relocatable.

            -rc, --reset-config
                Reset all the assembly configuration back to default values
                (in other words: ignore all the previous arguments except input and output files).

            -sad, --show-assembly-duration
                Display the time that took the assembly process and the entire process
                (only on successful completion).

            -sap, --silence-assembly-print
                Don't display messages generated during assembly via .PRINTX, .PRINT, .PRINT1
                and .PRINT2 instructions.

            -sb, --show-banner
                Display the program title and copyright notice banner (default).

            -se, --string-encoding
                Default text encoding to be used to convert the strings found in the source code
                to sequences of bytes, it can be an encoding name or a codepage number.
                Default is ASCII.

                This encoding can also be change directly in the source code by using the
                .STRENC instruction. In this case the special encoding name "default" (or "def")
                can be used to go back to the default encoding that was specified with this argument
                (or to ASCII if the argument was provided).

            -sv, --status-verbosity <level>
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
                   for INCLUDE, all the predefined symbols, the entire configuration resulting
                   from applying all the arguments.

                To silence other types of output see: --silence-warnings, --no-show-banner,
                --silence-assembly-print, --no-show-assembly-duration.

            -sw, --silence-warnings [<code>[,<code>[,...]]]
                Don't display the warnings with the specified codes.
                If no codes are specified, don't display any warning at all.
                Use --status-verbosity with a level of at least 2 to see the codes
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
                when compiling old sources that were intended to be assembled with Macro80.

            Full documentation (and donation links, wink wink):
            https://github.com/Konamiman/Nestor80
            """;
    }
}