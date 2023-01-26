using System.Text;

namespace Konamiman.Nestor80.Assembler.Relocatable
{
    /// <summary>
    /// Represents a "link item" as defined by the LINK-80 relocatable file format.
    /// </summary>
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
            Type = type;
            AddressType = addressType;
            AddressValue = addressValue;
            SymbolBytes = symbolBytes;
        }

        public static bool Link80Compatibility { get; set; } = false;

        public static LinkItem ForAddressReference(AddressType addressType, ushort addressValue)
        {
            var symbolBytes = new byte[4];
            symbolBytes[0] = (byte)ExtensionLinkItemType.Address;
            symbolBytes[1] = (byte)addressType;
            symbolBytes[2] = (byte)(addressValue & 0xFF);
            symbolBytes[3] = (byte)(addressValue >> 8 & 0xFF);

            return new LinkItem(LinkItemType.ExtensionLinkItem, symbolBytes);
        }

        public static LinkItem ForExternalReference(string symbol)
        {
            if (symbol.Length > AssemblySourceProcessor.MaxEffectiveExternalNameLength  && Link80Compatibility)
            {
                throw new InvalidOperationException($"{nameof(LinkItem)}.{nameof(ForExternalReference)}: {symbol} is longer than 6 characters");
            }

            var symbolLengthInBytes = (Link80Compatibility ? Encoding.ASCII : Encoding.UTF8).GetByteCount(symbol);
            var symbolBytes = new byte[symbolLengthInBytes + 1];
            symbolBytes[0] = (byte)ExtensionLinkItemType.ReferenceExternal;
            (Link80Compatibility ? Encoding.ASCII : Encoding.UTF8).GetBytes(symbol.ToCharArray(), 0, symbol.Length, symbolBytes, 1);

            return new LinkItem(LinkItemType.ExtensionLinkItem, symbolBytes);
        }

        public static LinkItem ForArithmeticOperator(ArithmeticOperatorCode arithmeticOperator)
        {
            var symbolBytes = new byte[2];
            symbolBytes[0] = (byte)ExtensionLinkItemType.ArithmeticOperator;
            symbolBytes[1] = (byte)arithmeticOperator;

            return new LinkItem(LinkItemType.ExtensionLinkItem, symbolBytes);
        }


        public LinkItemType Type { get; set; }

        /// <summary>
        /// Type of the address in the "A" field, if present.
        /// </summary>
        public AddressType AddressType { get; set; }

        /// <summary>
        /// Value of the address in the "A" field, if present.
        /// </summary>
        public ushort AddressValue { get; set; }

        /// <summary>
        /// Bytes in the "B" field, if present.
        /// </summary>
        public byte[] SymbolBytes { get; set; }

        public bool HasAddress => Type >= LinkItemType.DefineCommonSize;

        public bool HasSymbolBytes => Type <= LinkItemType.DefineEntryPoint;

        public bool IsExternalReference => Type == LinkItemType.ExtensionLinkItem && (ExtensionLinkItemType)SymbolBytes[0] == ExtensionLinkItemType.ReferenceExternal;

        public bool IsAddressReference => Type == LinkItemType.ExtensionLinkItem && (ExtensionLinkItemType)SymbolBytes[0] == ExtensionLinkItemType.Address;

        public ushort ReferencedAddressValue => (ushort)(SymbolBytes[2] | SymbolBytes[3] << 8);

        public AddressType ReferencedAddressType => (AddressType)SymbolBytes[1];

        public ArithmeticOperatorCode? ArithmeticOperator =>
            Type == LinkItemType.ExtensionLinkItem && (ExtensionLinkItemType)SymbolBytes[0] == ExtensionLinkItemType.ArithmeticOperator ?
            (ArithmeticOperatorCode)SymbolBytes[1] : null;

        public bool IsPlusOrMinus => ArithmeticOperator is ArithmeticOperatorCode.Plus or ArithmeticOperatorCode.Minus;

        public string GetSymbolName() => (Link80Compatibility ? Encoding.ASCII : Encoding.UTF8).GetString(SymbolBytes.Skip(1).ToArray());

        public (AddressType, ushort) GetReferencedAddress() => ((AddressType)SymbolBytes[1], (ushort)(SymbolBytes[2] | SymbolBytes[3] << 8));

        public override string ToString()
        {
            var s = nameof(LinkItem);

            if (Type == LinkItemType.ExtensionLinkItem)
            {
                var specialLinkItemType = (ExtensionLinkItemType)SymbolBytes[0];
                if (specialLinkItemType == ExtensionLinkItemType.Address)
                {
                    var addressType = (AddressType)SymbolBytes[1];
                    var addressValue = (ushort)(SymbolBytes[2] | SymbolBytes[3] << 8);
                    s += $", Reference address, {addressType} {addressValue:X4}";
                }
                else if (specialLinkItemType == ExtensionLinkItemType.ReferenceExternal)
                {
                    s += $", Reference external, {GetSymbolName()}";
                }
                else if (specialLinkItemType == ExtensionLinkItemType.ArithmeticOperator)
                {
                    s += $", Arithmetic operator, {(ArithmeticOperatorCode)SymbolBytes[1]}";
                }
                else
                {
                    s += $", unknown extension link item code: {Type}";
                }
            }
            else
            {
                s += $", {Type}";
                if (HasAddress)
                {
                    s += $", {AddressType} {AddressValue:X4}";
                }
                if (HasSymbolBytes)
                {
                    s += $", {Encoding.UTF8.GetString(SymbolBytes)}";
                }
            }

            return s;
        }
    }
}
