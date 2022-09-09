using System.Text;

namespace Konamiman.Nestor80
{
    public class LinkItem
    {
        public LinkItemType Type { get; set; }

        public AddressType AddressType { get; set; }

        public ushort AddressValue { get; set; }

        public byte[] SymbolBytes { get; set; }

        public bool HasAddress => Type >= LinkItemType.DefineCommonSize;

        public bool HasSymbolBytes => Type <= LinkItemType.DefineEntryPoint;

        public override string ToString()
        {
            var s = base.ToString();

            if(Type == LinkItemType.ExtensionLinkItem) {
                var specialLinkItemType = (SpecialLinkItemType)SymbolBytes[0];
                if(specialLinkItemType == SpecialLinkItemType.Address) {
                    s += $", Reference address, {AddressType} {AddressValue:X4}";
                }
                else if(specialLinkItemType == SpecialLinkItemType.ReferenceExternal) {
                    s += $", Reference external, {Encoding.ASCII.GetString(SymbolBytes)}";
                }
                else if(specialLinkItemType == SpecialLinkItemType.ArithmeticOperator) {
                    s += $", Arithmetic operator, {(ArithmeticOperatorCode)SymbolBytes[1]}";
                }
                else {
                    s += $", unknown extension link item code: {Type}";
                }
            }
            else {
                s += $", {Type}";
                if(HasAddress) {
                    s += $", {AddressType} {AddressValue:X4}";
                }
                if(HasSymbolBytes) {
                    s += $", {Encoding.ASCII.GetString(SymbolBytes)}";
                }
            }

            return s;
        }
    }
}
