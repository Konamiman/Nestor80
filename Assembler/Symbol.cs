namespace Konamiman.Nestor80.Assembler
{
    public class Symbol
    {
        internal static int MaxEffectivePublicNameLength {get; set;} = 16;
        internal const int MaxEffectiveExternalNameLength = 6;

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

        public string EffectiveName { get; private set; }

        public bool IsPublic { get; set; }

        private bool _IsExternal;
        public bool IsExternal
        {
            get => _IsExternal;
            set
            {
                if(value == _IsExternal)
                    return;

                if(value && _Value is not null) {
                    throw new InvalidOperationException($"Can't set {Name} as external, it has a value assigned");
                }

                _IsExternal = value;
                SetEffectiveName();
            }
        }

        private void SetEffectiveName()
        {
            var maxLength = _IsExternal ? MaxEffectiveExternalNameLength : MaxEffectivePublicNameLength;
            EffectiveName = Name.Length > maxLength ? Name[..maxLength].ToUpper() : Name.ToUpper();
        }

        private Address _Value;
        public Address Value 
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

        public bool IsKnown => Value is not null;

        public override string ToString()
        {
            var suffix = IsPublic ? "::" : (IsExternal ? "##" : "");
            var value = IsKnown ? $" = {Value:X4}" : "";
            return $"{EffectiveName}{suffix}{value}";
        }
    }
}
