using Konamiman.Nestor80.Assembler.Errors;
using Konamiman.Nestor80.Assembler.Expressions;
using Konamiman.Nestor80.Assembler.Output;
using Konamiman.Nestor80.Assembler.Relocatable;
using System.Text;

namespace Konamiman.Nestor80.Assembler.Infrastructure
{
    /// <summary>
    /// An instance of this class is used by <see cref="AssemblySourceProcessor"/> to keep track of the assembly process, 
    /// including current area and location pointer, current macro definition/expansion status, INCLUDEd files in use, etc.
    /// </summary>
    internal class AssemblyState
    {
        public AssemblyState(AssemblyConfiguration configuration, Stream sourceStream, Encoding sourceStreamEncoding)
        {
            Configuration = configuration;
            this.sourceStreamEncoding = sourceStreamEncoding;
            SourceStreamReader = new StreamReader(sourceStream, sourceStreamEncoding, true, 4096);
            streamCanSeek = SourceStreamReader.BaseStream.CanSeek;
            RelativeLabelsEnabled = configuration.AllowRelativeLabels;
        }

        private readonly bool streamCanSeek;

        public string CurrentSourceLineText { get; set; }

        private readonly Encoding sourceStreamEncoding;

        private readonly List<AssemblyError> Errors = new();

        public AssemblyConfiguration Configuration { get; init; }

        public StreamReader SourceStreamReader { get; private set; }

        public Encoding DefaultOutputStringEncoding { get; set; }

        public bool InPass2 { get; private set; } = false;

        public bool InPass1 => !InPass2;

        public int CurrentPass => InPass2 ? 2 : 1;

        public bool HasErrors => Errors.Any(e => !e.IsWarning);

        public int CurrentLineNumber { get; private set; } = 1;

        private ushort nextLocalSymbolNumber = 0;

        private readonly Dictionary<string, int> commonAreaSizes = new();
        private string currentCommonBlockName = null;

        public bool InsideNamedMacroInsideFalseConditional { get; set; } = false;

        public int IrpInsideNamedMacroInsideFalseConditionalNestingLevel = 0;

        public List<ProcessedSourceLine> ProcessedLines { get; private set; } = new();

        public Dictionary<string, NamedMacroDefinitionLine> NamedMacros { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

        private bool _RelativeLabelsEnabled;
        public bool RelativeLabelsEnabled
        {
            get => _RelativeLabelsEnabled;
            set
            {
                _RelativeLabelsEnabled = value;
                RegisterLastNonRelativeLabel(null);
            }
        }

        public void RegisterPendingExpression(
            ProcessedSourceLine line,
            Expression expression,
            int location = 0,
            CpuInstrArgType argumentType = CpuInstrArgType.None,
            bool isNegativeIxy = false)
        {
            if (InPass2)
            {
                ExpressionsPendingEvaluation.Add(new ExpressionPendingEvaluation() { Expression = expression, LocationInOutput = location, ArgumentType = argumentType, IsNegativeIxy = isNegativeIxy });
            }
        }

        public void ClearExpressionsPeindingEvaluation()
        {
            ExpressionsPendingEvaluation.Clear();
        }

        public List<ExpressionPendingEvaluation> ExpressionsPendingEvaluation { get; } = new();

        public Address EndAddress { get; private set; }

        public void RegisterEndInstruction(Address address)
        {
            if (address is null)
                throw new ArgumentNullException(nameof(address));

            EndAddress = address;
        }

        public bool EndReached => EndAddress is not null;

        public void SwitchToPass2(BuildType buildType)
        {
            InPass2 = true;
            CurrentLineNumber = 1;
            CurrentSourceLineText = null;
            CurrentPhasedLocationPointer = null;
            EndAddress = null;
            CurrentModule = null;
            currentRootSymbols = null;
            CurrentConditionalBlockType = ConditionalBlockType.None;
            nextLocalSymbolNumber = 0;
            InsideNamedMacroInsideFalseConditional = false;
            IrpInsideNamedMacroInsideFalseConditionalNestingLevel = 0;
            RelativeLabelsEnabled = Configuration.AllowRelativeLabels;
            RegisterLastNonRelativeLabel(null);
            modules.Clear();

            SwitchToArea(buildType != BuildType.Absolute ? AddressType.CSEG : AddressType.ASEG);
            SwitchToLocation(0);

            LocationPointersByArea[AddressType.CSEG] = 0;
            LocationPointersByArea[AddressType.DSEG] = 0;
            LocationPointersByArea[AddressType.ASEG] = 0;

            if (streamCanSeek)
            {
                SourceStreamReader.BaseStream.Seek(0, SeekOrigin.Begin);
                SourceStreamReader = new StreamReader(SourceStreamReader.BaseStream, sourceStreamEncoding, true, 4096);
            }
            else
            {
                var allSourceText = string.Join("\n", MainSourceLines);
                var allSourceBytes = sourceStreamEncoding.GetBytes(allSourceText);
                SourceStreamReader = new StreamReader(new MemoryStream(allSourceBytes), sourceStreamEncoding, true, 4096);
            }
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

        private AddressType locationAreaBeforePhase;

        public AddressType CurrentLocationArea { get; private set; }

        public void EnterPhase(Address address)
        {
            if (IsCurrentlyPhased)
            {
                throw new InvalidOperationException($"{nameof(EnterPhase)} isn't intended to be called while already in .PHASE mode");
            }

            locationAreaBeforePhase = CurrentLocationArea;
            CurrentLocationArea = address.Type;
            CurrentPhasedLocationPointer = address.Value;
        }

        public void ExitPhase()
        {
            if (!IsCurrentlyPhased)
            {
                throw new InvalidOperationException($"{nameof(ExitPhase)} isn't intended to be called while not in .PHASE mode");
            }

            CurrentPhasedLocationPointer = null;
            CurrentLocationArea = locationAreaBeforePhase;
        }

        private ushort currentDephasedLocationPointer;
        public ushort CurrentLocationPointer
        {
            get => CurrentPhasedLocationPointer.GetValueOrDefault(currentDephasedLocationPointer);
            private set
            {
                currentDephasedLocationPointer = value;
            }
        }

        public ushort? CurrentPhasedLocationPointer { get; private set; } = null;

        public bool IsCurrentlyPhased => CurrentPhasedLocationPointer is not null;

        public Address GetCurrentLocation() => new(CurrentLocationArea, CurrentLocationPointer, CurrentLocationArea is AddressType.COMMON ? currentCommonBlockName : null);

        public void SwitchToArea(AddressType area, string commonName = null)
        {
            if (IsCurrentlyPhased && area is not AddressType.ASEG)
            {
                throw new InvalidOperationException($"{nameof(SwitchToArea)} isn't intended to be executed while in .PHASE mode");
            }

            AdjustAreaSize();

            CurrentLocationPointer = area is AddressType.COMMON ? (ushort)0 : LocationPointersByArea[area];
            CurrentLocationArea = area;
            currentCommonBlockName = commonName;
        }

        private void AdjustAreaSize()
        {
            if (CurrentLocationArea is AddressType.COMMON)
            {
                if (commonAreaSizes.ContainsKey(currentCommonBlockName))
                {
                    commonAreaSizes[currentCommonBlockName] = Math.Max(commonAreaSizes[currentCommonBlockName], CurrentLocationPointer);
                }
                else
                {
                    commonAreaSizes.Add(currentCommonBlockName, CurrentLocationPointer);
                }
            }
            else
            {
                AreaSizes[CurrentLocationArea] = Math.Max(AreaSizes[CurrentLocationArea], CurrentLocationPointer);
                LocationPointersByArea[CurrentLocationArea] = CurrentLocationPointer;
            }
        }

        public void SwitchToLocation(ushort location)
        {
            if (IsCurrentlyPhased)
            {
                throw new InvalidOperationException($"{nameof(SwitchToLocation)} isn't intended to be executed while in .PHASE mode");
            }

            if (location == CurrentLocationPointer)
            {
                return;
            }

            AdjustAreaSize();
            CurrentLocationPointer = location;
        }

        public ushort GetAreaSize(AddressType area)
        {
            if (area == AddressType.COMMON)
            {
                throw new InvalidOperationException($"{nameof(GetAreaSize)} isn't intended to be invoked for common blocks");
            }

            return AreaSizes[area];
        }

        public Dictionary<string, int> GetCommonBlockSizes() => commonAreaSizes;

        public void IncreaseLocationPointer(int amount)
        {
            currentDephasedLocationPointer += (ushort)amount;
            if (IsCurrentlyPhased)
            {
                CurrentPhasedLocationPointer += (ushort)amount;
            }
        }

        public void IncreaseLineNumber()
        {
            if (CurrentMacroMode is MacroMode.None ||
                CurrentMacroMode is MacroMode.Definition && CurrentMacroExpansionState is null)
            {
                CurrentLineNumber++;
            }
        }

        public void AddError(AssemblyError error) => Errors.Add(error);

        public AssemblyError AddError(AssemblyErrorCode code, string message, bool withLineNumber = true)
        {
            int? ln = withLineNumber ? GetLineNumberForError() : null;
            var error = new AssemblyError(code, message, ln, withLineNumber ? CurrentSourceLineText : null, CurrentIncludeFilename, GetMacroNamesAndLinesForError());
            AddError(error);
            return error;
        }

        private int GetLineNumberForError()
        {
            foreach (var state in previousExpansionStates)
            {
                if (state.MacroType is MacroType.Named)
                {
                    return state.StartLineNumber;
                }
            }

            if (CurrentMacroExpansionState?.MacroType == MacroType.Named)
            {
                return CurrentMacroExpansionState.StartLineNumber;
            }

            if (CurrentMacroExpansionState is not null)
            {
                return CurrentMacroExpansionState.ActualLineNumber;
            }

            return CurrentLineNumber;
        }

        private (string, int)[] GetMacroNamesAndLinesForError()
        {
            var allStates = previousExpansionStates.Concat(new[] { CurrentMacroExpansionState }).ToArray();

            //TODO: Fix: the number returned isn't accurate for errors thrown inside REPTs inside named macros.
            foreach (var state in allStates)
            {
                if (state is NamedMacroExpansionState nmes)
                {
                    return nmes.RelativeLineNumber < 0 ? null : new (string, int)[] { (nmes.MacroName.ToUpper(), nmes.RelativeLineNumber + 1) };
                }
            }

            return null;
        }

        public AssemblyError[] GetErrors() => Errors.ToArray();

        private readonly Dictionary<string, SymbolInfo> Symbols = new(StringComparer.InvariantCultureIgnoreCase);

        public SymbolInfo[] GetSymbols() => Symbols.Values.ToArray();

        public bool HasSymbol(string symbol) => Symbols.ContainsKey(symbol);

        public bool SymbolIsOfKnownType(string symbol) => Symbols.ContainsKey(symbol) && Symbols[symbol].IsOfKnownType;

        public string LastNonRelativeLabel { get; private set; } = null;

        public HashSet<string> CurrentRelativeLabels { get; } = new(StringComparer.OrdinalIgnoreCase);

        public void AddSymbol(string name, SymbolType type, Address value = null, bool isPublic = false)
        {
            bool isNonRelativeLabel = false;
            if (type is SymbolType.Label && name[0] is not '.' && !CurrentRelativeLabels.Contains(name))
            {
                RegisterLastNonRelativeLabel(name);
                isNonRelativeLabel = true;
            }

            Symbols.Add(name, new SymbolInfo() { Name = name, Type = type, Value = value, IsPublic = isPublic, IsNonRelativeLabel = isNonRelativeLabel });
        }

        public void RegisterLastNonRelativeLabel(string label)
        {
            LastNonRelativeLabel = label;
            CurrentRelativeLabels.Clear();
        }

        public bool IsValidNonRelativeLabelName(string name) =>
            RelativeLabelsEnabled && name[0] is not '.' && !CurrentRelativeLabels.Contains(name);

        public void WrapUp()
        {
            AdjustAreaSize();
        }

        /// <summary>
        /// Get the <see cref="SymbolInfo"/> instance for a given symbol name,
        /// if the symbol exists. If necessary, the symbol name modified to prepend it
        /// with the current module or non-relative label.
        /// </summary>
        /// <param name="name">Symbol name, it may get modified.</param>
        /// <returns>Symbol object, or null if that symbol hasn't appeared in the source yet.</returns>
        public SymbolInfo GetSymbol(ref string name)
        {
            if (CurrentMacroExpansionState is NamedMacroExpansionState nmes)
            {
                var replaced = nmes.MaybeConvertLocalSymbolName(ref name, ref nextLocalSymbolNumber);
                if (!replaced)
                {
                    foreach (var state in previousExpansionStates)
                    {
                        if (state is NamedMacroExpansionState nmes2)
                        {
                            replaced = nmes2.MaybeConvertLocalSymbolName(ref name, ref nextLocalSymbolNumber);
                            if (replaced)
                            {
                                break;
                            }
                        }
                    }
                }
            }

            return GetSymbolWithoutLocalNameReplacement(name);
        }

        public SymbolInfo GetSymbolWithoutLocalNameReplacement(string name)
        {
            return Symbols.ContainsKey(name) ? Symbols[name] : null;
        }

        public char? MultiLineCommandDelimiter { get; set; }

        public bool InsideMultiLineComment => MultiLineCommandDelimiter.HasValue;

        public Stack<ConditionalBlockType> conditionalBlocksStack = new();

        public ConditionalBlockType CurrentConditionalBlockType { get; private set; }

        public bool InTrueConditional => CurrentConditionalBlockType is ConditionalBlockType.TrueIf or ConditionalBlockType.TrueElse;

        public bool InFalseConditional =>
            CurrentConditionalBlockType is ConditionalBlockType.FalseIf or ConditionalBlockType.FalseElse ||
            conditionalBlocksStack.Any(b => b is ConditionalBlockType.FalseIf or ConditionalBlockType.FalseElse);

        public bool InMainConditionalBlock => CurrentConditionalBlockType is ConditionalBlockType.TrueIf or ConditionalBlockType.FalseIf;

        public bool InElseBlock => CurrentConditionalBlockType is ConditionalBlockType.TrueElse or ConditionalBlockType.FalseElse;

        public bool InConditionalBlock => CurrentConditionalBlockType is not ConditionalBlockType.None;

        public void PushAndSetConditionalBlock(ConditionalBlockType blockType)
        {
            if (CurrentConditionalBlockType is not ConditionalBlockType.None)
                conditionalBlocksStack.Push(CurrentConditionalBlockType);

            CurrentConditionalBlockType = blockType;
        }

        public void SetConditionalBlock(ConditionalBlockType blockType)
        {
            CurrentConditionalBlockType = blockType;
        }

        public void PopConditionalBlock()
        {
            if (conditionalBlocksStack.Count == 0)
            {
                if (InConditionalBlock)
                {
                    CurrentConditionalBlockType = ConditionalBlockType.None;
                }
                else
                {
                    throw new InvalidOperationException("Attempted to exit a conditional block when none was in progress");
                }
            }
            else
            {
                CurrentConditionalBlockType = conditionalBlocksStack.Pop();
            }
        }

        private readonly Stack<IncludeState> includeStates = new();

        public string CurrentIncludeFilename { get; private set; } = null;

        public void PushIncludeState(Stream newStream, IncludeLine includeLine)
        {
            var previousState = new IncludeState()
            {
                PreviousFileName = CurrentIncludeFilename,
                ProcessedLine = includeLine,
                PreviousLineNumber = CurrentLineNumber,
                PreviousProcessedLines = ProcessedLines,
                PreviousSourceStreamReader = SourceStreamReader
            };

            includeStates.Push(previousState);
            InsideIncludedFile = true;

            CurrentIncludeFilename = includeLine.FileName;
            SourceStreamReader = newStream is null ? null : new StreamReader(newStream, sourceStreamEncoding, true, 4096);
            CurrentLineNumber = 0; //0 because the line number will be increased right after this method

            //Don't just clear the existing list, we really need a new one!
            ProcessedLines = new List<ProcessedSourceLine>();
        }

        public void PopIncludeState()
        {
            if (!InsideIncludedFile)
            {
                throw new InvalidOperationException("Can't exit included file because we aren't in one");
            }

            SourceStreamReader?.Dispose();

            var previousState = includeStates.Pop();

            if (SourceStreamReader is not null)
            {
                previousState.ProcessedLine.Lines = ProcessedLines.ToArray();
            }

            CurrentLineNumber = previousState.PreviousLineNumber + 1;
            SourceStreamReader = previousState.PreviousSourceStreamReader;
            ProcessedLines = previousState.PreviousProcessedLines;
            CurrentIncludeFilename = previousState.PreviousFileName;

            InsideIncludedFile = includeStates.Count > 0;
        }

        public bool InsideIncludedFile { get; private set; }

        public int CurrentIncludesDeepLevel => includeStates.Count;

        private readonly Stack<(string, HashSet<string>)> modules = new();

        public string CurrentModule { get; private set; } = null;

        private HashSet<string> currentRootSymbols = null;

        public void EnterModule(string name)
        {
            RegisterLastNonRelativeLabel(null);
            modules.Push((CurrentModule, currentRootSymbols));
            CurrentModule = CurrentModule is null ? name : $"{CurrentModule}.{name}";
            currentRootSymbols = new HashSet<string>(
                currentRootSymbols is null ? Array.Empty<string>() : currentRootSymbols,
                StringComparer.OrdinalIgnoreCase);
        }

        public void ExitModule()
        {
            if (CurrentModule is null)
            {
                throw new InvalidOperationException($"{nameof(ExitModule)} called while not in a module");
            }

            (CurrentModule, currentRootSymbols) = modules.Pop();
            RegisterLastNonRelativeLabel(null);
        }

        public void RegisterRootSymbols(IEnumerable<string> symbols)
        {
            if (currentRootSymbols is null)
            {
                throw new InvalidOperationException($"{nameof(RegisterRootSymbols)} called while not in a module");
            }

            foreach (var symbol in symbols)
            {
                currentRootSymbols.Add(symbol);
            }
        }

        /// <summary>
        /// Given a symbol name, prefixes it with the current module or non-relative label name
        /// if needed, unless the symbol is declared as root for the current module or is prefixed with ":".
        /// </summary>
        /// <param name="symbol">Symbol to check.</param>
        /// <returns>Symbol (maybe) prefixed with the current module or non-relative label name.</returns>
        public string Modularize(string symbol)
        {
            var inModule = CurrentModule is not null;

            if (inModule && currentRootSymbols.Contains(symbol))
            {
                return symbol;
            }

            var isDot = symbol[0] is '.';
            var isRelativeLabel = false;
            if (RelativeLabelsEnabled && isDot && LastNonRelativeLabel is not null)
            {
                symbol = LastNonRelativeLabel + symbol;
                isRelativeLabel = true;
            }
            else if (inModule)
            {
                symbol = $"{CurrentModule}{(isDot ? "" : ".")}{symbol}";
            }

            if (isRelativeLabel)
            {
                CurrentRelativeLabels.Add(symbol);
            }

            return symbol;
        }

        public MacroExpansionState CurrentMacroExpansionState { get; private set; } = null;

        private readonly Stack<MacroExpansionState> previousExpansionStates = new();

        public bool NamedMacroExists(string name) => NamedMacros.ContainsKey(name);

        public void RegisterNamedMacroDefinitionStart(NamedMacroDefinitionLine processedLine)
        {
            if (MacroDefinitionState.DefiningNamedMacro)
            {
                throw new InvalidOperationException($"{nameof(RegisterNamedMacroDefinitionStart)} is not supposed to be called while already in macro definition mode");
            }

            NamedMacros[processedLine.Name] = processedLine;
            MacroDefinitionState.StartDefinition(MacroType.Named, processedLine, CurrentLineNumber);
        }

        public void RegisterMacroExpansionStart(MacroExpansionLine expansionLine)
        {
            if (expansionLine.MacroType is MacroType.Named)
            {
                if (!NamedMacros.ContainsKey(expansionLine.Name))
                {
                    throw new InvalidOperationException($"{nameof(RegisterMacroExpansionStart)}: unknown named macro '{expansionLine.Name}'");
                }

                if (CurrentMacroExpansionState is not null)
                {
                    previousExpansionStates.Push(CurrentMacroExpansionState);
                }

                var macroDefinition = NamedMacros[expansionLine.Name];
                var ln = CurrentMacroExpansionState is null ||
                    CurrentMacroExpansionState.MacroType is MacroType.Named ||
                    previousExpansionStates.Any(s => s.MacroType is MacroType.Named) ? CurrentLineNumber : CurrentMacroExpansionState.ActualLineNumber;
                CurrentMacroExpansionState = new NamedMacroExpansionState(expansionLine.Name, expansionLine, macroDefinition.LineTemplates, macroDefinition.Arguments.Length, expansionLine.Parameters, ln);
            }
            else if (MacroDefinitionState.DefiningMacro)
            {
                throw new InvalidOperationException($"{nameof(RegisterMacroExpansionStart)} is not supposed to be called while already in macro definition mode");
            }
            else
            {
                var ln = CurrentMacroMode is MacroMode.Expansion ? CurrentMacroExpansionState.ActualLineNumber : CurrentLineNumber;
                MacroDefinitionState.StartDefinition(expansionLine.MacroType, expansionLine, ln + 1);
            }
        }

        public static void RegisterMacroDefinitionLine(string sourceLine, bool isMacroDefinitionOrExpansionInstruction)
        {
            if (isMacroDefinitionOrExpansionInstruction)
            {
                MacroDefinitionState.IncreaseDepth();
            }
            MacroDefinitionState.AddLine(sourceLine);
        }

        public bool RegisterMacroEnd()
        {
            if (CurrentMacroMode is MacroMode.Definition)
            {
                if (MacroDefinitionState.Depth > 1)
                {
                    MacroDefinitionState.DecreaseDepth();
                }
                else if (MacroDefinitionState.ProcessedLine is NamedMacroDefinitionLine nmdl)
                {
                    var lines = MacroDefinitionState.GetLines();
                    ReplaceMacroLineArgsWithPlaceholders(lines, nmdl.Arguments);
                    nmdl.LineTemplates = lines;
                    MacroDefinitionState.EndDefinition();
                }
                else
                {
                    var macroExpansionLine = (MacroExpansionLine)MacroDefinitionState.ProcessedLine;
                    MacroExpansionState expansionState;
                    var ln = MacroDefinitionState.StartLineNumber;
                    if (macroExpansionLine.MacroType is MacroType.ReptWithCount)
                    {
                        expansionState = new ReptWithCountExpansionState(macroExpansionLine, MacroDefinitionState.GetLines(), macroExpansionLine.RepetitionsCount, ln);
                    }
                    else
                    {
                        var lines = MacroDefinitionState.GetLines();
                        ReplaceMacroLineArgsWithPlaceholders(lines, new[] { macroExpansionLine.Placeholder });
                        expansionState = new ReptWithParamsExpansionState(macroExpansionLine, lines, macroExpansionLine.Parameters, ln);
                    }

                    if (CurrentMacroExpansionState is null)
                    {
                        IncreaseLineNumber(); //CurrentLineNumber++;
                    }
                    else
                    {
                        previousExpansionStates.Push(CurrentMacroExpansionState);
                    }

                    CurrentMacroExpansionState = expansionState;
                    MacroDefinitionState.EndDefinition();
                }
            }
            else if (CurrentMacroMode is MacroMode.Expansion)
            {
                //TODO: this is never reached?
                CurrentMacroExpansionState.ExpansionProcessedLine.Lines = CurrentMacroExpansionState.ProcessedLines.ToArray();
                if (previousExpansionStates.Count == 0)
                {
                    CurrentMacroExpansionState = null;
                }
                else
                {
                    CurrentMacroExpansionState = previousExpansionStates.Pop();
                }
            }
            else
            {
                return false;
            }

            return true;
        }

        private static void ReplaceMacroLineArgsWithPlaceholders(string[] macroLines, string[] args)
        {
            if (args.Length == 0 || macroLines.Length == 0)
            {
                return;
            }

            for (int macroLineIndex = 0; macroLineIndex < macroLines.Length; macroLineIndex++)
            {
                var macroLine = macroLines[macroLineIndex];
                macroLine = macroLine.Replace("{", "{{").Replace("}", "}}");
                for (int argIndex = 0; argIndex < args.Length; argIndex++)
                {
                    macroLine = SourceLineWalker.ReplaceMacroLineArgWithPlaceholder(macroLine, args[argIndex], argIndex);
                }
                macroLines[macroLineIndex] = macroLine;
            }
        }

        public string GetNextMacroExpansionLine()
        {
            if (CurrentMacroExpansionState is null)
            {
                return null;
            }

            string line;

            if (!CurrentMacroExpansionState.HasMore)
            {
                //If we just finished expanding a named macro but NOT from inside a REPT
                if (CurrentMacroExpansionState.MacroType is MacroType.Named && previousExpansionStates.Count == 0)
                {
                    CurrentLineNumber++;
                }

                CurrentMacroExpansionState.ExpansionProcessedLine.Lines = CurrentMacroExpansionState.ProcessedLines.ToArray();
                if (previousExpansionStates.Count == 0)
                {
                    CurrentMacroExpansionState = null;
                    line = null;
                }
                else
                {
                    CurrentMacroExpansionState = previousExpansionStates.Pop();
                    line = GetNextMacroExpansionLine();
                }

                return line;
            }

            line = CurrentMacroExpansionState.GetNextSourceLine();
            return line;
        }

        internal void RegisterProcessedLine(ProcessedSourceLine processedLine)
        {
            bool isMacroExpansion = false;

            if (processedLine is EndMacroLine || processedLine is MacroExpansionLine mel && mel.MacroType is MacroType.Named)
            {
                if (previousExpansionStates.Count > 0)
                {
                    previousExpansionStates.Peek().ProcessedLines.Add(processedLine);
                    isMacroExpansion = true;
                }
            }
            else
            {
                if (CurrentMacroExpansionState is not null)
                {
                    CurrentMacroExpansionState.ProcessedLines.Add(processedLine);
                    isMacroExpansion = true;
                }
            }

            if (isMacroExpansion)
            {
                return;
            }

            if (InPass2)
            {
                ProcessedLines.Add(processedLine);
            }
            else if (!streamCanSeek && !InsideIncludedFile)
            {
                MainSourceLines.Add(processedLine.Line);
            }
        }

        public MacroMode CurrentMacroMode =>
            MacroDefinitionState.DefiningMacro ? MacroMode.Definition :
            CurrentMacroExpansionState is not null ? MacroMode.Expansion :
            MacroMode.None;

        public bool ExpandingNamedMacro => CurrentMacroExpansionState is NamedMacroExpansionState;

        private readonly Dictionary<(string, bool), Expression> expressionsBySource = new();

        public readonly List<string> MainSourceLines = new();

        /// <summary>
        /// Return a <see cref="Expression"/> object from a expression text.
        /// A cache of expression objects is used so that expressions defined in pass 1
        /// can be recycled in pass 2 (note that what gets cached is the expression object
        /// itself, not the expression evaluation result).
        /// </summary>
        /// <param name="sourceLine">Source line.</param>
        /// <param name="forDefb">True if the expression is part of a DEFB line (strings are treated differently).</param>
        /// <param name="isByte">True if the expression is to be evaluated as a single byte.</param>
        /// <returns>The cached or newly generated expression.</returns>
        public Expression GetExpressionFor(string sourceLine, bool forDefb = false, bool isByte = false)
        {
            Expression expression;
            if (sourceLine.Contains('$'))
            {
                // When "$" is used as a label it refers to the current location counter,
                // thus we can't cache the expression. "$" could also be part of a
                // regular label or inside a string, but for simplicity we just check
                // if the character is present in the source line.
                expression = Expression.Parse(sourceLine, forDefb, isByte);
                expression.ValidateAndPostifixize();
                return expression;
            }

            if (expressionsBySource.ContainsKey((sourceLine, forDefb)))
            {
                return expressionsBySource[(sourceLine, forDefb)];
            }

            expression = Expression.Parse(sourceLine, forDefb, isByte);
            expression.ValidateAndPostifixize();
            expressionsBySource.Add((sourceLine, forDefb), expression);
            return expression;
        }

        public void RemoveNamedMacroDefinition(string name)
        {
            NamedMacros.Remove(name);
        }

        public void ExitMacro(bool forceEnd)
        {
            if (CurrentMacroExpansionState is null)
            {
                throw new InvalidOperationException($"{nameof(ExitMacro)} invoked while not in macro expansion mode");
            }

            CurrentMacroExpansionState.Exit(forceEnd);
        }
    }
}
