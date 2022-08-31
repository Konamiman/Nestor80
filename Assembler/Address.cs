namespace Konamiman.Nestor80.Assembler
{
    public class Address : IAssemblyOutputPart, IExpressionPart
    {
        public static Address AbsoluteZero = new(AddressType.ASEG, 0);
        public static Address AbsoluteMinusOne = new(AddressType.ASEG, 0xFFFF);

        public Address(AddressType type, ushort value, string commonBlockName = null)
        {
            if(type == AddressType.COMMON && commonBlockName == null) {
                throw new ArgumentException("A common address must have a common block name");
            }
            if(type != AddressType.COMMON && commonBlockName != null) {
                throw new ArgumentException("A non-common address can't have a common block name");
            }

            Type = type;
            Value = value;
            CommonBlockName = commonBlockName;
        }

        public static Address Absolute(ushort value) => new(AddressType.ASEG, value);
        public static Address Code(ushort value) => new(AddressType.CSEG, value);
        public static Address Data(ushort value) => new(AddressType.DSEG, value);

        public AddressType Type { get; private set; }

        public ushort Value { get; private set; }

        public string CommonBlockName { get; private set; }

        public bool IsAbsolute => Type == AddressType.ASEG;
        public bool IsCommon => Type == AddressType.COMMON;

        public bool SameModeAs(Address address2)
        {
            if(Type == AddressType.COMMON && address2.Type == AddressType.COMMON) {
                return CommonBlockName == address2.CommonBlockName;
            }

            return Type == address2.Type;
        }

        public bool IsValidByte
        {
            get
            {
                return Value < 256 || Value >= 0xFF00;
            }
        }

        public byte ValueAsByte
        {
            get
            {
                return (byte)(Value & 0xFF);
            }
        }

        public override string ToString()
        {
            return $"{Type} {Value:X4}";
        }

        public static bool operator ==(Address address1, object address2)
        {
            if(address2 is not Address)
                return false;

            if(address1 is null)
                return address2 is null;

            return address1.Equals(address2);
        }

        public static bool operator !=(Address address1, object address2)
        {
            return !(address1 == address2);
        }

        public override bool Equals(object obj)
        {
            if(obj == null || GetType() != obj.GetType())
                return false;

            var b2 = (Address)obj;
            return (Type == b2.Type && Value == b2.Value && (!IsCommon || CommonBlockName == b2.CommonBlockName));
        }

        public override int GetHashCode()
        {
            var result = Value | ((int)Type << 16);
            if(CommonBlockName is not null) {
                result ^= CommonBlockName.GetHashCode();
            }
            return result;
        }
    }
}
