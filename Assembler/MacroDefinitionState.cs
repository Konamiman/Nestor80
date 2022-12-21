using Konamiman.Nestor80.Assembler.Output;

namespace Konamiman.Nestor80.Assembler
{
    /// <summary>
    /// An onbject with information used to keep track of a running macro definition
    /// (while processing the source lines between the MACRO, IRP etc instruction 
    /// and the corresponding ENDM instruction).
    /// </summary>
    internal static class MacroDefinitionState
    {
        public static void StartDefinition(MacroType macroType, ProcessedSourceLine processedLine, int startLineNumber)
        {
            MacroType = macroType;
            ProcessedLine = processedLine;
            StartLineNumber = startLineNumber;
            lines.Clear();
            Depth = 1;
        }

        public static void EndDefinition()
        {
            MacroType = MacroType.None;
        }

        public static MacroType MacroType { get; private set; }

        public static ProcessedSourceLine ProcessedLine { get; private set; }

        public static int StartLineNumber { get; private set; }

        public static bool DefiningMacro => MacroType is not MacroType.None;

        public static bool DefiningNamedMacro => MacroType is MacroType.Named;

        public static readonly List<string> lines = new();

        public static void AddLine(string line) => lines.Add(line);

        public static string[] GetLines() => lines.ToArray();

        public static int Depth { get; private set; }

        public static void IncreaseDepth() => Depth++;

        public static void DecreaseDepth() => Depth--;
    }
}
