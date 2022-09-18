using Konamiman.Nestor80.Assembler.Output;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Text.RegularExpressions;

namespace Konamiman.Nestor80.Assembler
{
    public partial class AssemblySourceProcessor
    {
        private static readonly Regex ixPlusArgumentRegex = new(@"^\(\s*IX\s*[+-][^)]+\)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex iyPlusArgumentRegex = new(@"^\(\s*IY\s*[+-][^)]+\)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static readonly Regex indexPlusArgumentRegex = new(@"^\(\s*IX\s*(?<sign>[+-])(?<expression>[^)]+)\)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex memPointedByRegisterRegex = new(@"^\(\s*(?<reg>[A-Z]+)\s*\)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex registerRegex = new(@"^[A-Z]{1,3}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**
         * NOTE! Code like this:
         * 
         * jp (hl)
         * hl equ 1
         * jp (hl)
         * 
         * needs to end up assembling as this:
         * 
         * jp 1
         * jp 1
         * 
         * In pass 1 this will generate the equivalent of:
         * 
         * jp (hl)
         * jp 1
         * 
         * In pass 2 the instructions that reference REG or (REG) needs to be revisited:
         * if REG ends up having been defined as a symbol, then the generated instruction needs to be revisited.
         * This means generating a new instruction or maybe an error, like in the following example:
         * 
         * jp nz,0
         * nz equ 1
         */ 
        private static ProcessedSourceLine ProcessCpuInstruction(string opcode, SourceLineWalker walker)
        {
            string firstArgument = null, secondArgument = null;
            if(!walker.AtEndOfLine) {
                firstArgument = walker.ExtractExpression();
                //if(!walker.AtEndOfLine) {
                    //    secondArgument = walker.ExtractExpression();
                //}
            }

            var instructionsForOpcode = CurrentCpuInstructions[opcode];

            //Assumption: all the CPU instructions either:
            //1. Have one single variant that accepts no arguments; or
            //2. All of its existing variants require at least one argument.
            if(firstArgument is null) {
                var instruction = instructionsForOpcode.SingleOrDefault( i => i.FirstArgument is null );
                if(instruction is not null) {
                    return GenerateInstructionLine(instruction);
                }

                AddError(AssemblyErrorCode.InvalidCpuInstruction, $"The Z80 instruction {opcode.ToUpper()} requires one or more arguments");
                return GenerateInstructionLine(null);
            }

            // From here we know we received at least one argument

            if(instructionsForOpcode[0].FirstArgument is null) {
                //e.g. "NOP foo", handle anyway and generate a warning
                //(for compatibility with Macro80)
                AddError(AssemblyErrorCode.UnexpectedContentAtEndOfLine, $"Unexpected arguments(s) for the Z80 instruction {opcode.ToUpper()}");
                walker.DiscardRemaining();
                return GenerateInstructionLine(instructionsForOpcode[0]);
            }

            CpuInstruction[] candidateInstructions;
            if(walker.AtEndOfLine) {
                candidateInstructions = instructionsForOpcode.Where(i => i.FirstArgument is not null && i.SecondArgument is null).ToArray();
                if(candidateInstructions.Length == 0) {
                    AddError(AssemblyErrorCode.InvalidCpuInstruction, $"The Z80 instruction {opcode.ToUpper()} requires two arguments");
                    return GenerateInstructionLine(null);
                }
            }
            else {
                secondArgument = walker.ExtractExpression();
                candidateInstructions = instructionsForOpcode.Where(i => i.FirstArgument is not null && i.SecondArgument is not null).ToArray();
                if(candidateInstructions.Length == 0) {
                    //e.g. "INC A,foo", handle anyway and generate a warning
                    //(for compatibility with Macro80)
                    AddError(AssemblyErrorCode.UnexpectedContentAtEndOfLine, $"Unexpected second argument for the Z80 instruction {opcode.ToUpper()}");
                    candidateInstructions = instructionsForOpcode.Where(i => i.FirstArgument is not null && i.SecondArgument is null).ToArray();
                    secondArgument = null;
                    walker.DiscardRemaining();
                }
            }

            //Further narrow the candidate instructions depending on the shape of the arguments received:
            //we should get to either one single instruction, or multiple ones if the first argument
            //of all of them is of type "f" (fixed value)

            CpuInstruction matchingInstruction = null;

            var allowedRegisters1 = 
                candidateInstructions
                .Where(ci => IsRegisterReferencingPattern(ci.FirstArgument))
                .Select(ci => ci.FirstArgument).Distinct().ToArray();
            var allowedRegisters2 =
                candidateInstructions
                .Where(ci => ci.SecondArgument is not null && IsRegisterReferencingPattern(ci.SecondArgument))
                .Select(ci => ci.SecondArgument).Distinct().ToArray();
            var matchingInstructions = FindMatchingInstructions(candidateInstructions, firstArgument, secondArgument, allowedRegisters1, allowedRegisters2);
            if(matchingInstructions.Length == 0) {
                AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument(s) for the Z80 instruction {opcode.ToUpper()}");
                return GenerateInstructionLine(null);
            }
            else if(matchingInstructions.Length == 1) {
                matchingInstruction = matchingInstructions[0];
                if(IsFixedPattern(matchingInstruction.FirstArgument) && 
                   (matchingInstruction.SecondArgument is null || IsFixedPattern(matchingInstruction.SecondArgument))) { 
                    //e.g. INC A; LD A,(HL)
                    return GenerateInstructionLine(matchingInstruction);
                }
            }
            else if(matchingInstructions.Any(i => i.FirstArgument != "f")) {
                var instructionsList = string.Join("; ", candidateInstructions.Select(i => i.ToString()).ToArray());
                throw new Exception($"Somethign went wrong: {matchingInstructions.Length} candidate Z80 instructions found: {instructionsList}");
            }

            //Here we have one or two arguments, and at least one needs evaluation;
            //so try evaluating them before further proceeding

            string indexOffsetSign = null;
            string firstArgumentExpressionText = null;
            Expression firstArgumentExpression = null;
            Address firstArgumentValue = null;
            var firstArgumentIsFixed = true;
            string secondArgumentExpressionText = null;
            Expression secondArgumentExpression = null;
            Address secondArgumentValue = null;
            var secondArgumentIsFixed = true;

            if(!IsFixedPattern(firstArgument)) {
                var match = indexPlusArgumentRegex.Match(firstArgument);
                if(match.Success) {
                    indexOffsetSign = match.Groups["sign"].Value;
                    firstArgumentExpressionText = match.Groups["expression"].Value;
                }
                else {
                    firstArgumentExpressionText = firstArgument;
                }

                firstArgumentExpression = GetExpressionForInstructionArgument(opcode, firstArgument);
                if(firstArgumentExpression is null) {
                    return GenerateInstructionLine(null);
                }

                firstArgumentValue = firstArgumentExpression.TryEvaluate();
                if(firstArgumentValue is not null) {
                    firstArgumentExpression = null;
                }

                firstArgumentIsFixed = false;
            }

            if(secondArgument is not null && !IsFixedPattern(secondArgument)) {
                //Assumption: zero or one of the arguments are (IXY+-n), but not both
                if(indexOffsetSign is null) {
                    var match = indexPlusArgumentRegex.Match(secondArgument);
                    if(match.Success) {
                        indexOffsetSign = match.Groups["sign"].Value;
                        secondArgumentExpressionText = match.Groups["expression"].Value;
                    }
                    else {
                        secondArgumentExpressionText = secondArgument;
                    }
                }

                secondArgumentExpression = GetExpressionForInstructionArgument(opcode, secondArgument);
                if(secondArgumentExpression is null) {
                    return GenerateInstructionLine(null);
                }

                secondArgumentValue = secondArgumentExpression.TryEvaluate();
                if(secondArgumentValue is not null) {
                    secondArgumentExpression = null;
                }

                secondArgumentIsFixed = false;
            }

            if(matchingInstructions.Length > 1) {
                //Instructions where the first (or only) argument is of type "f".

                if(firstArgumentValue is null) {
                    //If the fixed value can't be evaluated at this point,
                    //we provisionally take the first matching instruction;
                    //we'll take the real one in pass 2.
                    matchingInstruction = matchingInstructions.First();
                }
                else {
                    matchingInstruction = matchingInstructions.SingleOrDefault(i => i.FirstArgumentFixedValue == firstArgumentValue.Value);
                    if(matchingInstruction is null) {
                        AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument for Z80 instruction {opcode.ToUpper()}: expression yields an unsupported value");
                        return GenerateInstructionLine(null);
                    }
                }
            }

            if(firstArgumentIsFixed && (secondArgument is null || secondArgumentIsFixed)) {
                return GenerateInstructionLine(matchingInstruction);
            }

            //At this point we have only one matching instruction,
            //with one or two arguments, and at least one is an expression.
            //If either can't be evaluated we generate an instruction line
            //with the appropriate pending expressions.

            if(firstArgumentValue is null || (!secondArgumentIsFixed && secondArgumentValue is null)) {
                return GenerateInstructionLine(
                    matchingInstruction,
                    pendingExpression1: firstArgumentExpression,
                    pendingExpression2: secondArgumentExpression);
            }

            //Here we know that the required expressions have been successfully evaluated.
            //WIP

            byte[] bytes = matchingInstruction.Opcodes.ToArray();

            if(matchingInstruction.FirstArgument == "d") {
                if(firstArgumentValue is null) {
                    GenerateInstructionLine(matchingInstruction, pendingExpression1: firstArgumentExpression);
                }
                else {
                    return ProcessArgumentForDTypeInstruction(matchingInstruction, bytes, firstArgumentValue) ?
                        GenerateInstructionLine(matchingInstruction, bytes) :
                        GenerateInstructionLine(null);
                }
            }

            if(matchingInstruction.SecondArgument == "d" && secondArgumentValue is not null) {
                return ProcessArgumentForDTypeInstruction(matchingInstruction, bytes, secondArgumentValue) ?
                    GenerateInstructionLine(matchingInstruction, bytes) :
                    GenerateInstructionLine(null);
            }

            if(!firstArgumentValue.IsAbsolute) {
                return GenerateInstructionLine(matchingInstruction, relocatables: new RelocatableOutputPart[] { new RelocatableAddress() { Type = firstArgumentValue.Type, Value = firstArgumentValue.Value } });
            }


            //At this point the first argument is an absolute and already parsed value

            bytes = matchingInstruction.Opcodes.ToArray();
            if(matchingInstruction.ValueSize == 1) {
                if(!firstArgumentValue.IsValidByte) {
                    AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument for Z80 instruction {opcode.ToUpper()}: argument value out of range");
                }
                var byteValue = firstArgumentValue.ValueAsByte;

                if(indexOffsetSign == "+" && byteValue > 127) {
                    var regName = firstArgument.Substring(1, 2).ToUpper();
                    AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Ofsset {regName}+{byteValue} in Z80 instruction {opcode.ToUpper()} will actually be interpreted as {regName}-{256 - byteValue}");
                }

                if(indexOffsetSign == "-") {
                    if(byteValue > 127) {
                        var regName = firstArgument.Substring(1, 2).ToUpper();
                        AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Ofsset {regName}-{byteValue} in Z80 instruction {opcode.ToUpper()} will actually be interpreted as {regName}+{256 - byteValue}");
                    }
                    byteValue = (byte)(256 - byteValue);
                }

                bytes[matchingInstruction.ValuePosition] = byteValue;
                return GenerateInstructionLine(matchingInstruction, bytes);
            }

            bytes[matchingInstruction.ValuePosition] = firstArgumentValue.ValueAsByte;
            bytes[matchingInstruction.ValuePosition+1] = ((byte)((firstArgumentValue.Value & 0xFF00) >> 8));

            return GenerateInstructionLine(matchingInstruction, bytes);
        }

        private static bool ProcessArgumentForDTypeInstruction(CpuInstruction instruction, byte[] instructionBytes, Address value)
        {
            if(value != state.CurrentLocationArea) {
                AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument for Z80 instruction {instruction.Instruction.ToUpper()}: the target address must be in the same area of the instruction");
                return false;
            }
            var offset = value.Value - (state.CurrentLocationPointer + 2);
            if(offset is < -128 or > 127) {
                AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument for Z80 instruction {instruction.Instruction.ToUpper()}: the target address is out of range");
                return false;
            }

            instructionBytes[instruction.ValuePosition] = (byte)(offset & 0xFF);
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
                AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument for Z80 instruction {opcode.ToUpper()}: {ex.Message}");
                return null;
            }
        }

        private static RelocatableOutputPart[] RelocatablesArrayFor(Address address)
        {
            return address.IsAbsolute ?
                null :
                new RelocatableOutputPart[] { new RelocatableAddress() { Type = address.Type, Value = address.Value } };
        }

        private static bool IsRegisterReferencingPattern(string argumentPattern)
        {
            //R, RR, (RR) or (IXY+-n)
            return char.IsUpper(argumentPattern[0]) || (argumentPattern[0] == '(' && char.IsUpper(argumentPattern[0]));
        }

        private static bool IsFixedPattern(string argumentPattern)
        {
            //R, RR or (RR)
            return char.IsUpper(argumentPattern[0]) || (argumentPattern[0] == '(' && char.IsUpper(argumentPattern[1]) && argumentPattern.Length == 4);
        }

        private static CpuInstruction[] FindMatchingInstructions(CpuInstruction[] candidateInstructions, string firstArgument, string secondArgument, string[] allowedRegisters1, string[] allowedRegisters2)
        {
            var firstArgumentPattern = GetCpuInstructionArgumentPattern(firstArgument, allowedRegisters1);
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
                    throw new InvalidOperationException("Something went wrong: more than one suitable Z80 instruction found");
                }
                return candidateInstructions;
            }

            //Repeat processing for second argument (TODO: try to deduplicate code)

            var secondArgumentPattern = GetCpuInstructionArgumentPattern(secondArgument, allowedRegisters2);
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

        private static string GetCpuInstructionArgumentPattern(string argument, string[] allowedSymbols)
        {
            string reg;
            if(state.HasSymbol(argument)) {
                return "n";
            }

            var match = memPointedByRegisterRegex.Match(argument);
            if(match.Success) {
                var register = match.Groups[1].Value;
                if(state.HasSymbol(register)) {
                    return "(n)";
                }
                if((reg = allowedSymbols.SingleOrDefault(s => s.Equals($"({register})", StringComparison.OrdinalIgnoreCase))) is not null) {
                    return $"({register})";
                }
                return "(n)";
            }

            if((reg = allowedSymbols.SingleOrDefault(s => s.Equals(argument, StringComparison.OrdinalIgnoreCase))) is not null) {
                return reg;
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

        private static ProcessedSourceLine GenerateInstructionLine(CpuInstruction instruction, byte[] actualBytes = null, RelocatableOutputPart[] relocatables = null, Expression pendingExpression1 = null, Expression pendingExpression2 = null)
        {
            var line = new CpuInstructionLine();
            if(instruction is null) {
                line.IsInvalid = true;
                return line;
            }

            actualBytes ??= instruction.Opcodes;
            if(pendingExpression1 is not null) {
                state.RegisterPendingExpression(line, pendingExpression1, instruction.ValuePosition, instruction.ValueSize);
            }
            if(pendingExpression2 is not null) {
                state.RegisterPendingExpression(line, pendingExpression2, instruction.SecondValuePosition.Value, instruction.SecondValueSize.Value);
            }

            line.Cpu = CpuType.Z80;
            line.FirstArgumentTemplate = instruction.FirstArgument;
            line.SecondArgumentTemplate = instruction.SecondArgument;
            line.OutputBytes = actualBytes;
            line.RelocatableParts = relocatables ?? Array.Empty<RelocatableOutputPart>();
            
            state.IncreaseLocationPointer(actualBytes.Length);
            line.NewLocationArea = state.CurrentLocationArea;
            line.NewLocationCounter = state.CurrentLocationPointer;

            return line;
        }
    }
}
