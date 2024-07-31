namespace Konamiman.Nestor80.Assembler.Relocatable
{
    public class SdccArea
    {
        public SdccArea(): this(null, false, false)
        {
        }

        public SdccArea(string name, bool isAbsolute, bool isOverlay)
        {
            Name = name;
            IsAbsolute = isAbsolute;
            IsOverlay = isOverlay;

            PrintableModifiers =
                IsAbsolute && IsOverlay ? "(ABS,OVR)" :
                IsAbsolute && !IsOverlay ? "(ABS,CON)" :
                !IsAbsolute && IsOverlay ? "(REL,OVR)" :
                "(REL,CON)";
        }

        public string Name { get; init; }

        public bool IsAbsolute { get; init; }

        public bool IsOverlay { get; init; }

        public string PrintableModifiers { get; }

        public ushort Address { get; set; } = 0;

        public ushort Size { get; set; } = 0;

        public static bool operator ==(SdccArea area, string name)
        {
            if(area is null || area.Name is null || name is null) {
                return false;
            }

            return string.Equals(area.Name, name, StringComparison.OrdinalIgnoreCase);
        }

        public static bool operator !=(SdccArea area, string name)
        {
            return !(area == name);
        }

        public override bool Equals(object obj)
        {
            if(obj is null) 
                return false;

            if(obj is string name)
                return string.Equals(name, Name);

            if(GetType() != obj.GetType())
                return false;

            var area2 = (SdccArea)obj;
            return this == area2;
        }

        public override int GetHashCode() => Name.ToUpper().GetHashCode();

        public override string ToString() => $"AREA {Name} ({(IsAbsolute ? "ABS" : "REL")},{(IsOverlay ? "OVR" : "CON")}), at {Address:X4}, size {Size}";
    }
}
