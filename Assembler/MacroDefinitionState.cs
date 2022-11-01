using Konamiman.Nestor80.Assembler.Output;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Konamiman.Nestor80.Assembler
{
    internal static class MacroDefinitionState
    {
        public static void StartDefinition(MacroType macroType, ProcessedSourceLine processedLine)
        {
            MacroType = macroType;
            ProcessedLine = processedLine;
            lines.Clear();
            Depth = 1;
        }

        public static void EndDefinition()
        {
            MacroType = MacroType.None;
        }

        public static MacroType MacroType { get; private set; }

        public static ProcessedSourceLine ProcessedLine { get; private set; }

        public static bool DefiningMacro => MacroType is not MacroType.None;

        public static readonly List<string> lines = new();

        public static void AddLine(string line) => lines.Add(line);

        public static string[] GetLines() => lines.ToArray();

        public static int Depth { get; private set; }

        public static void IncreaseDepth() => Depth++;

        public static void DecreaseDepth() => Depth--;
    }
}
