using Konamiman.Nestor80.Assembler;

namespace Konamiman.Nestor80.Linker
{
    public class AddressRange
    {
        public AddressRange(ushort start, ushort end, AddressType type, string commonBlockName = null)
        {
            Start = start;
            End = end;
            //Type = type;
            CommonBlockName = commonBlockName;
        }

        public ushort Start { get; }

        public ushort End { get; }

        //public AddressType Type { get; }

        public string CommonBlockName { get; }

        public static AddressRange Intersection(AddressRange range1, AddressRange range2)
        {
            /*if(range1.Type != range2.Type) {
                throw new InvalidOperationException($"{nameof(AddressRange)}.{nameof(Intersection)}: both ranges must be of the same type, got {range1.Type} and {range2.Type}");
            }*/

            if(range1.CommonBlockName != range2.CommonBlockName) {
                throw new InvalidOperationException($"{nameof(AddressRange)}.{nameof(Intersection)}: both ranges must be in the same common block, got {range1.CommonBlockName} and {range2.CommonBlockName}");
            }

            return
                range2.Start > range1.End || range1.Start > range2.End ?
                null :
                new AddressRange(Math.Max(range1.Start, range2.Start), Math.Min(range1.End, range2.End), AddressType.ASEG, range1.CommonBlockName);
        }
    }
}
