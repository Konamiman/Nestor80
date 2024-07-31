using Konamiman.Nestor80.Assembler.Relocatable;

namespace Konamiman.Nestor80.Assembler.Infrastructure
{
    /// <summary>
    /// Holds information about a symbol found in the source code.
    /// </summary>
    /// <remarks>
    /// This is an internal class used during the assembly process. <see cref="Symbol"/>, on the other hand,
    /// is public and is used to return the symbols list in <see cref="AssemblyResult"/>.
    /// </remarks>
    internal class SymbolInfo
    {
        public static bool Link80Compatibility { get; set; }

        public static bool IsSdccBuild { get; set; }

        private SymbolType _Type;
        public SymbolType Type
        {
            get => _Type;
            set
            {
                if (Type == SymbolType.External && HasKnownValue)
                {
                    throw new ArgumentNullException("The symbol has a value, it can't be declared as external");
                }
                _Type = value;
                SetEffectiveName();
            }
        }

        private string _Name;
        public string Name
        {
            get => _Name;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentNullException("Symbol name can't be empty");
                }

                _Name = value;
                SetEffectiveName();
            }
        }

        private string _SdccAreaName;
        public string SdccAreaName
        {
            get => _SdccAreaName;
            set
            {
                if(IsSdccBuild && string.IsNullOrWhiteSpace(value)) {
                    throw new ArgumentException("SDCC area name can't be empty");
                }

                _SdccAreaName = value;
                SetEffectiveName();
            }
        }

        public bool SdccAreaIsAbs { get; set; }

        public bool IsLabel => Type == SymbolType.Label;

        public bool IsExternal => Type == SymbolType.External;

        public bool IsOfKnownType => Type != SymbolType.Unknown;

        public bool IsConstant => Type == SymbolType.Equ || Type == SymbolType.Defl;

        public bool IsRedefinible => Type == SymbolType.Defl;

        /// <summary>
        /// If the symbol is declared as public its effective name (the name that will be used to refer to the symbol
        /// in the resulting relocatable file) is the original name truncated to 6 characters in LINK-80 compatibility name.
        /// This is a limitation of the LINK-80 relocatable file format.
        /// </summary>
        public string EffectiveName { get; private set; }

        private bool _IsPublic;
        public bool IsPublic
        {
            get => _IsPublic;
            set
            {
                if (value && Type == SymbolType.External)
                {
                    throw new ArgumentNullException("The symbol is declared as external, it can't be declared as public");
                }
                _IsPublic = value;
                SetEffectiveName();
            }

        }

        private void SetEffectiveName()
        {
            if (Link80Compatibility && (IsExternal || IsPublic) && Name.Length > AssemblySourceProcessor.MaxEffectiveExternalNameLength)
                EffectiveName = Name[..AssemblySourceProcessor.MaxEffectiveExternalNameLength].ToUpper();
            else
                EffectiveName = Name;
        }

        private Address _Value;
        internal Address Value
        {
            get => _Value;
            set
            {
                if (Value is not null && IsExternal)
                {
                    throw new InvalidOperationException($"Can't set a value for symbol {Name}, it's declared as external");
                }
                _Value = value;
            }
        }

        public bool HasKnownValue => Value is not null;

        public bool IsNonRelativeLabel { get; set; } = false;

        public override string ToString()
        {
            var value = HasKnownValue ? $" = {Value:X4}" : "";
            return $"{EffectiveName}, {(IsPublic ? "public " : "")}{Type}{value}";
        }
    }
}
