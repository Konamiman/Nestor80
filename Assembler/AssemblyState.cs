using Konamiman.Nestor80.Assembler.Output;

namespace Konamiman.Nestor80.Assembler
{
    internal class AssemblyState
    {
        private readonly List<AssemblyError> Errors = new();

        public AssemblyConfiguration Configuration { get; init; }

        public bool InPass2 { get; private set; } = false;

        public bool InPass1 => !InPass2;

        public bool HasErrors => Errors.Any(e => !e.IsWarning);

        public int CurrentLineNumber { get; private set; } = 1;

        public string ProgramName { get; set; }

        public void SwitchToPass2()
        {
            InPass2 = true;
            CurrentLineNumber = 1;
            ProgramName = Configuration.DefaultProgramName;

            CurrentLocationArea = AddressType.CSEG;

            LocationPointersByArea[AddressType.CSEG] = 0;
            LocationPointersByArea[AddressType.DSEG] = 0;
            LocationPointersByArea[AddressType.ASEG] = 0;
        }

        private readonly Dictionary<AddressType, ushort> LocationPointersByArea = new() {
            {AddressType.CSEG, 0},
            {AddressType.DSEG, 0},
            {AddressType.ASEG, 0}
        };

        public AddressType CurrentLocationArea { get; private set; } = AddressType.CSEG;

        public ushort CurrentLocationPointer { get; private set; }

        public void SwitchToArea(AddressType area)
        {
            //TODO: Handle sizes of commons
            if(area == CurrentLocationArea)
                return;

            if(area == AddressType.COMMON) {
                CurrentLocationPointer = 0;
            }
            else { 
                LocationPointersByArea[CurrentLocationArea] = CurrentLocationPointer;
                CurrentLocationPointer = LocationPointersByArea[area];
            }

            CurrentLocationArea = area;
        }

        public ushort GetLocationPointer(AddressType area)
        {
            //TODO: Handle commons
            if(area != AddressType.COMMON) {
                return LocationPointersByArea[area];
            }

            return 0;
        }

        public void IncreaseLocationPointer(int amount) => CurrentLocationPointer += (ushort)amount;

        public void IncreaseLineNumber() => CurrentLineNumber++;

        public void AddError(AssemblyError error) => Errors.Add(error);

        public void AddError(AssemblyErrorCode code, string message, bool withLineNumber = true)
        {
            AddError(new AssemblyError(code, message, withLineNumber ? CurrentLineNumber : null ));
        }

        public AssemblyError[] GetErrors() => Errors.ToArray();

        private Dictionary<string, Symbol> Symbols = new(StringComparer.InvariantCultureIgnoreCase);

        public Symbol[] GetSymbols() => Symbols.Values.ToArray();

        public void WrapUp()
        {
            //TODO: Handle sizes of commons
            if(CurrentLocationArea != AddressType.COMMON) {
                LocationPointersByArea[CurrentLocationArea] = CurrentLocationPointer;
            }
        }

        public Symbol GetSymbol(string name)
        {
            if(name == "$")
                return new Symbol() { Name = "$", Value = new Address(CurrentLocationArea, CurrentLocationPointer) };

            return Symbols.ContainsKey(name) ? Symbols[name] : null;
        }
    }
}
