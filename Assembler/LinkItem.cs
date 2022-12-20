using System.Text;

namespace Konamiman.Nestor80
{
    public class LinkItem
    {
        public LinkItem(LinkItemType type, byte[] symbolBytes)
            : this(type, AddressType.ASEG, 0, symbolBytes)
        { }

#if false
        public LinkItem(LinkItemType type, AddressType addressType, ushort addressValue)
            : this(type, addressType, addressValue, Array.Empty<byte>())
        { }

        public LinkItem(LinkItemType type, string symbol)
            : this(type, Encoding.ASCII.GetBytes(symbol))
        { }

        public LinkItem(LinkItemType type, AddressType addressType, ushort addressValue, string symbol)
            : this(type, addressType, addressValue, Encoding.ASCII.GetBytes(symbol))
        { }
#endif

        public LinkItem(LinkItemType type, AddressType addressType, ushort addressValue, byte[] symbolBytes)
        {
            this.Type = type;
            this.AddressType = addressType;
            this.AddressValue = addressValue;
            this.SymbolBytes = symbolBytes;
        }

        public static LinkItem ForAddressReference(AddressType addressType, ushort addressValue)
        {
            var symbolBytes = new byte[4];
            symbolBytes[0] = (byte)SpecialLinkItemType.Address;
            symbolBytes[1] = (byte)addressType;
            symbolBytes[2] = (byte)(addressValue & 0xFF);
            symbolBytes[3] = (byte)((addressValue >> 8) & 0xFF);

            return new LinkItem(LinkItemType.ExtensionLinkItem, symbolBytes);
        }

        public static LinkItem ForExternalReference(string symbol)
        {
            if(symbol.Length > 6) {
                throw new InvalidOperationException($"{nameof(LinkItem)}.{nameof(ForExternalReference)}: {symbol} is longer than 6 characters");
            }

            var symbolBytes = new byte[symbol.Length+1];
            symbolBytes[0] = (byte)SpecialLinkItemType.ReferenceExternal;
            Encoding.ASCII.GetBytes(symbol.ToCharArray(), 0, symbol.Length, symbolBytes, 1);

            return new LinkItem(LinkItemType.ExtensionLinkItem, symbolBytes);
        }

        public static LinkItem ForArithmeticOperator(ArithmeticOperatorCode arithmeticOperator)
        {
            var symbolBytes = new byte[2];
            symbolBytes[0] = (byte)SpecialLinkItemType.ArithmeticOperator;
            symbolBytes[1] = (byte)arithmeticOperator;

            return new LinkItem(LinkItemType.ExtensionLinkItem, symbolBytes);
        }


        public LinkItemType Type { get; set; }

        public AddressType AddressType { get; set; }

        public ushort AddressValue { get; set; }

        public byte[] SymbolBytes { get; set; }

        public bool HasAddress => Type >= LinkItemType.DefineCommonSize;

        public bool HasSymbolBytes => Type <= LinkItemType.DefineEntryPoint;

        public bool IsExternalReference => Type == LinkItemType.ExtensionLinkItem && (SpecialLinkItemType)SymbolBytes[0] == SpecialLinkItemType.ReferenceExternal;

        public bool IsAddressReference => Type == LinkItemType.ExtensionLinkItem && (SpecialLinkItemType)SymbolBytes[0] == SpecialLinkItemType.Address;

        public ushort ReferencedAddressValue => (ushort)(SymbolBytes[2] | (SymbolBytes[3] << 8));

        public AddressType ReferencedAddressType => (AddressType)SymbolBytes[1];

        public ArithmeticOperatorCode? ArithmeticOperator =>
            Type == LinkItemType.ExtensionLinkItem && (SpecialLinkItemType)SymbolBytes[0] == SpecialLinkItemType.ArithmeticOperator ?
            (ArithmeticOperatorCode)SymbolBytes[1] : null;

        public bool IsPlusOrMinus => ArithmeticOperator is ArithmeticOperatorCode.Plus or ArithmeticOperatorCode.Minus;

        public string GetSymbolName() => Encoding.ASCII.GetString(SymbolBytes.Skip(1).ToArray());

        public (AddressType, ushort) GetReferencedAddress() => ((AddressType)SymbolBytes[1], (ushort)(SymbolBytes[2] | (SymbolBytes[3] << 8)));

        public override string ToString()
        {
            var s = nameof(LinkItem);

            if(Type == LinkItemType.ExtensionLinkItem) {
                var specialLinkItemType = (SpecialLinkItemType)SymbolBytes[0];
                if(specialLinkItemType == SpecialLinkItemType.Address) {
                    var addressType = (AddressType)SymbolBytes[1];
                    var addressValue = (ushort)(SymbolBytes[2] | (SymbolBytes[3] << 8));
                    s += $", Reference address, {addressType} {addressValue:X4}";
                }
                else if(specialLinkItemType == SpecialLinkItemType.ReferenceExternal) {
                    s += $", Reference external, {GetSymbolName()}";
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
