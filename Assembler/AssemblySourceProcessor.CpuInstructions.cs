using Konamiman.Nestor80.Assembler.Output;
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
            //Important: we assume that all the CPU instructions either:
            //1. Have one single variant that accepts no arguments; or
            //2. All of its existing variants require at least one argument.

            string firstArgument = null, secondArgument = null;
            if(!walker.AtEndOfLine) {
                firstArgument = walker.ExtractExpression();
                //if(!walker.AtEndOfLine) {
                    //    secondArgument = walker.ExtractExpression();
                //}
            }

            var instructionsForOpcode = CurrentCpuInstructions[opcode];

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

            CpuInstruction matchingInstruction;
            var allowedRegisters = 
                candidateInstructions
                .Where(ci => char.IsUpper(ci.FirstArgument[0]) || ci.FirstArgument[0] == '(' && char.IsUpper(ci.FirstArgument[1]))
                .Select(ci => ci.FirstArgument)
                .Distinct()
                .ToArray();
            var matchingInstructions = FindMatchingInstructions(candidateInstructions, firstArgument, secondArgument, allowedRegisters);
            if(matchingInstructions.Length == 0) {
                AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument(s) for the Z80 instruction {opcode.ToUpper()}");
                return GenerateInstructionLine(null);
            }
            else if(matchingInstructions.Length == 1) {
                matchingInstruction = matchingInstructions[0];
                if(char.IsUpper(matchingInstruction.FirstArgument[0])) {
                    //e.g. INC A; INC HL
                    return GenerateInstructionLine(matchingInstruction);
                }

                if(matchingInstruction.FirstArgument[0] == '(' && matchingInstruction.FirstArgument.Length == 4) {
                    //e.g. LD A,(HL); JP (IX)
                    return GenerateInstructionLine(matchingInstruction);
                }
            }
            

            //At this point the first argument is either n, (nn) or (IXY+-n), so it's expression parsing time

            string indexOffsetSign = null;
            string firstArgumentExpressionText = null;
            Expression firstArgumentExpression = null;
            Address firstArgumentValue = null;
            var match = indexPlusArgumentRegex.Match(firstArgument);
            if(match.Success) {
                indexOffsetSign = match.Groups["sign"].Value;
                firstArgumentExpressionText = match.Groups["expression"].Value;
            }
            else {
                firstArgumentExpressionText = firstArgument;
            }

            try {
                firstArgumentExpression = Expression.Parse(firstArgumentExpressionText);
                firstArgumentExpression.ValidateAndPostifixize();
                firstArgumentValue = firstArgumentExpression.TryEvaluate();
            }
            catch(InvalidExpressionException ex) {
                AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument for Z80 instruction {opcode.ToUpper()}: {ex.Message}");
                return GenerateInstructionLine(null);
            }

            if(matchingInstructions.Length > 1) {
                //Can happen if the first argument pattern is of type "f"
                if(firstArgumentValue is null) {
                    AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument for Z80 instruction {opcode.ToUpper()}: expression for first argument references unknown symbols");
                    return GenerateInstructionLine(null);
                }
                matchingInstruction = matchingInstructions.SingleOrDefault(i => i.FirstArgumentFixedValue == firstArgumentValue.Value);
                if(matchingInstruction is null) {
                    AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument for Z80 instruction {opcode.ToUpper()}: expression for first argument yields an unsupported value");
                    return GenerateInstructionLine(null);
                }
                return GenerateInstructionLine(matchingInstruction);
            }

            matchingInstruction = matchingInstructions[0];
            if(firstArgumentValue is null) {
                return GenerateInstructionLine(matchingInstruction, pendingExpression1: firstArgumentExpression);
            }

            byte[] bytes = null;
            if(matchingInstruction.FirstArgument == "d") {
                if(firstArgumentValue.Type != state.CurrentLocationArea) {
                    AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument for Z80 instruction {opcode.ToUpper()}: the target address must be in the same area of the instruction");
                }
                var offset = firstArgumentValue.Value - (state.CurrentLocationPointer + 2);
                if(offset is < -128 or > 127) {
                    AddError(AssemblyErrorCode.InvalidCpuInstruction, $"Invalid argument for Z80 instruction {opcode.ToUpper()}: the target address is out of range");
                }

                bytes = matchingInstruction.Opcodes.ToArray();
                bytes[matchingInstruction.ValuePosition] = (byte)(offset & 0xFF);
                return GenerateInstructionLine(matchingInstruction, bytes);
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

            throw new NotImplementedException("I still can't handle that CPU instruction");
        }

        private static CpuInstruction[] FindMatchingInstructions(CpuInstruction[] candidateInstructions, string firstArgument, string secondArgument, string[] allowedRegisters)
        {
            //Assumption: all the candidate instructions have one argument of type "d" (offset from current location), or none has.
            var isLocationOffset = candidateInstructions[0].FirstArgument == "d" || candidateInstructions[0].SecondArgument == "d";
            var firstArgumentPattern = GetCpuInstructionArgumentPattern(firstArgument, allowedRegisters);
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

            throw new NotImplementedException("I can't process this instruction yet");
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
