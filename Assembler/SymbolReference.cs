using Konamiman.Nestor80.Assembler.Expressions;

namespace Konamiman.Nestor80.Assembler
{
    public class SymbolReference : IExpressionPart
    {
        public static SymbolReference For(string name, bool isExternal = false) => new() { SymbolName = name, IsExternal = isExternal };

        public string SymbolName { get; set; }

        public bool IsExternal { get; set; }

        public static bool operator ==(SymbolReference symbolref1, object symbolref2)
        {
            if(symbolref2 is not Address)
                return false;

            if(symbolref1 is null)
                return symbolref2 is null;

            return symbolref1.Equals(symbolref2);
        }

        public static bool operator !=(SymbolReference symbolref1, object symbolref2)
        {
            return !(symbolref1 == symbolref2);
        }

        public override bool Equals(object obj)
        {
            if(obj == null || GetType() != obj.GetType())
                return false;

            var b2 = (SymbolReference)obj;
            return SymbolName == b2.SymbolName && IsExternal == b2.IsExternal;
        }

        public override int GetHashCode()
        {
            return $"{SymbolName}#{IsExternal}".GetHashCode();
        }

        public override string ToString()
        {
            return IsExternal ? $"{SymbolName}##" : SymbolName;
        }
    }
}
