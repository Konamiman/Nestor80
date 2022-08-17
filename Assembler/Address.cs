namespace Konamiman.Nestor80.Assembler
{
    internal class Address : IAssemblyOutputPart, IExpressionPart
    {
        public static Address AbsoluteZero = new Address(AddressType.ASEG, 0);
        public static Address AbsoluteMinusOne = new Address(AddressType.ASEG, 0xFFFF);

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

        public AddressType Type { get; private set; }

        public ushort Value { get; private set; }

        public string CommonBlockName { get; private set; }

        public bool IsAbsolute
        {
            get
            {
                return Type == AddressType.ASEG;
            }
        }

        public void SetValue(ushort value)
        {
            this.Value = value;
        }

        public void IncValue(ushort amount)
        {
            unchecked {
                this.Value += amount;
            }
        }

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
    }
}
