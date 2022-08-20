﻿using Konamiman.Nestor80.Assembler;
using System.Diagnostics;
using System.Reflection;

namespace Konamiman.Nestor80.N80
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var sourceFileName = Path.Combine(Assembly.GetExecutingAssembly().Location, @"../../../../../SOURCE.MAC");
            var sourceStream = new FileStream(sourceFileName, FileMode.Open, FileAccess.Read);

            var outputFileName = Path.Combine(Assembly.GetExecutingAssembly().Location, @"../../../../../SOURCE.REL");
            var ourputStream = new FileStream(outputFileName, FileMode.Create, FileAccess.Write);

            var config = new AssemblyConfiguration() {
                DefaultProgramName = "SOURCE",
                ListFalseConditionals = true,
                SourceStream = sourceStream,
                Print = (s) => Debug.WriteLine(s),
                TargetStream = ourputStream,
                MaxLineLength = 20
            };

            var assembler = new Assembler.Assembler(config);
            var result = assembler.Assemble();
        }
    }
}