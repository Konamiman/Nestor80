using Konamiman.Nestor80.Assembler.Output;
using System.Reflection.Emit;
using System.Text.RegularExpressions;

namespace Konamiman.Nestor80.Assembler
{
    public partial class AssemblySourceProcessor
    {
        private static readonly Regex ixPlusArgumentRegex = new(@"^\(\s*IX\s*[+-][^)]+\)$", RegxOp);
        private static readonly Regex iyPlusArgumentRegex = new(@"^\(\s*IY\s*[+-][^)]+\)$", RegxOp);
        public static readonly Regex indexPlusArgumentRegex = new(@"^\(\s*I(X|Y)\s*(?<sign>[+-])(?<expression>[^)]+)\)$", RegxOp);
        private static readonly Regex memPointedByRegisterRegex = new(@"^\(\s*(?<reg>HL|DE|BC|IX|IY|SP|C)\s*\)$", RegxOp);
        private static readonly Regex registerRegex = new(@"^[A-Z]{1,3}$", RegxOp);

        private static readonly string[] z80InstructionsForRelativeJump = { "JR", "DJNZ" };

        private static readonly Dictionary<CpuInstrArgType, string> indexRegisterNames = new() {
            { CpuInstrArgType.IxOffset, "IX" },
            { CpuInstrArgType.IyOffset, "IY" }
        };

        private static readonly Dictionary<CpuParsedArgType, byte[]> ldIxyByteInstructions = new() {
            { CpuParsedArgType.IxPlusOffset, new byte[] { 0xdd, 0x36, 0, 0 } },
            { CpuParsedArgType.IyPlusOffset, new byte[] { 0xfd, 0x36, 0, 0 } }
        };

        private static readonly Dictionary<CpuParsedArgType, CpuInstrArgType> instrArgTypeByParsedType = new() {
            { CpuParsedArgType.IxPlusOffset, CpuInstrArgType.IxOffset },
            { CpuParsedArgType.IyPlusOffset, CpuInstrArgType.IyOffset }
        };

        private static ProcessedSourceLine ProcessCpuInstruction(string opcode, SourceLineWalker walker)
        {
            byte[] instructionBytes = null;
            CpuInstructionLine instructionLine = null;
            bool isNegativeIxy = false;

            static string RemoveSpacesAroundParenthesis(string argument)
            {
                if(argument[0] is '(' && argument.Length > 1 && argument[^1] is ')') {
                    argument = $"({argument[1..^1].Trim()})";
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

            if(FixedZ80Instructions.ContainsKey(instructionSearchKey)) {
                var line = new CpuInstructionLine() { FirstArgumentTemplate = firstArgument, SecondArgumentTemplate = secondArgument, OutputBytes = FixedZ80Instructions[instructionSearchKey] };
                CompleteInstructionLine(line);
                return line;
            }

            // There's at least one variable argument.

            var firstArgumentType = GetCpuInstructionArgumentPatternNew(firstArgument);
            var secondArgumentType = GetCpuInstructionArgumentPatternNew(secondArgument);

            if(string.Equals("LD", opcode, StringComparison.OrdinalIgnoreCase) &&
                firstArgumentType is CpuParsedArgType.IxPlusOffset or CpuParsedArgType.IyPlusOffset &&
                secondArgumentType is CpuParsedArgType.Number) {

                // Treat "LD (IX+n),n" and "LD (IY+n),n" as special cases since these are
                // the only ones with two variable arguments.
                // For simplicity we won't try to evaluate the expressions in this case.

                (var expression1Text, isNegativeIxy) = GetExpressionAndSignFromIndexArgument(firstArgument);

                var argument1Expression = GetExpressionForInstructionArgument(opcode, expression1Text);
                if(argument1Expression is null) {
                    return new CpuInstructionLine() { IsInvalid = true };
                }

                var argument2Expression = GetExpressionForInstructionArgument(opcode, secondArgument);
                if(argument2Expression is null) {
                    return new CpuInstructionLine() { IsInvalid = true };
                }

                instructionBytes = ldIxyByteInstructions[firstArgumentType].ToArray();

                instructionLine = new CpuInstructionLine() { FirstArgumentTemplate = firstArgument, SecondArgumentTemplate = secondArgument, OutputBytes = instructionBytes };
                //TODO: Use AdjustInstructionLineForExpression twice, see if it works
                state.RegisterPendingExpression(
                    instructionLine,
                    argument1Expression,
                    location: 2,
                    argumentType: firstArgumentType is CpuParsedArgType.IxPlusOffset ? CpuInstrArgType.IxOffset : CpuInstrArgType.IyOffset,
                    isNegativeIxy: isNegativeIxy
                );

                state.RegisterPendingExpression(
                    instructionLine,
                    argument2Expression,
                    location: 3,
                    argumentType: CpuInstrArgType.Byte
                );

                CompleteInstructionLine(instructionLine);
                return instructionLine;
            }

            else if(Z80InstructionsWithSelectorValue.ContainsKey(opcode)) {
                if(firstArgumentType is not CpuParsedArgType.Number and not CpuParsedArgType.NumberInParenthesis) {
                    AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument(s) for {currentCpu} instruction {opcode.ToUpper()}");
                    return new CpuInstructionLine() { IsInvalid = true };
                }

                (string, byte[], ushort)[] candidates = null;
                if(secondArgumentType is CpuParsedArgType.None) {
                    candidates = Z80InstructionsWithSelectorValue[opcode].Where(c => c.Item1 is null).ToArray();
                }
                else if(secondArgumentType is CpuParsedArgType.Fixed) {
                    candidates = Z80InstructionsWithSelectorValue[opcode].Where(c => string.Equals(c.Item1, secondArgument, StringComparison.OrdinalIgnoreCase)).ToArray();
                }
                else if(secondArgumentType is CpuParsedArgType.IxPlusOffset) {
                    candidates = Z80InstructionsWithSelectorValue[opcode].Where(c => c.Item1[0] == 'x').ToArray();
                }
                else if(secondArgumentType is CpuParsedArgType.IyPlusOffset) {
                    candidates = Z80InstructionsWithSelectorValue[opcode].Where(c => c.Item1[0] == 'y').ToArray();
                }

                if(candidates is null || candidates.Length == 0) {
                    AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument(s) for {currentCpu} instruction {opcode.ToUpper()}");
                    return new CpuInstructionLine() { IsInvalid = true };
                }

                var selectorExpression = GetExpressionForInstructionArgument(opcode, firstArgument);
                var selectorExpressionValue = selectorExpression.EvaluateIfNoSymbols();
                if(selectorExpressionValue is null) {
                    instructionLine = new CpuInstructionLine() { FirstArgumentTemplate = firstArgument, SecondArgumentTemplate = secondArgument, OutputBytes = candidates[0].Item2 };
                    state.RegisterInstructionsPendingSelection(instructionLine, candidates.Select(c => new InstructionPendingSelection() { InstructionBytes = c.Item2, SelectorValue = c.Item3 } ).ToArray(), selectorExpression);
                    CompleteInstructionLine(instructionLine);
                    return instructionLine;
                }

                var chosenInstruction = candidates.FirstOrDefault(c => c.Item3 == selectorExpressionValue.Value);
                if(chosenInstruction.Item2 is null) {
                    AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument(s) for {currentCpu} instruction {opcode.ToUpper()}");
                    return new CpuInstructionLine() { IsInvalid = true };
                }

                instructionLine = new CpuInstructionLine() { FirstArgumentTemplate = firstArgument, SecondArgumentTemplate = secondArgument, OutputBytes = chosenInstruction.Item2 };
                if(chosenInstruction.Item1 is "x" or "y") {
                    (var secondArgumentExpressionText, isNegativeIxy) = GetExpressionAndSignFromIndexArgument(secondArgument);
                    var secondArgumentExpression = GetExpressionForInstructionArgument(opcode, secondArgumentExpressionText);
                    if(secondArgumentExpression is null) {
                        return new CpuInstructionLine() { IsInvalid = true };
                    }
                    var secondArgumentExpressionValue = secondArgumentExpression.EvaluateIfNoSymbols();
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
            // If there are two arguments, one must be fixed (being the only exception "LD (IXY+n),n" which we'd have already handled).

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
                    return new CpuInstructionLine() { IsInvalid = true };
                }
            }

            if(instructionSearchKey is null) {
                AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument(s) for {currentCpu} instruction {opcode.ToUpper()}");
                return new CpuInstructionLine() { IsInvalid = true };
            }

            // Now that we have identified the instruction, deal with the expression for the variable argument
            // and generate either the full instruction or one needing expression evaluation in pass 2.

            int variableArgBytePosition = 0;
            var variableArgType = CpuInstrArgType.None;

            for(int i=0; i< Z80InstructionsWithOneVariableArgument.Length; i++) {
                var candidateInstructionInfo = Z80InstructionsWithOneVariableArgument[i];
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
                            CpuInstrArgType.Word) ||
                    (argSearchType is CpuParsedArgType.Number &&
                        instructionArgType is
                            CpuInstrArgType.Byte or
                            CpuInstrArgType.Word) ||
                    (argSearchType is CpuParsedArgType.IxPlusOffset && instructionArgType is CpuInstrArgType.IxOffset) ||
                    (argSearchType is CpuParsedArgType.IyPlusOffset && instructionArgType is CpuInstrArgType.IyOffset);

                if(isMatch) {
                    variableArgType = candidateInstructionInfo.Item2;
                    instructionBytes = candidateInstructionInfo.Item4;
                    variableArgBytePosition = candidateInstructionInfo.Item5;
                    break;
                }
            }

            if(variableArgType is CpuInstrArgType.None) {
                AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument(s) for {currentCpu} instruction {opcode.ToUpper()}");
                return new CpuInstructionLine() { IsInvalid = true };
            }

            if(z80InstructionsForRelativeJump.Contains(opcode, StringComparer.OrdinalIgnoreCase)) {
                variableArgType = CpuInstrArgType.OffsetFromCurrentLocation;
            }

            isNegativeIxy = false;
            string expressionText = variableArgument;
            if(variableArgType is CpuInstrArgType.IxOffset or CpuInstrArgType.IyOffset) {
                (expressionText, isNegativeIxy) = GetExpressionAndSignFromIndexArgument(variableArgument);
            }

            var argumentExpression = GetExpressionForInstructionArgument(opcode, expressionText);
            if(argumentExpression is null) {
                return new CpuInstructionLine() { IsInvalid = true };
            }

            instructionLine = new CpuInstructionLine() { FirstArgumentTemplate = firstArgument, SecondArgumentTemplate = secondArgument, Cpu = currentCpu, OutputBytes = instructionBytes };
            
            var adjustOk = AdjustInstructionLineForExpression(instructionLine, argumentExpression, variableArgBytePosition, variableArgType, isNegativeIxy);
            CompleteInstructionLine(instructionLine);
            return adjustOk ? instructionLine : new CpuInstructionLine() { IsInvalid = true };
        }

        private static void CompleteInstructionLine(CpuInstructionLine line)
        {
            state.IncreaseLocationPointer(line.OutputBytes.Length);
            line.Cpu = currentCpu;
            line.NewLocationArea = state.CurrentLocationArea;
            line.NewLocationCounter = state.CurrentLocationPointer;
        }

        private static (string, bool) GetExpressionAndSignFromIndexArgument(string argument)
        {
            var match = indexPlusArgumentRegex.Match(argument);
            var ixRegisterSign = match.Groups["sign"].Value;
            var expressionText = ixRegisterSign + match.Groups["expression"].Value;
            return (expressionText, ixRegisterSign == "-");
        }

        private static bool AdjustInstructionLineForExpression(CpuInstructionLine line, Expression argumentExpression, int argBytePosition, CpuInstrArgType argType, bool isNegativeIxy = false)
        {
            var variableArgumentValue = argumentExpression.EvaluateIfNoSymbols();
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

            if(variableArgumentValue.IsAbsolute) {
                var instructionBytes = line.OutputBytes.ToArray();
                if(!ProcessArgumentForInstruction(argType, instructionBytes, variableArgumentValue, argBytePosition, isNegativeIxy)) {
                    return false;
                }
                line.OutputBytes = instructionBytes;
                line.RelocatableParts = Array.Empty<RelocatableOutputPart>();
            }
            else {
                var relocatable = RelocatableFromAddress(
                    variableArgumentValue,
                    argBytePosition,
                    argType is CpuInstrArgType.Word or CpuInstrArgType.WordInParenthesis ? 2 : 1);
                line.RelocatableParts = new[] { relocatable };
            }

            return true;
        }

        private static CpuParsedArgType GetCpuInstructionArgumentPatternNew(string argument)
        {
            if(argument is null)
                return CpuParsedArgType.None;

            var match = memPointedByRegisterRegex.Match(argument);
            if(match.Success) {
                return CpuParsedArgType.Fixed;
            }
            if(z80RegisterNames.Contains(argument, StringComparer.OrdinalIgnoreCase)) {
                return CpuParsedArgType.Fixed;
            }
            if(ixPlusArgumentRegex.IsMatch(argument)) {
                return CpuParsedArgType.IxPlusOffset;
            }
            if(iyPlusArgumentRegex.IsMatch(argument)) {
                return CpuParsedArgType.IyPlusOffset;
            }

            if(argument[0] == '(') {
                return CpuParsedArgType.NumberInParenthesis;
            }

            return CpuParsedArgType.Number;
        }

        private static bool ProcessArgumentForInstruction(CpuInstrArgType argumentType, byte[] instructionBytes, Address value, int position, bool isNegativeIxy = false)
        {
            if(argumentType is CpuInstrArgType.OffsetFromCurrentLocation) {
                if(value.Type != state.CurrentLocationArea) {
                    AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument: the target address must be in the same area of the instruction");
                    return false;
                }
                var offset = value.Value - (state.CurrentLocationPointer + 2);
                if(offset is < -128 or > 127) {
                    AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument: the target address is out of range");
                    return false;
                }

                instructionBytes[position] = (byte)offset;
                return true;
            }

            if(argumentType is CpuInstrArgType.Byte or CpuInstrArgType.ByteInParenthesis && value.IsAbsolute) {
                instructionBytes[position] = value.ValueAsByte;
                return true;
            }

            else if(argumentType is CpuInstrArgType.Word or CpuInstrArgType.WordInParenthesis&& value.IsAbsolute) {
                instructionBytes[position] = value.ValueAsByte;
                instructionBytes[position + 1] = (byte)((value.Value & 0xFF00) >> 8);
                return true;
            }

            else if(argumentType is not CpuInstrArgType.IxOffset and not CpuInstrArgType.IyOffset) {
                throw new Exception($"{nameof(ProcessArgumentForInstruction)}: got unexpected argument type: {argumentType}");
            }

            if(!value.IsValidByte) {
                AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument: value out of range for {indexRegisterNames[argumentType]} instruction");
                return false;
            }
            var byteValue = value.ValueAsByte;

            if(!isNegativeIxy && byteValue > 127) {
                AddError(AssemblyErrorCode.ConfusingOffset, $"Ofsset {indexRegisterNames[argumentType]}+{byteValue} will actually be interpreted as {indexRegisterNames[argumentType]}-{256 - byteValue}");
            }

            if(isNegativeIxy && value.Value < (ushort)0xFF80) {
                AddError(AssemblyErrorCode.ConfusingOffset, $"Ofsset {indexRegisterNames[argumentType]}-{(65536 - value.Value)} will actually be interpreted as {indexRegisterNames[argumentType]}+{byteValue}");
            }

            if(value.IsAbsolute) {
                instructionBytes[position] = byteValue;
            }

            return true;
        }

        private static Expression GetExpressionForInstructionArgument(string opcode, string argument)
        {
            try {
                var expression = Expression.Parse(argument);
                expression.ValidateAndPostifixize();
                return expression;
            }
            catch(InvalidExpressionException ex) {
                AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument for {currentCpu} instruction {opcode.ToUpper()}: {ex.Message}");
                return null;
            }
        }

        private static RelocatableOutputPart RelocatableFromAddress(Address address, int index, int size)
        {
            return address.IsAbsolute ?
                null :
                new RelocatableAddress() { Type = address.Type, Value = address.Value, Index = index, IsByte = (size == 1) };
        }
    }
}
