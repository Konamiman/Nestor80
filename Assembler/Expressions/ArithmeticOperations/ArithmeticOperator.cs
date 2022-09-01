using Konamiman.Nestor80.Assembler.Expressions;

namespace Konamiman.Nestor80.Assembler.ArithmeticOperations
{
    internal abstract class ArithmeticOperator : IExpressionPart, IAssemblyOutputPart
    {
        protected ArithmeticOperator() { }

        public abstract int Precedence { get; }

        public abstract string Name { get; }

        public abstract bool IsUnary { get; }

        public virtual byte? ExtendedLinkItemType => null;

        public bool AllowedForRelocatableSymbols => ExtendedLinkItemType != null;

        protected Address AbsoluteZero => new Address(AddressType.ASEG, 0);

        protected Address AbsoluteMinusOne => new Address(AddressType.ASEG, 0xFFFF);

        public Address Operate(Address value1, Address value2)
        {
            if(IsUnary && value2 is not null) {
                throw new ArgumentException($"Operator \"{Name}\" is unary, can't apply to two values");
            }
            if(!IsUnary && value2 is null) {
                throw new ArgumentException($"Operator \"{Name}\" is not unary, can't apply to only one value");
            }

            return OperateCore(value1, value2);
        }

        protected abstract Address OperateCore(Address value1, Address value2);

        public override string ToString() => Name;

        public static bool operator ==(ArithmeticOperator operator1, object operator2)
        {
            if(operator2 is not Address)
                return false;

            if(operator1 is null)
                return operator2 is null;

            return operator1.Equals(operator2);
        }

        public static bool operator !=(ArithmeticOperator operator1, object operator2)
        {
            return !(operator1 == operator2);
        }

        public override bool Equals(object obj)
        {
            if(obj == null || GetType() != obj.GetType())
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
