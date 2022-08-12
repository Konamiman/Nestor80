namespace Konamiman.Nestor80.Assembler
{
    internal class Address
    {
        public static Address AbsoluteZero = new Address(AddressType.Absolute, 0);
        public static Address AbsoluteMinusOne = new Address(AddressType.Absolute, 0xFFFF);

        public Address(AddressType type, ushort value, string commonBlockName = null)
        {
            if(type == AddressType.Common && commonBlockName == null) {
                throw new ArgumentException("A common address must have a common block name");
            }
            if(type != AddressType.Common && commonBlockName != null) {
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
                return Type == AddressType.Absolute;
            }
        }

        public bool SameModeAs(Address address2)
        {
            if(Type == AddressType.Common && address2.Type == AddressType.Common) {
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

        public static implicit operator int(Address address)
        {
            if(!address.IsAbsolute) {
                throw new InvalidOperationException($"Only absolute addresses can be cast to int (address type is {address.Type})");
            }

            return address.Value;
        }

        public static Address operator +(Address address1, Address address2)
        {
            // At least one of the operands must be Absolute
            // Absolute + <mode> = <mode>

            if(!address1.IsAbsolute && !address2.IsAbsolute) {
                throw new InvalidOperationException($"Can't add addresses in different modes (attempted {address1.Type} + {address2.Type}");
            }

            var type = address1.IsAbsolute ? address2.Type : address1.Type;

            unchecked {
                return new Address(type, (ushort)(address1.Value + address2.Value));
            }
        }

        public static Address operator -(Address address1, Address address2)
        {
            // <mode> - Absolute = <mode>
            // <mode> - <mode> = Absolute, where the two <mode>s are the same

            if(!address2.IsAbsolute && (address1.Type != address2.Type)) {
                throw new InvalidOperationException($"Can't substract addresses in different modes if the second one is not absolute (attempted {address1.Type} - {address2.Type}");
            }

            var type = address2.IsAbsolute ? address1.Type : AddressType.Absolute;

            unchecked {
                return new Address(type, (ushort)(address1.Value + address2.Value));
            }
        }

        public static Address operator *(Address address1, Address address2)
        {
            // One of the operands must be absolute
            // <mode1> * <mode2> = <mode2>

            if(!address1.IsAbsolute || !address2.IsAbsolute) {
                throw new InvalidOperationException($"Can't multiply addresses if neither of them is absolute (attempted {address1.Type} * {address2.Type}");
            }

            unchecked {
                return new Address(address2.Type, (ushort)(address1.Value + address2.Value));
            }
        }

        public static Address operator /(Address address1, Address address2)
        {
            // One of the operands must be absolute
            // <mode> / Absolute = <mode>
            // Absolute / <mode> = <mode>

            if(!address1.IsAbsolute || !address2.IsAbsolute) {
                throw new InvalidOperationException($"Can't divide addresses if neither of them is absolute (attempted {address1.Type} / {address2.Type}");
            }

            var type = address1.IsAbsolute ? address2.Type : address1.Type;

            unchecked {
                return new Address(type, (ushort)(address1.Value / address2.Value));
            }
        }

        public static Address operator %(Address address1, Address address2)
        {
            // One of the operands must be absolute
            // <mode> MOD Absolute = <mode>
            // Absolute MOD <mode> = <mode>

            if(!address1.IsAbsolute || !address2.IsAbsolute) {
                throw new InvalidOperationException($"Can't MOD addresses if neither of them is absolute (attempted {address1.Type} MOD {address2.Type}");
            }

            var type = address1.IsAbsolute ? address2.Type : address1.Type;

            unchecked {
                return new Address(type, (ushort)(address1.Value % address2.Value));
            }
        }

        public static Address operator >>(Address address1, int value)
        {
            // SHR: The second operator must be absolute

            unchecked {
                return new Address(address1.Type, (ushort)(address1.Value >> value));
            }
        }

        public static Address operator <<(Address address1, int value)
        {
            // SHL: The second operator must be absolute

            unchecked {
                return new Address(address1.Type, (ushort)(address1.Value << value));
            }
        }

        public static Address operator ~(Address address)
        {
            // NOT: The result is of the same type

            unchecked {
                return new Address(address.Type, (ushort)~address.Value);
            }
        }

        public static Address operator ==(Address address1, Address address2)
        {
            // EQ: Both addresses must be in the same mode

            if(!address1.SameModeAs(address2)) {
                throw new InvalidOperationException($"Can't compare addresses if they aren't in the same mode (attempted {address1.Type} EQ {address2.Type}");
            }

            return address1.Value == address2.Value ? AbsoluteMinusOne : AbsoluteZero;
        }

        public static Address operator !=(Address address1, Address address2)
        {
            // NE: Both addresses must be in the same mode

            if(!address1.SameModeAs(address2)) {
                throw new InvalidOperationException($"Can't compare addresses if they aren't in the same mode (attempted {address1.Type} NE {address2.Type}");
            }

            return address1.Value != address2.Value ? AbsoluteMinusOne : AbsoluteZero;
        }

        public static Address operator <(Address address1, Address address2)
        {
            // LT: Both addresses must be in the same mode

            if(!address1.SameModeAs(address2)) {
                throw new InvalidOperationException($"Can't compare addresses if they aren't in the same mode (attempted {address1.Type} LT {address2.Type}");
            }

            return address1.Value < address2.Value ? AbsoluteMinusOne : AbsoluteZero;
        }

        public static Address operator >(Address address1, Address address2)
        {
            // GT: Both addresses must be in the same mode

            if(!address1.SameModeAs(address2)) {
                throw new InvalidOperationException($"Can't compare addresses if they aren't in the same mode (attempted {address1.Type} GT {address2.Type}");
            }

            return address1.Value > address2.Value ? AbsoluteMinusOne : AbsoluteZero;
        }

        public static Address operator <=(Address address1, Address address2)
        {
            // LE: Both addresses must be in the same mode

            if(!address1.SameModeAs(address2)) {
                throw new InvalidOperationException($"Can't compare addresses if they aren't in the same mode (attempted {address1.Type} LE {address2.Type}");
            }

            return address1.Value <= address2.Value ? AbsoluteMinusOne : AbsoluteZero;
        }

        public static Address operator >=(Address address1, Address address2)
        {
            // GE: Both addresses must be in the same mode

            if(!address1.SameModeAs(address2)) {
                throw new InvalidOperationException($"Can't compare addresses if they aren't in the same mode (attempted {address1.Type} GE {address2.Type}");
            }

            return address1.Value >= address2.Value ? AbsoluteMinusOne : AbsoluteZero;
        }

        public override bool Equals(object obj)
        {
            return obj is Address && (this == (Address)obj).Value != 0;
        }

        public override int GetHashCode()
        {
            var hash = ((byte)Type << 16) | Value;
            if(Type != AddressType.Common) {
                return hash;
            }

            return hash ^ CommonBlockName.GetHashCode();
        }
    }
}
