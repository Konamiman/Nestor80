using Konamiman.Nestor80.Assembler.Relocatable;

namespace Konamiman.Nestor80.Assembler.Expressions.ExpressionParts
{
    /// <summary>
    /// Base class for the arithmetic operators to be used in expressions.
    /// </summary>
    internal abstract class ArithmeticOperator : IExpressionPart
    {
        protected ArithmeticOperator() { }

        public abstract int Precedence { get; }

        public abstract string Name { get; }

        public abstract bool IsUnary { get; }

        /// <summary>
        /// The "Arithmetic operator" extended link item as defined by the LINK-80 relocatable
        /// file format admits only a subset of the existing operators.
        /// Those will have this property redefined so that it returns the proper operator code
        /// as expected by LINK-80.
        /// </summary>
        public virtual byte? ExtendedLinkItemType => null;

        public bool AllowedForRelocatableSymbols => ExtendedLinkItemType != null;

        protected static Address AbsoluteZero => new Address(AddressType.ASEG, 0);

        protected static Address AbsoluteMinusOne => new Address(AddressType.ASEG, 0xFFFF);

        public Address Operate(Address value1, Address value2)
        {
            if (IsUnary && value2 is not null)
            {
                throw new ArgumentException($"Operator \"{Name}\" is unary, can't apply to two values");
            }
            if (!IsUnary && value2 is null)
            {
                throw new ArgumentException($"Operator \"{Name}\" is not unary, can't apply to only one value");
            }

            return OperateCore(value1, value2);
        }

        protected abstract Address OperateCore(Address value1, Address value2);

        public override string ToString() => Name;

        public static bool operator ==(ArithmeticOperator operator1, object operator2)
        {
            if (operator2 is not Address)
                return false;

            if (operator1 is null)
                return operator2 is null;

            return operator1.Equals(operator2);
        }

        public static bool operator !=(ArithmeticOperator operator1, object operator2)
        {
            return !(operator1 == operator2);
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
                return false;

            var b2 = (ArithmeticOperator)obj;
            return b2.GetType() == GetType() && b2.Name == Name;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }
    }
}
