using Konamiman.Nestor80.Assembler.Errors;
using Konamiman.Nestor80.Assembler.Expressions;
using Konamiman.Nestor80.Assembler.Infrastructure;
using Konamiman.Nestor80.Assembler.Output;
using Konamiman.Nestor80.Assembler.Relocatable;
using System.Text.RegularExpressions;

namespace Konamiman.Nestor80.Assembler
{
    //This file contains the code that processes source lines representing CPU instructions.

    public partial class AssemblySourceProcessor
    {
        private static readonly Regex ixPlusArgumentRegex = new(@"^(\(\s*IX\s*[+-].+\))|(.+\(\s*IX\s*\))$", RegxOp);
        private static readonly Regex iyPlusArgumentRegex = new(@"^(\(\s*IY\s*[+-].+\))|(.+\(\s*IY\s*\))$", RegxOp);
        private static readonly Regex pcPlusArgumentRegex = new(@"^\(\s*PC\s*[+-].+\)$", RegxOp);
        private static readonly Regex spPlusArgumentRegex = new(@"^\(\s*SP\s*[+-].+\)$", RegxOp);
        private static readonly Regex hlPlusArgumentRegex = new(@"^\(\s*HL\s*[+-].+\)$", RegxOp);
        private static readonly Regex registerPlusArgumentRegex = new(@"^(\(\s*(IX|IY|PC|SP|HL)\s*(?<sign>[+-])(?<expression>.+)\))|((?<sign>[+-])?(?<expression>.+)\s*\(\s*(IX|IY)\s*\))$", RegxOp);
        private static readonly Regex z80MemPointedByRegisterRegex = new(@"^\(\s*(?<reg>HL|DE|BC|IX|IY|SP|C)\s*\)$", RegxOp);
        private static readonly Regex z280MemPointedByRegisterRegex = new(@"^\(\s*(?<reg>HL|DE|BC|IX|IY|PC|SP|C|IX *\+ *IY|HL *\+ *IX|HL *\+ *IY)\s*\)$", RegxOp);
        private static readonly Regex registerRegex = new(@"^[A-Z]{1,3}$", RegxOp);

        private static readonly Dictionary<CpuInstrArgType, string> indexRegisterNames = new() {
            { CpuInstrArgType.IxOffset, "IX" },
            { CpuInstrArgType.IyOffset, "IY" },
            { CpuInstrArgType.PcOffset, "PC" },
            { CpuInstrArgType.SpOffset, "SP" },
            { CpuInstrArgType.HlOffset, "HL" }
        };

        private static readonly string[] z280SpecialFixedValues = new[] { "DEHL", "IX+IY", "HL+IX", "HL+IY" };

        private static readonly Dictionary<CpuParsedArgType, byte[]> Z80ldIxyByteInstructions = new() {
            { CpuParsedArgType.IxPlusOffset, new byte[] { 0xdd, 0x36, 0, 0 } },
            { CpuParsedArgType.IyPlusOffset, new byte[] { 0xfd, 0x36, 0, 0 } }
        };

        private static readonly Dictionary<CpuParsedArgType, byte[]> Z280ldOffsetByteInstructions = new() {
            { CpuParsedArgType.NumberInParenthesis, new byte[] { 0xdd, 0x3e, 0, 0, 0 } },
            { CpuParsedArgType.IxPlusOffset, new byte[] { 0xfd, 0x0e, 0, 0, 0 } },
            { CpuParsedArgType.IyPlusOffset, new byte[] { 0xfd, 0x16, 0, 0, 0 } },
            { CpuParsedArgType.PcPlusOffset, new byte[] { 0xfd, 0x06, 0, 0, 0 } },
            { CpuParsedArgType.SpPlusOffset, new byte[] { 0xdd, 0x06, 0, 0, 0 } },
            { CpuParsedArgType.HlPlusOffset, new byte[] { 0xfd, 0x1e, 0, 0, 0 } }
        };

        private static readonly Dictionary<CpuParsedArgType, byte[]> Z280ldwOffsetByteInstructions = new() {
            { CpuParsedArgType.NumberInParenthesis, new byte[] { 0xdd, 0x11, 0, 0, 0, 0 } },
            { CpuParsedArgType.PcPlusOffset, new byte[] { 0xdd, 0x31, 0, 0, 0, 0 } },
        };

        private static readonly Dictionary<CpuParsedArgType, CpuInstrArgType> instrArgTypeByParsedType = new() {
            { CpuParsedArgType.IxPlusOffset, CpuInstrArgType.IxOffset },
            { CpuParsedArgType.IyPlusOffset, CpuInstrArgType.IyOffset },
            { CpuParsedArgType.PcPlusOffset, CpuInstrArgType.PcOffset },
            { CpuParsedArgType.SpPlusOffset, CpuInstrArgType.SpOffset },
            { CpuParsedArgType.HlPlusOffset, CpuInstrArgType.HlOffset },
            { CpuParsedArgType.NumberInParenthesis, CpuInstrArgType.WordInParenthesis },
        };

        private static readonly CpuInstrArgType[] z280FirstArgTypesForInstructionsWithTwoVariableArguments = new[] {
            CpuInstrArgType.IxOffset,
            CpuInstrArgType.IxOffsetLong,
            CpuInstrArgType.IyOffset,
            CpuInstrArgType.IyOffsetLong,
            CpuInstrArgType.PcOffset,
            CpuInstrArgType.SpOffset,
            CpuInstrArgType.HlOffset,
            CpuInstrArgType.WordInParenthesis
        };

        private static readonly CpuInstrArgType[] wordArgTypes = new[] {
            CpuInstrArgType.Word,
            CpuInstrArgType.WordInParenthesis,
            CpuInstrArgType.IxOffsetLong,
            CpuInstrArgType.IyOffsetLong,
            CpuInstrArgType.HlOffset,
            CpuInstrArgType.PcOffset,
            CpuInstrArgType.SpOffset
        };

        /// <summary>
        /// Process a source line that represents a CPU instruction.
        /// </summary>
        /// <param name="opcode">The instruction opcode.</param>
        /// <param name="walker">A walker to be used to retrieve the remaining of the source line, pointing past the opcode.</param>
        /// <returns>The result of processing the source line.</returns>
        /// <exception cref="Exception"></exception>
        private static ProcessedSourceLine ProcessCpuInstruction(string opcode, SourceLineWalker walker)
        {
            byte[] instructionBytes = null;
            CpuInstructionLine instructionLine = null;
            bool isNegativeIxy = false;

            static string RemoveSpacesAroundParenthesis(string argument)
            {
                if(argument[0] is '(' && argument.Length > 1 && argument[^1] is ')') {
                    if(isZ280 && z280MemPointedByRegisterRegex.IsMatch(argument)) {
                        argument = $"({argument[1..^1].Replace(" ", "")})";
                    }
                    else {
                        argument = $"({argument[1..^1].Trim()})";
                    }
                    
                }
                return argument;
            }

            string firstArgument = null, secondArgument = null;
            if(!walker.AtEndOfLine) {
                firstArgument = RemoveSpacesAroundParenthesis(walker.ExtractExpression());
                if(!walker.AtEndOfLine) {
                    secondArgument = RemoveSpacesAroundParenthesis(walker.ExtractExpression());
                    if(secondArgument.Equals("AF'", StringComparison.OrdinalIgnoreCase)) {
                        secondArgument = "AF";
                    }
                }
            }

            // First let's see if it's an instruction with no variable arguments.

            string instructionSearchKey = null;
            if(firstArgument is null) {
                instructionSearchKey = opcode;
            }
            else if(secondArgument is null) {
                instructionSearchKey = $"{opcode} {firstArgument}";
            }
            else {
                instructionSearchKey = $"{opcode} {firstArgument},{secondArgument}";
            }

            if(isZ280 && ZeroIndexExclusiveZ280Instructions.ContainsKey(instructionSearchKey) && !FixedZ80Instructions.ContainsKey(instructionSearchKey)) {
                var line = new CpuInstructionLine() {
                    FirstArgumentTemplate = firstArgument,
                    SecondArgumentTemplate = secondArgument,
                    OutputBytes =
                        isZ280LongIndexMode && !isZ280AutoIndexMode && ZeroIndexExclusiveZ280Instructions[instructionSearchKey].Item2 is not null ?
                        ZeroIndexExclusiveZ280Instructions[instructionSearchKey].Item2 : ZeroIndexExclusiveZ280Instructions[instructionSearchKey].Item1
                };
                CompleteInstructionLine(line);
                return line;
            }

            if(isZ280 && !z280AllowPriviliegedInstructions && (Z280PrivilegedInstructions.Contains(opcode, StringComparer.OrdinalIgnoreCase) || Z280PrivilegedLdInstructions.Contains(instructionSearchKey, StringComparer.OrdinalIgnoreCase))) {
                AddError(AssemblyErrorCode.PrivilegedInstructionFound, $"Privileged instructions aren't allowed when {Z280_ALLOW_PRIV_SYMBOL} equals 0, found: {instructionSearchKey.ToUpper()}");
                walker.DiscardRemaining();
                return new CpuInstructionLine() { IsInvalid = true };
            }

            if(isZ280 && !z280AllowPriviliegedInstructions && z280IoInstructionsArePrivileged && Z280IOInstructions.Contains(opcode, StringComparer.OrdinalIgnoreCase)) {
                AddError(AssemblyErrorCode.IoInstructionFound, $"I/O instructions aren't allowed when {Z280_IO_IS_PRIVILEGED_SYMBOL} equals 0, found: {instructionSearchKey.ToUpper()}");
                walker.DiscardRemaining();
                return new CpuInstructionLine() { IsInvalid = true };
            }

            if(isZ280 && opcode.Equals("SLL", StringComparison.OrdinalIgnoreCase)) {
                AddError(AssemblyErrorCode.InvalidCpuInstruction, "SLL is an undocumented Z80 instruction that isn't available in the Z280");
                walker.DiscardRemaining();
                return new CpuInstructionLine() { IsInvalid = true };
            }

            if(isZ280 && instructionSearchKey.Equals("OUT F,(C)", StringComparison.OrdinalIgnoreCase)) {
                AddError(AssemblyErrorCode.InvalidCpuInstruction, "OUT F,(C) is an undocumented Z80 instruction that isn't available in the Z280");
                walker.DiscardRemaining();
                return new CpuInstructionLine() { IsInvalid = true };
            }

            if(currentCpuFixedInstructions.ContainsKey(instructionSearchKey)) {
                var line = new CpuInstructionLine() { FirstArgumentTemplate = firstArgument, SecondArgumentTemplate = secondArgument, OutputBytes = currentCpuFixedInstructions[instructionSearchKey] };
                CompleteInstructionLine(line);
                return line;
            }

            // There's (or there should be) at least one variable argument.

            var firstArgumentType = GetCpuInstructionArgumentPattern(firstArgument);
            var secondArgumentType = GetCpuInstructionArgumentPattern(secondArgument);

            if(firstArgumentType is CpuParsedArgType.Fixed && secondArgumentType is CpuParsedArgType.Fixed or CpuParsedArgType.None) {
                AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid {currentCpu} instruction: {instructionSearchKey}");
                walker.DiscardRemaining();
                return new CpuInstructionLine() { IsInvalid = true };
            }

            bool isInstructionWithTwoVariableArguments = false;
            var isLD = string.Equals("LD", opcode, StringComparison.OrdinalIgnoreCase);
            if(isZ280) {
                isInstructionWithTwoVariableArguments = 
                    (( isLD &&
                        firstArgumentType is CpuParsedArgType.IxPlusOffset or CpuParsedArgType.IyPlusOffset or CpuParsedArgType.PcPlusOffset or CpuParsedArgType.SpPlusOffset or CpuParsedArgType.HlPlusOffset or CpuParsedArgType.NumberInParenthesis
                    ) ||
                    (string.Equals("LDW", opcode, StringComparison.OrdinalIgnoreCase) &&
                        firstArgumentType is CpuParsedArgType.PcPlusOffset or CpuParsedArgType.NumberInParenthesis
                    ))
                    && secondArgumentType is CpuParsedArgType.Number;
            }
            else {
                isInstructionWithTwoVariableArguments = string.Equals("LD", opcode, StringComparison.OrdinalIgnoreCase) &&
                    firstArgumentType is CpuParsedArgType.IxPlusOffset or CpuParsedArgType.IyPlusOffset &&
                    secondArgumentType is CpuParsedArgType.Number;
            }

            if(isInstructionWithTwoVariableArguments) {

                // Treat the Z80 "LD (IX/IY+n),n" and the Z280 "LD (IX/IY/PC/SP/HL+nn),n", "LD (nn),n", "LDW (nn),nn" and "LDW (PC+nn),nn"
                // instructions as special cases since these are the only ones with two variable arguments.
                // For simplicity we won't try to evaluate the expressions in this case.

                string expression1Text;
                if(firstArgumentType is CpuParsedArgType.NumberInParenthesis) {
                    expression1Text = firstArgument[1..^1].Trim();
                    isNegativeIxy = false;
                }
                else {
                    (expression1Text, isNegativeIxy) = GetExpressionAndSignFromIndexArgument(firstArgument);
                }

                var argument1Expression = GetExpressionForInstructionArgument(opcode, expression1Text, !isZ280);
                if(argument1Expression is null) {
                    return new CpuInstructionLine() { IsInvalid = true };
                }

                // Since the expression will be cached we need to register all the referenced symbols now,
                // even if they are unknown so far, so that in pass 2 the processing of pending expressions
                // will work (it would throw a null reference otherwise when attempting state.GetSymbol);
                // GetSymbolForExpression will effectively register the symbol if it's still unknown.
                foreach(var s in argument1Expression.ReferencedSymbols) {
                    GetSymbolForExpression(s.SymbolName, s.IsExternal, s.IsRoot);
                }

                var argument2Expression = GetExpressionForInstructionArgument(opcode, secondArgument, isLD);
                if(argument2Expression is null) {
                    return new CpuInstructionLine() { IsInvalid = true };
                }

                foreach(var s in argument2Expression.ReferencedSymbols) {
                    GetSymbolForExpression(s.SymbolName, s.IsExternal, s.IsRoot);
                }

                int secondArgBytePosition;
                CpuInstrArgType firstArgumentInstrArgType;
                var isShortLD = isLD && firstArgumentType is CpuParsedArgType.IxPlusOffset or CpuParsedArgType.IyPlusOffset && !UseLongIndexVersionOfInstruction(argument1Expression);
                if(!isZ280 || isShortLD) {
                    secondArgBytePosition = 3;
                    firstArgumentInstrArgType = instrArgTypeByParsedType[firstArgumentType];
                    instructionBytes = Z80ldIxyByteInstructions[firstArgumentType].ToArray();
                }
                else if(isLD) {
                    secondArgBytePosition = firstArgumentType is CpuParsedArgType.NumberInParenthesis ? 4 : 4;
                    firstArgumentInstrArgType = instrArgTypeByParsedType[firstArgumentType];
                    if(firstArgumentInstrArgType is CpuInstrArgType.IxOffset) {
                        firstArgumentInstrArgType = CpuInstrArgType.IxOffsetLong;
                    }
                    else if(firstArgumentInstrArgType is CpuInstrArgType.IyOffset) {
                        firstArgumentInstrArgType = CpuInstrArgType.IyOffsetLong;
                    }
                    instructionBytes = Z280ldOffsetByteInstructions[firstArgumentType].ToArray();
                }
                else {
                    secondArgBytePosition = 4;
                    firstArgumentInstrArgType = firstArgumentType is CpuParsedArgType.NumberInParenthesis ? CpuInstrArgType.WordInParenthesis : CpuInstrArgType.PcOffset;
                    instructionBytes = Z280ldwOffsetByteInstructions[firstArgumentType];
                }

                instructionLine = new CpuInstructionLine() { FirstArgumentTemplate = firstArgument, SecondArgumentTemplate = secondArgument, OutputBytes = instructionBytes };
                //TODO: Use AdjustInstructionLineForExpression twice, see if it works
                state.RegisterPendingExpression(
                    instructionLine,
                    argument1Expression,
                    location: 2,
                    argumentType: firstArgumentInstrArgType,
                    isNegativeIxy: isNegativeIxy
                );

                state.RegisterPendingExpression(
                    instructionLine,
                    argument2Expression,
                    location: secondArgBytePosition,
                    argumentType: isLD ? CpuInstrArgType.Byte : CpuInstrArgType.Word
                );

                CompleteInstructionLine(instructionLine);
                return instructionLine;
            }

            else if(currentCpuInstructionsWithSelectorValue.ContainsKey(opcode)) {
                // Found an instruction whose first argument is one of a fixed set (IM, RST, BIT, SET, RES).
                // These need special treatment because:
                // 1. This special argument doesn't directly translate to a byte or word in the output bytes; and
                // 2. This special argument could be unknown at pass 1 and thus we need to select a dummy instruction
                //    (for the location counter to update properly) and defer the selection of the real instruction to pass 2.

                if(firstArgumentType is not CpuParsedArgType.Number and not CpuParsedArgType.NumberInParenthesis) {
                    AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument(s) for {currentCpu} instruction {opcode.ToUpper()}");
                    walker.DiscardRemaining();
                    return new CpuInstructionLine() { IsInvalid = true };
                }

                (string, byte[], ushort)[] candidates = null;
                if(secondArgumentType is CpuParsedArgType.None) {
                    candidates = currentCpuInstructionsWithSelectorValue[opcode].Where(c => c.Item1 is null).ToArray();
                }
                else if(secondArgumentType is CpuParsedArgType.Fixed) {
                    candidates = currentCpuInstructionsWithSelectorValue[opcode].Where(c => string.Equals(c.Item1, secondArgument, StringComparison.OrdinalIgnoreCase)).ToArray();
                }
                else if(secondArgumentType is CpuParsedArgType.IxPlusOffset) {
                    candidates = currentCpuInstructionsWithSelectorValue[opcode].Where(c => c.Item1[0] == 'x').ToArray();
                }
                else if(secondArgumentType is CpuParsedArgType.IyPlusOffset) {
                    candidates = currentCpuInstructionsWithSelectorValue[opcode].Where(c => c.Item1[0] == 'y').ToArray();
                }

                if(candidates is null || candidates.Length == 0) {
                    AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument(s) for {currentCpu} instruction {opcode.ToUpper()}");
                    walker.DiscardRemaining();
                    return new CpuInstructionLine() { IsInvalid = true };
                }

                (string, byte[], int) chosenInstruction;
                var selectorExpression = GetExpressionForInstructionArgument(opcode, firstArgument, true);
                Address selectorExpressionValue;
                try {
                    selectorExpressionValue = EvaluateIfNoSymbolsOrPass2(selectorExpression);
                }
                catch(InvalidExpressionException ex) {
                    AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument for {currentCpu} instruction {opcode.ToUpper()}: {ex.Message}");
                    walker.DiscardRemaining();
                    return new CpuInstructionLine() { IsInvalid = true };
                }
                instructionLine = new CpuInstructionLine() { Opcode = opcode, FirstArgumentTemplate = firstArgument, SecondArgumentTemplate = secondArgument };
                if(selectorExpressionValue is null) {
                    if(state.InPass1) {
                        chosenInstruction = candidates[0];
                    }
                    else {
                        AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument(s) for {currentCpu} instruction {opcode.ToUpper()}");
                        walker.DiscardRemaining();
                        return new CpuInstructionLine() { IsInvalid = true };
                    }
                }
                else {
                    chosenInstruction = candidates.FirstOrDefault(c => c.Item3 == selectorExpressionValue.Value);
                    if(chosenInstruction.Item2 is null) {
                        AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument(s) for {currentCpu} instruction {opcode.ToUpper()}");
                        walker.DiscardRemaining();
                        return new CpuInstructionLine() { IsInvalid = true };
                    }
                }
                instructionLine.OutputBytes = chosenInstruction.Item2.ToArray();
                
                if(chosenInstruction.Item1 is "x" or "y") {
                    (var secondArgumentExpressionText, isNegativeIxy) = GetExpressionAndSignFromIndexArgument(secondArgument);
                    var secondArgumentExpression = GetExpressionForInstructionArgument(opcode, secondArgumentExpressionText, true);
                    if(secondArgumentExpression is null) {
                        return new CpuInstructionLine() { IsInvalid = true };
                    }
                    if(!AdjustInstructionLineForExpression(instructionLine, secondArgumentExpression, 2, instrArgTypeByParsedType[secondArgumentType], isNegativeIxy)) {
                        return new CpuInstructionLine() { IsInvalid = true };
                    };
                }

                CompleteInstructionLine(instructionLine);
                return instructionLine;
            }

            instructionSearchKey = null;
            string variableArgument;
            var argSearchType = CpuParsedArgType.None;
            var argSearchPosition = CpuArgPos.None;

            // Search the instruction based on the opcode and the type of the supplied arguments.
            // If there are two arguments, one must be fixed (if it's an instruction with two variable arguments we'll have handled it already).

            if(secondArgument is null) {
                if(firstArgumentType is CpuParsedArgType.Fixed) {
                    throw new Exception($"{nameof(ProcessCpuInstruction)}: something went wrong: a fixed instruction argument was not processed as such.");
                }

                variableArgument = firstArgument;
                argSearchType = firstArgumentType;
                argSearchPosition = CpuArgPos.Single;
                instructionSearchKey = opcode;
            }
            else {
                if(firstArgumentType is not CpuParsedArgType.Fixed ^ secondArgumentType is not CpuParsedArgType.Fixed) {
                    if(firstArgumentType is CpuParsedArgType.Fixed) {
                        variableArgument = secondArgument;
                        argSearchType = secondArgumentType;
                        argSearchPosition = CpuArgPos.Second;
                        instructionSearchKey = $"{opcode} {firstArgument}";
                    }
                    else {
                        variableArgument = firstArgument;
                        argSearchType = firstArgumentType;
                        argSearchPosition = CpuArgPos.First;
                        instructionSearchKey = $"{opcode} {secondArgument}";
                    }
                }
                else {
                    AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument(s) for {currentCpu} instruction {opcode.ToUpper()}");
                    walker.DiscardRemaining();
                    return new CpuInstructionLine() { IsInvalid = true };
                }
            }

            if(instructionSearchKey is null) {
                AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument(s) for {currentCpu} instruction {opcode.ToUpper()}");
                walker.DiscardRemaining();
                return new CpuInstructionLine() { IsInvalid = true };
            }

            // Now that we have identified the instruction, deal with the expression for the variable argument
            // and generate either the full instruction or one needing expression evaluation in pass 2.

            int variableArgBytePosition = 0;
            var variableArgType = CpuInstrArgType.None;
            (string, CpuInstrArgType, CpuArgPos, byte[], int) candidateInstructionInfo;
            byte[] longIndexInstructionBytes = null;
            int longIndexInstructionBytePositionOfVariableArgument = 0;

            for(int i=0; i<currentCpuInstructionsWithOneVariableArgumentCount; i++) {
                if(isZ280 && i<ExclusiveZ280InstructionsWithOneVariableArgument.Length) {
                    var z280candidateInstructionInfo = ExclusiveZ280InstructionsWithOneVariableArgument[i];
                    candidateInstructionInfo = (z280candidateInstructionInfo.Item1, z280candidateInstructionInfo.Item2, z280candidateInstructionInfo.Item3, z280candidateInstructionInfo.Item4, z280candidateInstructionInfo.Item5);
                    longIndexInstructionBytes = z280candidateInstructionInfo.Item6;
                    longIndexInstructionBytePositionOfVariableArgument = z280candidateInstructionInfo.Item7;
                }
                else {
                    candidateInstructionInfo = Z80InstructionsWithOneVariableArgument[isZ280 ? i-ExclusiveZ280InstructionsWithOneVariableArgument.Length : i];
                    longIndexInstructionBytes = null;
                }

                if(!string.Equals(candidateInstructionInfo.Item1, instructionSearchKey, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                if(candidateInstructionInfo.Item3 != argSearchPosition) {
                    continue;
                }

                var instructionArgType = candidateInstructionInfo.Item2;
                var isMatch =
                    (argSearchType is CpuParsedArgType.NumberInParenthesis &&
                        instructionArgType is
                            CpuInstrArgType.ByteInParenthesis or
                            CpuInstrArgType.WordInParenthesis or
                            CpuInstrArgType.Byte or
                            CpuInstrArgType.Word or
                            CpuInstrArgType.OffsetFromCurrentLocation or
                            CpuInstrArgType.OffsetFromCurrentLocationMinusOne) ||
                    (argSearchType is CpuParsedArgType.Number &&
                        instructionArgType is
                            CpuInstrArgType.Byte or
                            CpuInstrArgType.Word or
                            CpuInstrArgType.OffsetFromCurrentLocation or
                            CpuInstrArgType.OffsetFromCurrentLocationMinusOne) ||
                    (argSearchType is CpuParsedArgType.IxPlusOffset && instructionArgType is CpuInstrArgType.IxOffset or CpuInstrArgType.IxOffsetLong) ||
                    (argSearchType is CpuParsedArgType.IyPlusOffset && instructionArgType is CpuInstrArgType.IyOffset or CpuInstrArgType.IyOffsetLong) ||
                    (argSearchType is CpuParsedArgType.PcPlusOffset && instructionArgType is CpuInstrArgType.PcOffset) ||
                    (argSearchType is CpuParsedArgType.SpPlusOffset && instructionArgType is CpuInstrArgType.SpOffset) ||
                    (argSearchType is CpuParsedArgType.HlPlusOffset && instructionArgType is CpuInstrArgType.HlOffset);

                if(isMatch) {
                    variableArgType = candidateInstructionInfo.Item2;
                    instructionBytes = candidateInstructionInfo.Item4.ToArray();
                    variableArgBytePosition = candidateInstructionInfo.Item5;
                    break;
                }
            }

            var forceByte = longIndexInstructionBytes != null && !isZ280LongIndexMode && !isZ280AutoIndexMode;
            var forceWord = longIndexInstructionBytes != null && isZ280LongIndexMode && !isZ280AutoIndexMode;
            if(forceWord) {
                instructionBytes = longIndexInstructionBytes;
                variableArgBytePosition = longIndexInstructionBytePositionOfVariableArgument;
                if(variableArgType is CpuInstrArgType.IxOffset) variableArgType = CpuInstrArgType.IxOffsetLong;
                if(variableArgType is CpuInstrArgType.IyOffset) variableArgType = CpuInstrArgType.IyOffsetLong;
            }

            if(variableArgType is CpuInstrArgType.None) {
                AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument(s) for {currentCpu} instruction {opcode.ToUpper()}");
                walker.DiscardRemaining();
                return new CpuInstructionLine() { IsInvalid = true };
            }

            isNegativeIxy = false;
            string expressionText = variableArgument;
            if(CpuInstrArgTypeIsIndex(variableArgType)) {
                (expressionText, isNegativeIxy) = GetExpressionAndSignFromIndexArgument(variableArgument);
            }

            var argumentExpression = GetExpressionForInstructionArgument(opcode, expressionText, forceByte || CpuInstrArgTypeIsByte(variableArgType));
            if(argumentExpression is null) {
                return new CpuInstructionLine() { IsInvalid = true };
            }

            if(isZ280AutoIndexMode && longIndexInstructionBytes != null && UseLongIndexVersionOfInstruction(argumentExpression)) {
                instructionBytes = longIndexInstructionBytes;
                variableArgBytePosition = longIndexInstructionBytePositionOfVariableArgument;
                if(variableArgType is CpuInstrArgType.IxOffset) variableArgType = CpuInstrArgType.IxOffsetLong;
                if(variableArgType is CpuInstrArgType.IyOffset) variableArgType = CpuInstrArgType.IyOffsetLong;
            }

            instructionLine = new CpuInstructionLine() { Opcode = opcode, FirstArgumentTemplate = firstArgument, SecondArgumentTemplate = secondArgument, Cpu = currentCpu, OutputBytes = instructionBytes };
            
            var adjustOk = AdjustInstructionLineForExpression(instructionLine, argumentExpression, variableArgBytePosition, variableArgType, isNegativeIxy);
            CompleteInstructionLine(instructionLine);
            if(!adjustOk) walker.DiscardRemaining();
            return adjustOk ? instructionLine : new CpuInstructionLine() { IsInvalid = true };
        }

        private static bool UseLongIndexVersionOfInstruction(Expression expression)
        {
            if(isZ280AutoIndexMode) {
                try {
                    var value = expression.TryEvaluate();
                    return (object)value == null || !value.IsValidByte || !value.IsAbsolute;
                }
                catch(ExpressionContainsExternalReferencesException) {
                    return true;
                }
            }

            return isZ280LongIndexMode;
        }

        private static bool CpuInstrArgTypeIsByte(CpuInstrArgType type) =>
            //Note that we aren't including CpuInstrArgType.OffsetFromCurrentLocation even though it's of byte type, that's on purpose.
            //We need expressions of that type to be always evaluated in pass 2 by AdjustInstructionLineForExpression, instead of
            //generating a relocatable expression with "store as byte";
            //the resulting offset will later be adjusted by ProcessArgumentForInstruction.
            type is CpuInstrArgType.Byte or CpuInstrArgType.ByteInParenthesis or CpuInstrArgType.IxOffset or CpuInstrArgType.IyOffset;

        private static bool CpuInstrArgTypeIsIndex(CpuInstrArgType type) =>
            type is CpuInstrArgType.IxOffset or CpuInstrArgType.IyOffset
                or CpuInstrArgType.IxOffsetLong or CpuInstrArgType.IyOffsetLong
                or CpuInstrArgType.PcOffset or CpuInstrArgType.SpOffset or CpuInstrArgType.HlOffset;

        private static void CompleteInstructionLine(CpuInstructionLine line)
        {
            IncreaseLocationPointer(line.OutputBytes.Length);
            line.Cpu = currentCpu;
            line.NewLocationArea = state.CurrentLocationArea;
            line.NewLocationCounter = state.CurrentLocationPointer;
            line.RelocatableParts ??= Array.Empty<RelocatableOutputPart>();
        }

        private static (string, bool) GetExpressionAndSignFromIndexArgument(string argument)
        {
            var match = registerPlusArgumentRegex.Match(argument);
            var ixRegisterSign = match.Groups["sign"].Success ? match.Groups["sign"].Value : "+";
            var expressionText = ixRegisterSign + match.Groups["expression"].Value;
            return (expressionText, ixRegisterSign == "-");
        }

        /// <summary>
        /// Given a variable argument for an instruction, and an expression that represents it,
        /// try to evaluate it and either use <see cref="ProcessArgumentForInstruction"/> to process the resulting value,
        /// register it as an expression pending evaluation, or register it as a relocatable address
        /// in the resulting instance of <see cref="ProcessedSourceLine"/>.
        /// </summary>
        /// <param name="line">The generated processed line.</param>
        /// <param name="argumentExpression">The expression representing the argument.</param>
        /// <param name="argBytePosition">The position of the argument in the byte array that the instruction produces.</param>
        /// <param name="argType">The argument type.</param>
        /// <param name="isNegativeIxy">True if the instruction is IX/IY with negative offset.</param>
        /// <returns></returns>
        private static bool AdjustInstructionLineForExpression(CpuInstructionLine line, Expression argumentExpression, int argBytePosition, CpuInstrArgType argType, bool isNegativeIxy = false)
        {
            Address variableArgumentValue;
            try {
                variableArgumentValue = EvaluateIfNoSymbolsOrPass2(argumentExpression);
            }
            catch(ExpressionContainsExternalReferencesException) {
                variableArgumentValue = null;
            }
            catch(InvalidExpressionException ex) {
                AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument for {currentCpu} instruction {line.Opcode.ToUpper()}: {ex.Message}");
                return false;
            }

            if(variableArgumentValue is null) {
                state.RegisterPendingExpression(
                    line,
                    argumentExpression,
                    location: argBytePosition,
                    argumentType: argType,
                    isNegativeIxy: isNegativeIxy
                );

                return true;
            }

            if(variableArgumentValue.IsAbsolute || argType is CpuInstrArgType.OffsetFromCurrentLocation or CpuInstrArgType.OffsetFromCurrentLocationMinusOne) {
                var instructionBytes = line.OutputBytes.ToArray();
                if(!ProcessArgumentForInstruction(line.Opcode, argType, instructionBytes, variableArgumentValue, argBytePosition, isNegativeIxy)) {
                    return false;
                }
                line.OutputBytes = instructionBytes;
                line.RelocatableParts = Array.Empty<RelocatableOutputPart>();
            }
            else {
                var relocatable = RelocatableFromAddress(
                    variableArgumentValue,
                    argBytePosition,
                    wordArgTypes.Contains(argType) ? 2 : 1,
                    argumentExpression.SdccAreaName
                );
                line.RelocatableParts = new[] { relocatable };
            }

            return true;
        }

        private static CpuParsedArgType GetCpuInstructionArgumentPattern(string argument)
        {
            if(argument is null)
                return CpuParsedArgType.None;

            var match = currentCpuMemPointedByRegisterRegex.Match(argument);
            if(match.Success) {
                return CpuParsedArgType.Fixed;
            }
            if(z80RegisterNames.Contains(argument, StringComparer.OrdinalIgnoreCase) || (isZ280 && argument.Equals("DEHL", StringComparison.OrdinalIgnoreCase))) {
                return CpuParsedArgType.Fixed;
            }
            if(ixPlusArgumentRegex.IsMatch(argument)) {
                return CpuParsedArgType.IxPlusOffset;
            }
            if(iyPlusArgumentRegex.IsMatch(argument)) {
                return CpuParsedArgType.IyPlusOffset;
            }
            if(pcPlusArgumentRegex.IsMatch(argument)) {
                return CpuParsedArgType.PcPlusOffset;
            }
            if(spPlusArgumentRegex.IsMatch(argument)) {
                return CpuParsedArgType.SpPlusOffset;
            }
            if(hlPlusArgumentRegex.IsMatch(argument)) {
                return CpuParsedArgType.HlPlusOffset;
            }

            if(argument[0] == '(') {
                return CpuParsedArgType.NumberInParenthesis;
            }

            return CpuParsedArgType.Number;
        }

        /// <summary>
        /// Given a variable argument for an instruction, and provided that the corresponding expression has been already evaluated,
        /// process and validate the resulting value taking in account special cases (such as DJNZ or JR offsets, IX/IY instruction offsets)
        /// and updating the generated instruction bytes as appropriate.
        /// </summary>
        /// <param name="opcode">Instruction opcode.</param>
        /// <param name="argumentType">Argument type.</param>
        /// <param name="instructionBytes">Instruction bytes to be updated.</param>
        /// <param name="value">Evaluated expression value.</param>
        /// <param name="position">Position of the argument in the instruction bytes array.</param>
        /// <param name="isNegativeIxy">True if the instruction is IX/IY with negative offset.</param>
        /// <returns>True on success, false on error.</returns>
        /// <exception cref="Exception"></exception>
        private static bool ProcessArgumentForInstruction(string opcode, CpuInstrArgType argumentType, byte[] instructionBytes, Address value, int position, bool isNegativeIxy = false)
        {
            if(argumentType is CpuInstrArgType.OffsetFromCurrentLocation or CpuInstrArgType.OffsetFromCurrentLocationMinusOne) {
                if(value.Type != state.CurrentLocationArea) {
                    AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument: the target address must be in the same area of the instruction");
                    return false;
                }
                var offset = value.Value - (state.CurrentLocationPointer + (argumentType is CpuInstrArgType.OffsetFromCurrentLocation ? 2 : 3));
                if(offset is < -128 or > 127) {
                    AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument: the target address is out of range");
                    return false;
                }

                instructionBytes[position] = (byte)offset;
                return true;
            }

            if(argumentType is CpuInstrArgType.Byte or CpuInstrArgType.ByteInParenthesis && value.IsAbsolute) {
                if(!value.IsValidByte) {
                    AddError(AssemblyErrorCode.InvalidExpression, $"Invalid argument for {opcode.ToUpper()}: {value.Value:X4}h is not a valid byte value");
                    return false;
                }
                instructionBytes[position] = value.ValueAsByte;
                return true;
            }

            else if(argumentType is CpuInstrArgType.Word or CpuInstrArgType.WordInParenthesis && value.IsAbsolute) {
                instructionBytes[position] = value.ValueAsByte;
                instructionBytes[position + 1] = (byte)((value.Value & 0xFF00) >> 8);
                return true;
            }

            else if(!CpuInstrArgTypeIsIndex(argumentType)) {
                throw new Exception($"{nameof(ProcessArgumentForInstruction)}: got unexpected argument type: {argumentType}");
            }

            var byteExpected = argumentType is CpuInstrArgType.IxOffset or CpuInstrArgType.IyOffset;

            if(byteExpected && !value.IsValidByte) {
                AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument: value out of range for {indexRegisterNames[argumentType]} instruction");
                return false;
            }
            var byteValue = value.ValueAsByte;

            if(!isNegativeIxy && byteValue > (byteExpected ? 127 : 32767)) {
                AddError(AssemblyErrorCode.ConfusingOffset, $"Ofsset {indexRegisterNames[argumentType]}+{byteValue} will actually be interpreted as {indexRegisterNames[argumentType]}-{256 - byteValue}");
            }

            if(isNegativeIxy && value.Value < (byteExpected ? (ushort)0xFF80 : -32768)) {
                AddError(AssemblyErrorCode.ConfusingOffset, $"Ofsset {indexRegisterNames[argumentType]}-{(65536 - value.Value)} will actually be interpreted as {indexRegisterNames[argumentType]}+{byteValue}");
            }

            if(value.IsAbsolute) {
                instructionBytes[position] = byteValue;
                if(!byteExpected) {
                    instructionBytes[position+1] = (byte)(value.Value >> 8);
                }
            }

            return true;
        }

        private static Expression GetExpressionForInstructionArgument(string opcode, string argument, bool isByte)
        {
            try {
                var expression = state.GetExpressionFor(argument, isByte: isByte);
                return expression;
            }
            catch(InvalidExpressionException ex) {
                AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument for {currentCpu} instruction {opcode.ToUpper()}: {ex.Message}");
                return null;
            }
        }

        private static RelocatableOutputPart RelocatableFromAddress(Address address, int index, int size, string sdccAreaName)
        {
            return address.IsAbsolute ?
                null :
                new RelocatableValue() { Type = address.Type, Value = address.Value, Index = index, IsByte = (size == 1), CommonName = address.CommonBlockName, SdccAreaName = sdccAreaName };
        }
    }
}
