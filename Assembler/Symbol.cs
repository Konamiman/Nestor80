using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Konamiman.Nestor80.Assembler
{
    internal class Symbol
    {
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
                if(value != _IsExternal) {
                    _IsExternal = value;
                    SetEffectiveName();
                }
            }
        }

        private void SetEffectiveName()
        {
            var maxLength = _IsExternal ? 6 : 16;
            EffectiveName = Name.Substring(0, maxLength).ToUpper();
        }

        public Address Value { get; set; }

        public bool IsKnown => Value != null;
    }
}
