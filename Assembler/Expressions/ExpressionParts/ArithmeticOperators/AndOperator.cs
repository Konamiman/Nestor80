﻿using Konamiman.Nestor80.Assembler.Relocatable;

namespace Konamiman.Nestor80.Assembler.Expressions.ExpressionParts.ArithmeticOperators
{
    internal class AndOperator : BinaryOperator
    {
        public static AndOperator Instance = new();

        public override int Precedence => 7;

        public override string Name => "AND";

        public override byte ExtendedLinkItemType => 24;

        protected override Address OperateCore(Address value1, Address value2)
        {
            // At least one of the operands must be Absolute
            // Absolute AND <mode> = <mode>

            if (!value1.IsAbsolute && !value2.IsAbsolute)
            {
                throw new InvalidExpressionException($"AND: At least one of the operands must be absolute (attempted {value1.EffectiveType} AND {value2.EffectiveType})");
            }

            var type = value1.IsAbsolute ? value2.Type : value1.Type;

            return new Address(type, (ushort)(value1.Value & value2.Value));
        }
    }
}
