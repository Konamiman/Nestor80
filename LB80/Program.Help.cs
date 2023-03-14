namespace Konamiman.Nestor80.LB80;

internal partial class Program
{
    static readonly string bannerText = """
        Libstor80 - The Z80 library manager for the 21th century
        (c) Konamiman 2023

        """;

    static readonly string simpleHelpText = """
        Usage: LB80 [<common arguments>] <command> <library file> [<command arguments>]
               LB80 -v|--version
               LB80 -h|--help
        """;

    /// <summary>
    /// Full help text, displayed when the program is run with the -h or --help argument.
    /// </summary>
    static readonly string extendedHelpText = $$$"""

    Common arguments can be specified as follows (and are combined in that order):
        
    - A LB80_ARGS environment variable (can be disabled with --no-env-args).
    - The command line, these must all come before the command name.

    If you need to include a space as part of an argument (e.g. a directory name) 
    in LB80_ARGS, escape it with a backslash (so "\ " to represent " ").
    Escaping for command line arguments depends on your shell.

    Common arguments must come before the command name. All these arguments are optional,
    and some apply only to certain commands (and are ignored otherwise).

    A library file consists of one or more relocatable programs. Each program
    has a name that is taken from a "Program name" link item, this type of link item
    is created by Nestor80 from the name given in a "name('program_name')" instruction,
    or from a "TITLE Program title" instruction, or from the source file name, 
    whichever is found first.

    Library files an relocatable files share the same internal format. The only
    difference as far as this documentation is concerned is that a relocatable file
    contains one single program, while a library file contains (usually) more
    than one program. Other library files can be passed to all commands with
    arguments of type <relocatable file>.

    All relative file paths are considered relative to the working directory, which
    can be specified with the --working-dir argument and is the current directory
    by default.


    COMMANDS


    LB80 [<arguments>] c|create <library file> <relocatable file> [<relocatable file>...]

        Creates a new library file from the contents of at least one (usually two or more)
        relocatable files. The programs are appended to the new library file in the order
        in which they are found in the relocatable/library files, and files are processed
        in the order in which they appear in the command line. If the library file already
        exists it will be deleted first.

        Two or more programs having the same name will cause the command to fail
        unless the --allow-duplicates argument is specified.

        Example: L80 create MATH.LIB SUM.REL SUBSTR.REL

    
    LB80 [<arguments>] a|add <library file> <relocatable file> [<relocatable file>...]

        Modifies an existing library file by appending additional programs to it.
        As in the case of "create" the programs are appended to the new library file 
        in the order in which they are found in the relocatable/library files, and files
        are processed in the order in which they appear in the command line.

        Two or more or the newly appended programs having the same name, or one or more
        of the newly appended programs having the same name as one of the programs already
        present in the library file, will cause the command to fail unless the 
        --allow-duplicates argument is specified.

        Example: L80 add MATH.LIB MULT.REL DIV.REL


    LB80 [<arguments>] s|set <library file> <relocatable file> [<relocatable file>...]

        Modifies an existing library file by either appending additional programs to it,
        or replacing existing programs.

        For each program found in the specified relocatable files the following action
        is performed: if the program already exists in the library file, the existing
        program is replaced with the new one; otherwise the program is appended to the
        library file (like the "add" command does). Programs are identified by their names,
        in a case-insensitive way.

        If the library contains more than one program with a given name (because
        --allow-duplicates was used when creating the library or when adding programs
        to it), only the first one will be replaced.

        Example: L80 set MATH.LIB MULTv2.REL POW.REL SQRT.REL


    LB80 [<arguments>] r|remove <library file> <program name> [<program name>...]

        Modifies an existing library file by removing one or more programs from it.
        Programs are identified by their names, in a case-insensitive way. Program
        names corresponding to programs not present in the library file are ignored.

        If the library contains more than one program with a given name (because
        --allow-duplicates was used when creating the library or when adding programs
        to it), only the first one will be removed.

        If the command would cause all the existing programs in the library file to be
        removed, the library file is simply deleted.

        Example: L80 remove MATH.LIB POWER SQROOT


    LB80 [<arguments>] e|extract <library file> <program name> [<relocatable file path>]

        Extracts one program from a library file into a separate relocatable file.
        If the relocatable file already exists it will be deleted first.
        Programs are identified by their names, in a case-insensitive way.

        If no relocatable file path is specified, a file with the name of the program
        and .REL extension will be created in the working directory. A different 
        extension can be specified with the --file-extension argument.

        If the library contains more than one program with the specified name (because
        --allow-duplicates was used when creating the library or when adding programs to it),
        the first of the found programs will be extracted.

        If you want to create a new library file with a subset of the programs present
        in another existing library file, use the "extract" command once per existing 
        program, then use "create" to create the new library file.

        Example: L80 extract MATH.LIB POWER POW.REL      --> creates POW.REL
                 L80 --file-extension rel MATH.LIB POWER --> creates POWER.rel
        

    LB80 [<arguments>] v|view <library file>

        List the programs in an existing library file. For each program the following
        information is printed: program name, size of code and data segments,
        name and size of common blocks, name of value of public symbols, external
        symbol references.

        Example: L80 view MATH.LIB


    LB80 [<arguments>] d|dump <library file>
    
        Dumps the entire contents of an existing library file, this includes all the
        raw byte sequences and all the link items in all the programs in the file.
        This command can be considered as a (very) verbose version of "view", and won't
        generally be useful to you unless you are developing an assembler or a linker.
    
        Example: L80 dump MATH.LIB
    

    COMMON ARGUMENTS


    Reminder: all the common arguments in the command line must come before the command name.

    -ad, --allow-duplicates
        This argument is only used with the "create" and "add" commands, it allows the
        library file to contain multiple programs with the same name (program names
        are compared in a case-insensitive way).

    -co, --color-output
        Display error messages in color (default).

    -fe, --file-extension [.]<extension>
        This argument is only used with the "extract" command, and only when no explicit
        name for the relocatable file to be created is given. It specifies the extension
        for the file that will be created, default is .REL
    
    -nad, --no-allow-duplicates
        This argument is only used with the "create" and "add" commands, it forces
        an error if the operation would cause the library file to contain multiple programs
        with the same name, with the exception of programs already present in the library file
        for the "add" command (program names are compared in a case-insensitive way).
        This is the default behavior.

    -nco, --no-color-output
        Don't display error messages and errors in color.
    
    -nea, --no-env-args
        Instructs Libstor80 to ignore the LB80_ARGS environment variable and thus to process
        arguments exclusively from the command line. This argument is ignored when found
        inside LB80_ARGS itself.

    -nsb, --no-show-banner
        Don't display the program title and copyright notice banner.

    -rc, --reset-config
        Resets all the arguments back to default values (in other words: ignores all the
        previous arguments and starts over).

    -sb, --show-banner
        Display the program title and copyright notice banner (default).

    -vb, --verbosity <level>
        Selects the verbosity of the status messages shown during the process.
        The information shown for each level is as follows (each level includes the
        information from all the previous levels):
        
        0: Nothing. See also --no-show-banner.
        1: Path of the library file being created, dumped or modified (default).
        2: All the command line arguments from all the sources, the entire configuration
           resulting from applying all the arguments, relocatable file paths as they
           are read, all the involved program names.

    -w, --working-dir <path>
        Specifies the base directory for all the relative path names specified as command
        arguments, both for files to be read and for files to be created or modified.
        The default value is the current directory.


    Libstor80 exit codes are:
        
        0: Success
        1: Invalid arguments
        2: Error opening or reading a library file or a relocatable file
        3: Error creating or modifying the library file
        4: Duplicate programs found when creating or adding content to a library file
        5: Program not found when extracting a program from a library file
        6: Fatal error

    Full documentation (and donation links, wink wink):
    https://github.com/Konamiman/Nestor80
    """;
}
