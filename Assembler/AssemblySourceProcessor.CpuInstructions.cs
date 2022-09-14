using Konamiman.Nestor80.Assembler.Output;
using System.Reflection.Emit;

namespace Konamiman.Nestor80.Assembler
{
    public partial class AssemblySourceProcessor
    {
        private static ProcessedSourceLine ProcessCpuInstruction(string opcode, SourceLineWalker walker)
        {
            //Important: we assume that all the CPU instructions either:
            //1. Have one single variant that accepts no arguments; or
            //2. All of its existing variants require at least one argument.

            string firstArgument = null, secondArgument = null;
            if(!walker.AtEndOfLine) {
                firstArgument = walker.ExtractExpression();
                if(!walker.AtEndOfLine) {
                    secondArgument = walker.ExtractExpression();
                }
            }

            var instructionsForOpcode = CurrentCpuInstructions[opcode];

            if(firstArgument is null) {
                var instruction = instructionsForOpcode.FirstOrDefault( i => i.FirstArgument is null );
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

            throw new NotImplementedException("I still can't handle that CPU instruction");
        }

        private static ProcessedSourceLine GenerateInstructionLine(CpuInstruction instruction, byte[] actualBytes = null, Expression pendingExpression1 = null, Expression pendingExpression2 = null)
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
            
            state.IncreaseLocationPointer(actualBytes.Length);
            line.NewLocationArea = state.CurrentLocationArea;
            line.NewLocationCounter = state.CurrentLocationPointer;

            return line;
        }
    }
}
