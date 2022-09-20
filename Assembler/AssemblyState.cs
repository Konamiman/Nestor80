﻿using Konamiman.Nestor80.Assembler.Expressions;
using Konamiman.Nestor80.Assembler.Output;
using System.Text;

namespace Konamiman.Nestor80.Assembler
{
    internal class AssemblyState
    {
        private readonly List<AssemblyError> Errors = new();

        public AssemblyConfiguration Configuration { get; init; }

        public Encoding DefaultOutputStringEncoding {get;set;}

        public bool InPass2 { get; private set; } = false;

        public bool InPass1 => !InPass2;

        public bool HasErrors => Errors.Any(e => !e.IsWarning);

        public int CurrentLineNumber { get; private set; } = 1;

        public string ProgramName { get; set; }

        public List<ProcessedSourceLine> ProcessedLines { get; } = new();

        public void RegisterPendingExpression(ProcessedSourceLine line, Expression expression, int location = 0, int size = 2)
        {
            if(!ExpressionsPendingEvaluation.ContainsKey(line)) {
                ExpressionsPendingEvaluation[line] = new List<ExpressionPendingEvaluation>();
            }

            ExpressionsPendingEvaluation[line].Add(new ExpressionPendingEvaluation() { Expression = expression, LocationInOutput = location, OutputSize = size } );
        }

        public Dictionary<ProcessedSourceLine, List<ExpressionPendingEvaluation>> ExpressionsPendingEvaluation { get; } = new();

        public Address EndAddress { get; private set; }

        public void End(Address address)
        {
            if(address is null)
                throw new ArgumentNullException(nameof(address));

            EndAddress = address;
        }

        public bool EndReached => EndAddress is not null;

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

        private readonly Dictionary<AddressType, ushort> AreaSizes = new() {
            {AddressType.CSEG, 0},
            {AddressType.DSEG, 0},
            {AddressType.ASEG, 0}
        };

        public AddressType CurrentLocationArea { get; private set; } = AddressType.CSEG;

        public ushort CurrentLocationPointer { get; private set; }

        public Address GetCurrentLocation() => new(CurrentLocationArea, CurrentLocationPointer);

        public void SwitchToArea(AddressType area)
        {
            //TODO: Handle sizes of commons
            if(area == CurrentLocationArea)
                return;

            if(area == AddressType.COMMON) {
                CurrentLocationPointer = 0;
            }
            else {
                AreaSizes[CurrentLocationArea] = Math.Max(AreaSizes[CurrentLocationArea], CurrentLocationPointer);
                LocationPointersByArea[CurrentLocationArea] = CurrentLocationPointer;
                CurrentLocationPointer = LocationPointersByArea[area];
            }

            CurrentLocationArea = area;
        }

        public void SwitchToLocation(ushort location)
        {
            //TODO: Handle commons
            CurrentLocationPointer = location;
            AreaSizes[CurrentLocationArea] = Math.Max(AreaSizes[CurrentLocationArea], CurrentLocationPointer);
        }

        public ushort GetLocationPointer(AddressType area)
        {
            //TODO: Handle commons
            if(area != AddressType.COMMON) {
                return LocationPointersByArea[area];
            }

            return 0;
        }

        public ushort GetAreaSize(AddressType area)
        {
            //TODO: Handle commons
            if(area != AddressType.COMMON) {
                return AreaSizes[area];
            }

            return 0;
        }

        public void IncreaseLocationPointer(int amount) => CurrentLocationPointer += (ushort)amount;

        public void IncreaseLineNumber() => CurrentLineNumber++;

        public void AddError(AssemblyError error) => Errors.Add(error);

        public AssemblyError AddError(AssemblyErrorCode code, string message, bool withLineNumber = true)
        {
            var error = new AssemblyError(code, message, withLineNumber ? CurrentLineNumber : null );
            AddError(error);
            return error;
        }

        public AssemblyError[] GetErrors() => Errors.ToArray();

        private readonly Dictionary<string, SymbolInfo> Symbols = new(StringComparer.InvariantCultureIgnoreCase);

        public SymbolInfo[] GetSymbols() => Symbols.Values.ToArray();

        public bool HasSymbol(string symbol) => Symbols.ContainsKey(symbol);

        public bool SymbolIsKnown(string symbol) => Symbols.ContainsKey(symbol) && Symbols[symbol].HasKnownValue;

        public bool SymbolIsOfKnownType(string symbol) => Symbols.ContainsKey(symbol) && Symbols[symbol].IsOfKnownType;

        public void AddSymbol(string name, SymbolType type, Address value = null, bool isPublic = false) =>
            Symbols.Add(name, new SymbolInfo() { Name = name, Type = type, Value = value, IsPublic = isPublic });

        public void WrapUp()
        {
            //TODO: Handle sizes of commons
            if(CurrentLocationArea != AddressType.COMMON) {
                AreaSizes[CurrentLocationArea] = Math.Max(AreaSizes[CurrentLocationArea], CurrentLocationPointer);
                LocationPointersByArea[CurrentLocationArea] = CurrentLocationPointer;
            }
        }

        public SymbolInfo GetSymbol(string name)
        {
            return Symbols.ContainsKey(name) ? Symbols[name] : null;
        }

        public char? MultiLineCommandDelimiter { get; set; }

        public bool InsideMultiLineComment => MultiLineCommandDelimiter.HasValue;

        public Stack<ConditionalBlockType> conditionalBlocksStack = new();

        public ConditionalBlockType CurrentConditionalBlockType { get; private set; }

        public bool InTrueConditional => CurrentConditionalBlockType is ConditionalBlockType.TrueIf or ConditionalBlockType.TrueElse;

        public bool InFalseConditional =>
            (CurrentConditionalBlockType is ConditionalBlockType.FalseIf or ConditionalBlockType.FalseElse) ||
            (conditionalBlocksStack.Any(b => b is ConditionalBlockType.FalseIf or ConditionalBlockType.FalseElse));

        public bool InMainConditionalBlock => CurrentConditionalBlockType is ConditionalBlockType.TrueIf or ConditionalBlockType.FalseIf;

        public bool InElseBlock => CurrentConditionalBlockType is ConditionalBlockType.TrueElse or ConditionalBlockType.FalseElse;

        public bool InConditionalBlock => CurrentConditionalBlockType is not ConditionalBlockType.None;

        public void PushAndSetConditionalBlock(ConditionalBlockType blockType)
        {
            if(CurrentConditionalBlockType is not ConditionalBlockType.None)
                conditionalBlocksStack.Push(CurrentConditionalBlockType);

            CurrentConditionalBlockType = blockType;
        }

        public void SetConditionalBlock(ConditionalBlockType blockType)
        {
            CurrentConditionalBlockType = blockType;
        }

        public void PopConditionalBlock()
        {
            if(conditionalBlocksStack.Count == 0) {
                if(InConditionalBlock) {
                    CurrentConditionalBlockType = ConditionalBlockType.None;
                }
                else {
                    throw new InvalidOperationException("Attempted to exit a conditional block when none was in progress");
                }
            }
            else {
                CurrentConditionalBlockType = conditionalBlocksStack.Pop();
            }
        }
    }
}
