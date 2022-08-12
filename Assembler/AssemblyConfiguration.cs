using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Konamiman.Nestor80.Assembler
{
    public class AssemblyConfiguration
    {
        public string DefaultProgramName { get; set; }

        public Stream SourceStream { get; set; }

        public Stream TargetStream { get; set; }

        public Stream ListingStream { get; set; }

        public bool ListingAsCrossReference { get; set; }

        public bool OctalListing { get; set; }

        public bool ListFalseConditionals { get; set; }

        public bool Support8bitChars { get; set; }

        public Func<string, Stream> GetStreamForInclude { get; set; }
    }
}
