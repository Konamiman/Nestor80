namespace Konamiman.Nestor80.Assembler
{
    internal class SymbolInfo
    {
        private SymbolType _Type;
        public SymbolType Type
        {
            get => _Type;
            set
            {
                if(Type == SymbolType.External && HasKnownValue) {
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
                if(string.IsNullOrWhiteSpace(value)) {
                    throw new ArgumentNullException("Symbol name can't be empty");
                }

                _Name = value;
                SetEffectiveName();
            }
        }

        public bool IsLabel => Type == SymbolType.Label;

        public bool IsExternal => Type == SymbolType.External;

        public bool IsOfKnownType => Type != SymbolType.Unknown;

        public bool IsConstant => Type == SymbolType.Equ || Type == SymbolType.Defl;

        public bool IsRedefinible => Type == SymbolType.Defl;

        public string EffectiveName { get; private set; }

        private bool _IsPublic;
        public bool IsPublic
        {
            get => _IsPublic;
            set
            {
                if(value && Type == SymbolType.External) {
                    throw new ArgumentNullException("The symbol is declared as external, it can't be declared as public");
                }
                _IsPublic = value;
                SetEffectiveName();
            }
                
        }

        private void SetEffectiveName()
        {
            if((IsExternal || IsPublic) && Name.Length > AssemblySourceProcessor.MaxEffectiveExternalNameLength)
                EffectiveName = Name[..AssemblySourceProcessor.MaxEffectiveExternalNameLength].ToUpper();
            else
                EffectiveName = Name.ToUpper();
        }

        private Address _Value;
        internal Address Value 
        {
            get => _Value;
            set
            {
                if(Value is not null && IsExternal) {
                    throw new InvalidOperationException($"Can't set a value for symbol {Name}, it's declared as external");
                }
                _Value = value;
            }
        }

        public bool HasKnownValue => Value is not null;

        public override string ToString()
        {
            var value = HasKnownValue ? $" = {Value:X4}" : "";
            return $"{EffectiveName}, {(IsPublic ? "public " : "")}{Type}{value}";
        }
    }
}
