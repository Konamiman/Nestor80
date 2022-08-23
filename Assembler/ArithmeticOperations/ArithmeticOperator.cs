namespace Konamiman.Nestor80.Assembler.ArithmeticOperations
{
    internal abstract class ArithmeticOperator : IExpressionPart, IAssemblyOutputPart
    {
        protected ArithmeticOperator() { }

        public abstract int Precedence { get; }

        public abstract string Name { get; }

        public virtual bool IsUnary => false;

        public virtual byte? ExtendedLinkItemType => null;

        public bool AllowedForRelocatableSymbols => ExtendedLinkItemType != null;

        protected Address AbsoluteZero => new Address(AddressType.ASEG, 0);

        protected Address AbsoluteMinusOne => new Address(AddressType.ASEG, 0xFFFF);

        public Address Operate(Address value1, Address value2)
        {
            if(IsUnary && value2 != null) {
                throw new ArgumentException($"Operator \"{Name}\" is unary, can't apply to two values");
            }
            if(!IsUnary && value2 == null) {
                throw new ArgumentException($"Operator \"{Name}\" is not unary, can't apply to only one value");
            }

            return OperateCore(value1, value2);
        }

        protected abstract Address OperateCore(Address value1, Address value2);

        public override string ToString() => Name;
    }
}
