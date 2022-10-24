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

        private static ProcessedSourceLine ProcessCpuInstruction(string opcode, SourceLineWalker walker)
        {
            string firstArgument = null, secondArgument = null;
            if(!walker.AtEndOfLine) {
                firstArgument = walker.ExtractExpression();
                if(!walker.AtEndOfLine) {
                    secondArgument = walker.ExtractExpression();
                }
            }

            // First let's see if it's an instruction with no variable arguments

            string RemoveSpacesAroundParenthesis(string argument)
            {
                if(argument[0] is '(' && argument.Length > 1) {
                    argument = $"({argument[1..].Trim()}";
                }
                if(argument[^1] is ')') {
                    argument = $"{argument[^1..].Trim()})";
                }
                return argument;
            }

            string fixedInstructionKey;
            if(firstArgument is null) {
                fixedInstructionKey = opcode;
            }
            else if(secondArgument is null) {
                fixedInstructionKey = $"{opcode} {RemoveSpacesAroundParenthesis(firstArgument)}";
            }
            else {
                fixedInstructionKey = $"{opcode} {RemoveSpacesAroundParenthesis(firstArgument)},{RemoveSpacesAroundParenthesis(secondArgument)}";
            }

            if(FixedZ80Instructions.ContainsKey(fixedInstructionKey)) {
                return new CpuInstructionLine() { OutputBytes = FixedZ80Instructions[fixedInstructionKey] };
            }

            return ProcessCpuInstructionOld(opcode, walker, firstArgument, secondArgument);
        }

        private static ProcessedSourceLine ProcessCpuInstructionOld(string opcode, SourceLineWalker walker, string firstArgument, string secondArgument)
        { 

            var instructionsForOpcode = currentCpuInstructions[opcode];

            //Assumption: all the CPU instructions either:
            //1. Have one single variant that accepts no arguments; or
            //2. All of its existing variants require at least one argument.

            if(firstArgument is null) {
                var instruction = instructionsForOpcode.SingleOrDefault( i => i.FirstArgument is null );
                if(instruction is not null) {
                    return GenerateInstructionLine(instruction);
                }

                AddError(AssemblyErrorCode.InvalidCpuInstruction, $"The {currentCpu} instruction {opcode.ToUpper()} requires one or more arguments");
                return GenerateInstructionLine(null);
            }

            // From here we know that we received at least one argument

            if(instructionsForOpcode.All(i => i.FirstArgument is null)) {
                //e.g. "NOP foo", handle anyway and generate a warning
                //(for compatibility with Macro80)
                AddError(AssemblyErrorCode.UnexpectedContentAtEndOfLine, $"Unexpected arguments(s) for the {currentCpu} instruction {opcode.ToUpper()}");
                walker.DiscardRemaining();
                return GenerateInstructionLine(instructionsForOpcode[0]);
            }

            CpuInstruction[] candidateInstructions;
            if(walker.AtEndOfLine) {
                candidateInstructions = instructionsForOpcode.Where(i => i.FirstArgument is not null && i.SecondArgument is null).ToArray();
                if(candidateInstructions.Length == 0) {
                    AddError(AssemblyErrorCode.InvalidCpuInstruction, $"The {currentCpu} instruction {opcode.ToUpper()} requires two arguments");
                    return GenerateInstructionLine(null);
                }
            }
            else {
                secondArgument = walker.ExtractExpression();
                if(opcode.Equals("EX", StringComparison.OrdinalIgnoreCase) && secondArgument.Equals("AF'", StringComparison.OrdinalIgnoreCase)) {
                    secondArgument = "AF";
                }
                candidateInstructions = instructionsForOpcode.Where(i => i.FirstArgument is not null && i.SecondArgument is not null).ToArray();
                if(candidateInstructions.Length == 0) {
                    //e.g. "INC A,foo", handle anyway and generate a warning
                    //(for compatibility with Macro80)
                    AddError(AssemblyErrorCode.UnexpectedContentAtEndOfLine, $"Unexpected second argument for the {currentCpu} instruction {opcode.ToUpper()}");
                    candidateInstructions = instructionsForOpcode.Where(i => i.FirstArgument is not null && i.SecondArgument is null).ToArray();
                    secondArgument = null;
                    walker.DiscardRemaining();
                }
            }

            //Select the matching instruction from the candidates based on the pattern of the argument(s).
            //
            //Arguments can conform to one of these patterns:
            //
            //fixed: a fixed register or flag specification, e.g. A, (HL), NC
            //evaluable: an expression that eventually needs to be evaluable, this includes (IX+s) and (IY+s)
            //specific: a special case of "evaluable", where the argument has a specific value
            //          (e.g. IM has only three valid variants: "IM 0", "IM 1", "IM 2").
            //          Assumption: a specific argument is always the first one
            //          Assumption: all the candidate instructions have the first argument as specific, or none has
            //
            //For instructions with the first argument being of type "specific", more than one matching
            //instruction can actually be selected.

            CpuInstruction matchingInstruction = null;
            bool firstArgumentIsSpecificValue = true;   //e.g. IM n

            var firstArgumentPattern = GetCpuInstructionArgumentPattern(firstArgument);
            var secondArgumentPattern = GetCpuInstructionArgumentPattern(secondArgument);

            var matchingInstructions = FindMatchingInstructions(candidateInstructions, firstArgument, firstArgumentPattern, secondArgument, secondArgumentPattern);
            if(matchingInstructions.Length == 0) {
                AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument(s) for the {currentCpu} instruction {opcode.ToUpper()}");
                return GenerateInstructionLine(null);
            }
            else if(matchingInstructions.Length == 1) {
                matchingInstruction = matchingInstructions[0];
                if(IsFixedPattern(matchingInstruction.FirstArgument) && 
                   (matchingInstruction.SecondArgument is null || IsFixedPattern(matchingInstruction.SecondArgument))) { 
                    //e.g. "INC A" or "LD A,(HL)" - nothing to evaluate so directly use the instruction as is
                    return GenerateInstructionLine(matchingInstruction);
                }
                firstArgumentIsSpecificValue = false;
            }
            else if(matchingInstructions.Any(i => i.FirstArgument != "f")) {
                var instructionsList = string.Join("; ", candidateInstructions.Select(i => i.ToString()).ToArray());
                throw new Exception($"Somethign went wrong: {matchingInstructions.Length} candidate {currentCpu} instructions found: {instructionsList}");
            }

            //Here we have one or two arguments, and at least one is either specific or evaluable;
            //so try evaluating them before further proceeding.
            //
            //Possible instruction patterns at this point:
            //
            //opcode evaluable
            //opcode fixed, evaluable
            //opcode specific, fixed
            //opcode specific, evaluable
            //opcode evaluable, evaluable - only for LD (IXY+-n),n

            string indexOffsetSign = null;
            Expression firstArgumentExpression = null;
            Address firstArgumentValue = null;
            var firstArgumentIsFixed = true;
            Expression secondArgumentExpression = null;
            Address secondArgumentValue = null;
            var secondArgumentIsFixed = true;

            if(!IsFixedPattern(firstArgumentPattern)) {
                var match = indexPlusArgumentRegex.Match(firstArgument);
                string firstArgumentExpressionText = firstArgument;
                if(match.Success) {
                    indexOffsetSign = match.Groups["sign"].Value;
                    firstArgumentExpressionText = indexOffsetSign + match.Groups["expression"].Value;
                }

                firstArgumentExpression = GetExpressionForInstructionArgument(opcode, firstArgumentExpressionText);
                if(firstArgumentExpression is null) {
                    return GenerateInstructionLine(null);
                }

                firstArgumentValue = firstArgumentExpression.EvaluateIfNoSymbols();

                firstArgumentIsFixed = false;
            }

            if(secondArgument is not null && !IsFixedPattern(secondArgumentPattern)) {
                //Assumption: zero or one of the arguments are (IXY+-n), but not both
                string secondArgumentExpressionText = secondArgument;
                if(indexOffsetSign is null && !matchingInstructions.First().SecondValuePosition.HasValue) {
                    var match = indexPlusArgumentRegex.Match(secondArgument);
                    if(match.Success) {
                        indexOffsetSign = match.Groups["sign"].Value;
                        secondArgumentExpressionText = indexOffsetSign + match.Groups["expression"].Value;
                    }
                }

                secondArgumentExpression = GetExpressionForInstructionArgument(opcode, secondArgumentExpressionText);
                if(secondArgumentExpression is null) {
                    return GenerateInstructionLine(null);
                }

                secondArgumentValue = secondArgumentExpression.EvaluateIfNoSymbols();

                secondArgumentIsFixed = false;
            }

            Expression instructionSelector = null;
            if(firstArgumentIsSpecificValue) {
                if(firstArgumentValue is null) {
                    //Select one of the possible instructions "at random",
                    //we'll replace it with the proper one in pass 2
                    matchingInstruction = matchingInstructions.First();
                    instructionSelector = firstArgumentExpression;
                }
                else {
                    matchingInstruction = matchingInstructions.SingleOrDefault(i => i.FirstArgumentFixedValue == firstArgumentValue.Value);
                    if(matchingInstruction is null) {
                        AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument for {currentCpu} instruction {opcode.ToUpper()}: expression yields an unsupported value");
                        return GenerateInstructionLine(null);
                    }
                    matchingInstructions = null;
                }

                if(secondArgument is null || secondArgumentIsFixed) {
                    //e.g. "RST n" or "BIT n,A" - nothing to further evaluate so use the instruction as is
                    return GenerateInstructionLine(matchingInstruction, candidateInstructions: matchingInstructions, candidateSelector: instructionSelector);
                }
            }
            else {
                matchingInstructions = null;
            }

            //If the first argument doesn't need evaluation,
            //promote the second argument to being de facto the first one;
            //this will simplify processing going further.

            if(firstArgumentIsSpecificValue || firstArgumentIsFixed) { 
                firstArgument = secondArgument;
                firstArgumentValue = secondArgumentValue;
                firstArgumentExpression = secondArgumentExpression;
                firstArgumentIsFixed = secondArgumentIsFixed;
                firstArgumentPattern = secondArgumentPattern;
                secondArgument = null;
                secondArgumentExpression = null;
                secondArgumentValue = null;
            }

            //From this point we treat arguments with the "specific" pattern as "fixed",
            //since we have already selected the matching instruction.
            //So the possible patterns are now:
            //
            //opcode evaluable
            //opcode fixed, evaluable
            //opcode evaluable, evaluable - only for LD (IXY+-n),n

            //If at least one argument can't be evaluated we generate an instruction line
            //with the appropriate pending expressions.

            var argumentType =
                matchingInstruction.FirstArgument == "d" || matchingInstruction.SecondArgument == "d" ? CpuInstructionArgumentType.OffsetFromCurrentLocation:
                indexOffsetSign is not null ? CpuInstructionArgumentType.IxyOffset : 
                matchingInstruction.ValueSize == 1 ? CpuInstructionArgumentType.Byte :
                CpuInstructionArgumentType.Word;
            var ixRegName = argumentType is CpuInstructionArgumentType.IxyOffset ? firstArgument.Substring(1, 2).ToUpper() : null;

            if(firstArgumentValue is null || (secondArgument is not null && !secondArgumentIsFixed && secondArgumentValue is null)) {
                return GenerateInstructionLine(
                    matchingInstruction,
                    pendingExpression1: firstArgumentExpression,
                    pendingExpression2: secondArgumentExpression,
                    argumentType: argumentType,
                    ixRegisterName: ixRegName,
                    ixRegisterSign: indexOffsetSign,
                    candidateInstructions: matchingInstructions, 
                    candidateSelector: instructionSelector);
            }

            //Here we know that the required expressions have been successfully evaluated,
            //we just need to perform the final validations on them and generate the instruction.

            byte[] bytes = matchingInstruction.Opcodes.ToArray();


            if(!ProcessArgumentForInstruction(argumentType, bytes, firstArgumentValue, matchingInstruction.ValuePosition, ixRegName, indexOffsetSign)) { 
                return GenerateInstructionLine(null);
            }
            else if(argumentType is CpuInstructionArgumentType.OffsetFromCurrentLocation) {
                return GenerateInstructionLine(matchingInstruction, bytes, candidateInstructions: matchingInstructions, candidateSelector: instructionSelector);
            }

            if(secondArgument is null || secondArgumentIsFixed) {
                return GenerateInstructionLine(
                    matchingInstruction, 
                    bytes, 
                    relocatables: new RelocatableOutputPart[] {
                        RelocatableFromAddress(firstArgumentValue, matchingInstruction.ValuePosition, matchingInstruction.ValueSize)
                    },
                    candidateInstructions: matchingInstructions, 
                    candidateSelector: instructionSelector);
            }

            if(matchingInstruction.SecondValuePosition is null) {
                throw new Exception($"Something went wrong parsing {opcode.ToUpper()} instruction: we have an unexpected second argument");
            }

            if(!firstArgumentIsFixed && !firstArgumentIsSpecificValue) {
                //To prevent the sign from being mistakenly used
                //to validate the second argument in LD (IXY+-n),n
                indexOffsetSign = null;
            }

            if(!ProcessEvaluatedInstructionArgument(opcode, bytes, secondArgument, secondArgumentValue, indexOffsetSign, matchingInstruction.SecondValueSize.Value, matchingInstruction.SecondValuePosition.Value)) {
                return GenerateInstructionLine(null);
            }

            return GenerateInstructionLine(
                matchingInstruction,
                bytes, 
                relocatables: new RelocatableOutputPart[] {
                    RelocatableFromAddress(firstArgumentValue, matchingInstruction.ValuePosition, matchingInstruction.ValueSize),
                    RelocatableFromAddress(secondArgumentValue, matchingInstruction.SecondValuePosition.Value, matchingInstruction.SecondValueSize.Value),
                },
                candidateInstructions: matchingInstructions, 
                candidateSelector: instructionSelector);
        }

        private static bool ProcessEvaluatedInstructionArgument(string opcode, byte[] bytes, string argumentText, Address argumentValue, string indexOffsetSign, int valueSize, int valuePosition)
        {
            if(valueSize == 1) {
                if(!argumentValue.IsValidByte) {
                    AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument for {currentCpu} instruction {opcode.ToUpper()}: argument value out of range");
                    return false;
                }
                var byteValue = argumentValue.ValueAsByte;

                if(indexOffsetSign == "+" && byteValue > 127) {
                    var regName = argumentText.Substring(1, 2).ToUpper();
                    AddError(AssemblyErrorCode.ConfusingOffset, $"Ofsset {regName}+{byteValue} in {currentCpu} instruction {opcode.ToUpper()} will actually be interpreted as {regName}-{256 - byteValue}");
                }

                if(indexOffsetSign == "-" && argumentValue.Value < (ushort)0xFF80) {
                    var regName = argumentText.Substring(1, 2).ToUpper();
                    AddError(AssemblyErrorCode.ConfusingOffset, $"Ofsset {regName}-{(65536-argumentValue.Value)} in {currentCpu} instruction {opcode.ToUpper()} will actually be interpreted as {regName}+{byteValue}");
                }

                if(argumentValue.IsAbsolute) {
                    bytes[valuePosition] = byteValue;
                }
            }
            else if(argumentValue.IsAbsolute) {
                bytes[valuePosition] = argumentValue.ValueAsByte;
                bytes[valuePosition + 1] = (byte)((argumentValue.Value & 0xFF00) >> 8);
            }

            return true;
        }

        private static bool ProcessArgumentForDTypeInstruction(CpuInstruction instruction, byte[] instructionBytes, Address value)
        {
            if(value.Type != state.CurrentLocationArea) {
                AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument for {currentCpu} instruction {instruction.Instruction.ToUpper()}: the target address must be in the same area of the instruction");
                return false;
            }
            var offset = value.Value - (state.CurrentLocationPointer + 2);
            if(offset is < -128 or > 127) {
                AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument for {currentCpu} instruction {instruction.Instruction.ToUpper()}: the target address is out of range");
                return false;
            }

            instructionBytes[instruction.ValuePosition] = (byte)(offset & 0xFF);
            return true;
        }

        private static bool ProcessArgumentForInstruction(CpuInstructionArgumentType argumentType, byte[] instructionBytes, Address value, int position, string ixRegName = null, string ixOffsetSign = null)
        {
            if(argumentType is CpuInstructionArgumentType.OffsetFromCurrentLocation) {
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

            if(argumentType is CpuInstructionArgumentType.Byte && value.IsAbsolute) {
                instructionBytes[position] = value.ValueAsByte;
                return true;
            }

            else if(argumentType is CpuInstructionArgumentType.Word && value.IsAbsolute) {
                instructionBytes[position] = value.ValueAsByte;
                instructionBytes[position + 1] = (byte)((value.Value & 0xFF00) >> 8);
                return true;
            }

            else if(argumentType is not CpuInstructionArgumentType.IxyOffset) {
                throw new Exception($"{nameof(ProcessArgumentForInstruction)}: got unexpected argument type: {argumentType}");
            }

            if(!value.IsValidByte) {
                AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument: value out of range for {ixRegName} instruction");
                return false;
            }
            var byteValue = value.ValueAsByte;

            if(ixOffsetSign == "+" && byteValue > 127) {
                AddError(AssemblyErrorCode.ConfusingOffset, $"Ofsset {ixRegName}+{byteValue} will actually be interpreted as {ixRegName}-{256 - byteValue}");
            }

            if(ixOffsetSign == "-" && value.Value < (ushort)0xFF80) {
                AddError(AssemblyErrorCode.ConfusingOffset, $"Ofsset {ixRegName}-{(65536 - value.Value)} will actually be interpreted as {ixRegName}+{byteValue}");
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

        private static bool IsRegisterReferencingPattern(string argumentPattern)
        {
            //R, RR, (RR) or (IXY+-n)
            return char.IsUpper(argumentPattern[0]) || (argumentPattern[0] == '(' && char.IsUpper(argumentPattern[1]));
        }

        private static bool IsFixedPattern(string argumentPattern)
        {
            //R, RR or (RR)
            return char.IsUpper(argumentPattern[0]) || (argumentPattern[0] == '(' && char.IsUpper(argumentPattern[1]) && (argumentPattern.Length is 3 or 4));
        }

        private static CpuInstruction[] FindMatchingInstructions(CpuInstruction[] candidateInstructions, string firstArgument, string firstArgumentPattern, string secondArgument, string secondArgumentPattern)
        {
            if(firstArgumentPattern == "n") {
                candidateInstructions = candidateInstructions.Where(ci => ci.FirstArgument is "n" or "f" or "d").ToArray();
            }
            else if(firstArgumentPattern == "(n)") {
                //This is needed so that e.g. "JP (n)" will be correctly interpreted as "JP n"
                var candidateInstructions2 = candidateInstructions.Where(ci => ci.FirstArgument == "(n)").ToArray();
                if(candidateInstructions2.Length == 0) {
                    candidateInstructions = candidateInstructions.Where(ci => ci.FirstArgument == "n").ToArray();
                }
                else {
                    candidateInstructions = candidateInstructions2;
                }
            }
            else {
                candidateInstructions = candidateInstructions.Where(ci => string.Equals(firstArgumentPattern, ci.FirstArgument, StringComparison.OrdinalIgnoreCase)).ToArray();
            }

            if(candidateInstructions.Length == 0) {
                return Array.Empty<CpuInstruction>();
            }

            if(secondArgument is null) {
                if(candidateInstructions.Length > 1 && candidateInstructions.Any(ci => ci.FirstArgument != "f")) {
                    throw new InvalidOperationException("Something went wrong: more than one suitable {currentCpu} instruction found");
                }
                return candidateInstructions;
            }

            //Repeat processing for second argument (TODO: try to deduplicate code)

            if(secondArgumentPattern == "n") {
                candidateInstructions = candidateInstructions.Where(ci => ci.SecondArgument is "n" or "f" or "d").ToArray();
            }
            else if(secondArgumentPattern == "(n)") {
                //This is needed so that e.g. "JP (n)" will be correctly interpreted as "JP n"
                var candidateInstructions2 = candidateInstructions.Where(ci => ci.SecondArgument == "(n)").ToArray();
                if(candidateInstructions2.Length == 0) {
                    candidateInstructions = candidateInstructions.Where(ci => ci.SecondArgument == "n").ToArray();
                }
                else {
                    candidateInstructions = candidateInstructions2;
                }
            }
            else {
                candidateInstructions = candidateInstructions.Where(ci => string.Equals(secondArgumentPattern, ci.SecondArgument, StringComparison.OrdinalIgnoreCase)).ToArray();
            }

            return candidateInstructions.ToArray();
        }

        private static string GetCpuInstructionArgumentPattern(string argument)
        {
            if(argument is null)
                return null;

            var match = memPointedByRegisterRegex.Match(argument);
            if(match.Success) {
                return $"({match.Groups[1].Value.ToUpper()})";
            }
            if(z80RegisterNames.Contains(argument, StringComparer.OrdinalIgnoreCase)) {
                return argument.ToUpper();
            }
            if(ixPlusArgumentRegex.IsMatch(argument)) {
                return "(IX+s)";
            }
            if(iyPlusArgumentRegex.IsMatch(argument)) {
                return "(IY+s)";
            }

            if(argument[0] == '(') {
                return "(n)";
            }

            return "n";
        }

        private static ProcessedSourceLine GenerateInstructionLine(
            CpuInstruction instruction, 
            byte[] actualBytes = null, 
            RelocatableOutputPart[] relocatables = null,
            Expression pendingExpression1 = null,
            Expression pendingExpression2 = null, 
            CpuInstructionArgumentType argumentType = CpuInstructionArgumentType.None,
            string ixRegisterName = null,
            string ixRegisterSign = null,
            CpuInstruction[] candidateInstructions = null,
            Expression candidateSelector = null)
        {
            var line = new CpuInstructionLine();
            if(instruction is null) {
                line.IsInvalid = true;
                return line;
            }

            actualBytes ??= instruction.Opcodes.ToArray();
            if(pendingExpression1 is not null) {
                if(argumentType == CpuInstructionArgumentType.None) {
                    argumentType = instruction.ValueSize == 1 ? CpuInstructionArgumentType.Byte : CpuInstructionArgumentType.Word;
                }
                state.RegisterPendingExpression(line, pendingExpression1, instruction.ValuePosition, argumentType: argumentType, ixRegisterName: ixRegisterName, ixRegisterSign: ixRegisterSign);
            }
            if(pendingExpression2 is not null) {
                state.RegisterPendingExpression(line, pendingExpression2, instruction.SecondValuePosition.Value, 
                    argumentType: instruction.SecondValueSize.Value == 1 ? CpuInstructionArgumentType.Byte : CpuInstructionArgumentType.Word);
            }

            line.Cpu = currentCpu;
            line.FirstArgumentTemplate = instruction.FirstArgument;
            line.SecondArgumentTemplate = instruction.SecondArgument;
            line.OutputBytes = actualBytes;
            line.RelocatableParts = relocatables?.Where(r => r is not null).ToArray() ?? Array.Empty<RelocatableOutputPart>();
            
            state.IncreaseLocationPointer(actualBytes.Length);
            line.NewLocationArea = state.CurrentLocationArea;
            line.NewLocationCounter = state.CurrentLocationPointer;

            if(candidateInstructions is not null) {
                state.RegisterInstructionsPendingSelection(line, candidateInstructions, candidateSelector);
            }

            return line;
        }
    }
}
