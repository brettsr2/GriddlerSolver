using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Globalization;
using System.Threading;

namespace Griddler_Solver
{
  class Solver
  {
    #region data
    public String Name
    { get; set; } = String.Empty;
    public String Url
    { get; set; } = String.Empty;

    public Board Board
    { get; set; } = new();
    #endregion // data

    #region drawing
    public const String TimeFormat = @"mm\:ss";

    public Int32 MaxHintsCountRow
    {
      get
      {
        return GetMaxItemCount(Board.HintsRow);
      }
    }
    public Int32 MaxHintsCountColumn
    {
      get
      {
        return GetMaxItemCount(Board.HintsColumn);
      }
    }

    public Double CellSize
    { get; set; } = 15;

    public List<PuzzleColors> ListColors
    { get; set; } = [];
    #endregion // drawing

    #region solving
    [JsonIgnore]
    public Config Config
    { get; set; } = new();

    private List<StaticAnalysis> _ListStaticAnalysis = [];

    [JsonIgnore]
    public IReadOnlyList<StaticAnalysis> ListStaticAnalysis => _ListStaticAnalysis;

    private class BacktrackState
    {
      public CellValue[][] BoardSnapshot { get; set; } = Array.Empty<CellValue[]>();
      public Boolean[][] HintRowSolved { get; set; } = Array.Empty<Boolean[]>();
      public Boolean[][] HintColumnSolved { get; set; } = Array.Empty<Boolean[]>();
      public Int32 GuessRow { get; set; }
      public Int32 GuessCol { get; set; }
      public CellValue NextGuess { get; set; }  // CellValue.Unknown = exhausted (both tried)
      public UInt64 PermutationsLimit { get; set; }
    }
    private Stack<BacktrackState> _backtrackStack = new();

    private readonly record struct LineKey(Boolean IsRow, Int32 Index);
    #endregion // solving

    public Solver()
    {
    }
    public Solver(Hint[][] hintsRow, Hint[][] hintsColumn)
    {
      Board = new Board()
      {
        HintsRow = hintsRow,
        HintsColumn = hintsColumn
      };
      Board.Init();
    }

    public void Clear()
    {
      Board.Clear();
      _ListStaticAnalysis = [];
    }

    private Int32 GetMaxItemCount(Hint[][]? hints)
    {
      Int32 max = 0;

      foreach (var hint in hints!)
      {
        max = Math.Max(max, hint.Length);
      }

      return max;
    }

    private void PrintIterationStatistic(Int32 iteration, UInt64 currentPermutationsCount, UInt64 permutationsLimit, TimeSpan globalElapsed, TimeSpan iterationElapsed)
    {
      Int32 unknownCount = 0, blankCount = 0, solvedCount = 0;

      for (Int32 row = 0; row < Board.RowCount; row++)
      {
        for (Int32 col = 0; col < Board.ColumnCount; col++)
        {
          if (Board[row, col] == CellValue.Unknown)
          {
            unknownCount++;
          }
          else if (Board[row, col] == CellValue.Background)
          {
            blankCount++;
          }
          else
          {
            solvedCount++;
          }
        }
      }

      Int32 total = Board.RowCount * Board.ColumnCount;
      Int32 percentSolved = solvedCount * 100 / total;

      StringBuilder stringBuilder = new StringBuilder();
      stringBuilder.Append($"[{globalElapsed.ToString(TimeFormat)}]");
      stringBuilder.Append($"[{iteration}]");
      stringBuilder.Append($"[{iterationElapsed.ToString(TimeFormat)}]");
      stringBuilder.Append($" Solved: {solvedCount}({percentSolved}%), ");
      stringBuilder.Append($"Total permutations: {FormatNumber(currentPermutationsCount)}, ");
      stringBuilder.Append($"Limit per line: {FormatNumber(permutationsLimit)}");

      Config.Progress?.AddMessage(stringBuilder.ToString());
    }
    public void Solve(Config config)
    {
      Config = config;
      _backtrackStack.Clear();
      Config.Progress?.AddMessage($"Start");
      Config.Progress?.AddMessage($"Cells to solve: {Board.RowCount * Board.ColumnCount}");

      if (Board.RowCount == 0 || Board.ColumnCount == 0)
      {
        Config.Progress?.AddMessage($"Empty board. Nothing to solve.");
        return;
      }

      List<SolverLine> listSolverLine = [];
      for (Int32 row = 0; row < Board.RowCount; row++)
      {
        listSolverLine.Add(new SolverLine()
        {
          Config = Config,
          Index = row,
          IsRow = true,
          Score = CalculateScore(Board.HintsRow[row]),
          Board = Board,
        });
      }
      for (Int32 column = 0; column < Board.ColumnCount; column++)
      {
        listSolverLine.Add(new SolverLine()
        {
          Config = Config,
          Index = column,
          IsRow = false,
          Score = CalculateScore(Board.HintsColumn[column]),
          Board = Board,
        });
      }
      if (Config.ScoreSortingEnabled)
      {
        listSolverLine.Sort(delegate (SolverLine line1, SolverLine line2)
        {
          return -line1.Score.CompareTo(line2.Score);
        });
      }

      // Build lookup arrays for O(1) row/column → SolverLine mapping
      SolverLine[] rowLines = new SolverLine[Board.RowCount];
      SolverLine[] columnLines = new SolverLine[Board.ColumnCount];
      foreach (SolverLine sl in listSolverLine)
      {
        if (sl.IsRow) rowLines[sl.Index] = sl;
        else columnLines[sl.Index] = sl;
      }

      Stopwatch stopWatchGlobal = Stopwatch.StartNew();
      Int32 iteration = 0;

      UInt64 permutationsLimit = Config.PermutationsLimit ? 1000000 : UInt32.MaxValue;

      StaticAnalysis();
      OverlapAnalysis();

      while (!Board.IsSolved)
      {
        if (Config.Break)
        {
          break;
        }

        Stopwatch stopWatchIteration = Stopwatch.StartNew();

        iteration++;

        Config.TicksCurrentIterationTimer = Config.TicksCurrentIterationStart = DateTime.Now.Ticks;
        Config.IterationPrefixLength = 9 + $"{iteration}".Length; // [mm:ss] = 7, [ + ] = 2

        Boolean changed = false;
        Boolean contradiction = false;
        UInt64 currentPermutationsCount = 0;
        UInt64 permutationsMinLimit = UInt64.MaxValue;

        if (Config.PermutationAnalysisEnabled)
        {
          (changed, currentPermutationsCount, permutationsMinLimit, permutationsLimit, contradiction) =
              ProcessWorkQueue(listSolverLine, rowLines, columnLines,
                               permutationsLimit, iteration);
        }

        // Immediate contradiction handling — backtrack without waiting for all workers
        if (contradiction)
        {
          if (_backtrackStack.Count > 0)
          {
            Boolean foundAlternative = false;
            while (_backtrackStack.Count > 0)
            {
              var state = _backtrackStack.Pop();
              RestoreState(state, listSolverLine);

              if (state.NextGuess != CellValue.Unknown) // Has an alternative to try
              {
                // Push exhausted marker so future contradictions are still detected
                _backtrackStack.Push(new BacktrackState
                {
                  BoardSnapshot = state.BoardSnapshot,
                  HintRowSolved = state.HintRowSolved,
                  HintColumnSolved = state.HintColumnSolved,
                  GuessRow = state.GuessRow,
                  GuessCol = state.GuessCol,
                  NextGuess = CellValue.Unknown, // Both alternatives tried
                  PermutationsLimit = state.PermutationsLimit
                });
                Board[state.GuessRow, state.GuessCol] = state.NextGuess;
                permutationsLimit = state.PermutationsLimit;
                Config.Progress?.AddMessage($"Backtrack: contradiction, trying {state.NextGuess} at ({state.GuessRow + 1},{state.GuessCol + 1}), depth {_backtrackStack.Count}");
                foundAlternative = true;
                break;
              }
              else
              {
                Config.Progress?.AddMessage($"Backtrack: both options exhausted at ({state.GuessRow + 1},{state.GuessCol + 1}), unwinding, depth {_backtrackStack.Count}");
              }
            }

            if (!foundAlternative)
            {
              Config.Progress?.AddMessage("Backtracking exhausted all options. No solution found.");
              break;
            }

            StaticAnalysis();
            OverlapAnalysis();
            continue;
          }
          else
          {
            Config.Progress?.AddMessage("Contradiction detected. Puzzle has no solution.");
            break;
          }
        }

        StaticAnalysis();
        OverlapAnalysis();

        Config.Progress?.AddDebugMessage($"Iteration {iteration}");
        foreach (SolverLine solverLine in listSolverLine)
        {
          Config.Progress?.AddDebugMessage(solverLine.ToString());
        }

        if (changed == false)
        {
          if (permutationsMinLimit < UInt64.MaxValue)
          {
            permutationsLimit = permutationsMinLimit + 1;
          }
          else
          {
            Boolean anyDirty = false;
            foreach (SolverLine solverLine in listSolverLine)
            {
              if (!solverLine.IsSolved)
              {
                Boolean dirty = solverLine.IsRow ? Board.IsRowDirty(solverLine.Index) : Board.IsColumnDirty(solverLine.Index);
                if (dirty)
                {
                  anyDirty = true;
                  break;
                }
              }
            }
            if (!anyDirty)
            {
              // Normal solver is stuck — try backtracking as last resort
              if (Config.BacktrackingEnabled)
              {
                var (guessRow, guessCol) = FindBestGuessCell();
                if (guessRow >= 0)
                {
                  _backtrackStack.Push(new BacktrackState
                  {
                    BoardSnapshot = Board.CurrentState,
                    HintRowSolved = SaveHintSolved(Board.HintsRow),
                    HintColumnSolved = SaveHintSolved(Board.HintsColumn),
                    GuessRow = guessRow,
                    GuessCol = guessCol,
                    NextGuess = CellValue.Background,
                    PermutationsLimit = permutationsLimit
                  });
                  Board[guessRow, guessCol] = CellValue.Color;
                  Config.Progress?.AddMessage($"Backtrack: guessing Color at ({guessRow + 1},{guessCol + 1}), depth {_backtrackStack.Count}");
                  ResetSolverLines(listSolverLine);
                  _ListStaticAnalysis.Clear();
                  // Keep current permutationsLimit — don't reset, we need full analysis for contradiction detection
                  StaticAnalysis();
                  OverlapAnalysis();
                  continue;
                }
              }
              Config.Progress?.AddMessage("No progress and no dirty lines. Solver stuck.");
              break;
            }
          }
        }

        stopWatchIteration.Stop();
        PrintIterationStatistic(iteration, currentPermutationsCount, permutationsLimit, stopWatchGlobal.Elapsed, stopWatchIteration.Elapsed);

        if (Config.StepMode)
        {
          break;
        }
      }

      foreach (SolverLine solverLine in listSolverLine)
      {
        Config.Progress?.AddDebugMessage(solverLine.ToString());
      }

      Config.Break = true;

      Board.Iterations = iteration;
      Board.TimeTaken = stopWatchGlobal.Elapsed;
    }
    private (Boolean AnyChanged, UInt64 TotalPermutations, UInt64 PermutationsMinLimit, UInt64 FinalLimit, Boolean Contradiction)
        ProcessWorkQueue(
            List<SolverLine> listSolverLine,
            SolverLine[] rowLines,
            SolverLine[] columnLines,
            UInt64 permutationsLimit,
            Int32 iteration)
    {
      Config.ContradictionDetected = false;

      var queue = new ConcurrentQueue<SolverLine>();
      var inSystem = new ConcurrentDictionary<LineKey, byte>();
      Int32 pendingItems = 0;
      var drainComplete = new ManualResetEventSlim(false);
      Boolean anyChanged = false;
      UInt64 totalPermutations = 0;
      UInt64 permutationsMinLimit = UInt64.MaxValue;
      UInt64 currentLimit = permutationsLimit;
      Object syncLock = new Object();

      // Local helper: try to enqueue a line if dirty, unsolved, within limit, and not already in system
      void TryEnqueue(SolverLine line)
      {
        if (Config.ContradictionDetected)
        {
          return;
        }

        if (line.IsSolved)
        {
          return;
        }

        Boolean dirty = line.IsRow ? Board.IsRowDirty(line.Index) : Board.IsColumnDirty(line.Index);
        if (!dirty)
        {
          return;
        }

        if (line.MaxPermutationsCount > currentLimit)
        {
          lock (syncLock)
          {
            permutationsMinLimit = Math.Min(permutationsMinLimit, line.MaxPermutationsCount);
          }
          return;
        }

        var key = new LineKey(line.IsRow, line.Index);
        if (inSystem.TryAdd(key, 0))
        {
          Interlocked.Increment(ref pendingItems);
          queue.Enqueue(line);
        }
      }

      // Seed the queue with all eligible lines
      foreach (SolverLine solverLine in listSolverLine)
      {
        TryEnqueue(solverLine);
      }

      if (Volatile.Read(ref pendingItems) == 0)
      {
        return (false, 0, permutationsMinLimit, currentLimit, false);
      }

      // Worker procedure
      void WorkerProc()
      {
        while (!Config.Break)
        {
          if (!queue.TryDequeue(out SolverLine? line))
          {
            if (Volatile.Read(ref pendingItems) <= 0)
            {
              break;
            }
            Thread.Sleep(1);
            continue;
          }

          var lineKey = new LineKey(line.IsRow, line.Index);

          // Contradiction already detected by another worker — skip processing
          if (Config.ContradictionDetected)
          {
            inSystem.TryRemove(lineKey, out _);
            if (Interlocked.Decrement(ref pendingItems) <= 0)
            {
              drainComplete.Set();
            }
            continue;
          }

          // Clear dirty BEFORE reading snapshot (same as original code)
          if (line.IsRow)
          {
            Board.ClearRowDirty(line.Index);
          }
          else
          {
            Board.ClearColumnDirty(line.Index);
          }

          // Solve: GetRow/GetColumn locked, GeneratePermutations local, MergeRow/MergeColumn locked
          Interlocked.Increment(ref Config.ActiveWorkers);
          line.Solve();
          Interlocked.Decrement(ref Config.ActiveWorkers);

          // Immediate contradiction detection — signal all workers to stop
          if (line.HasContradiction)
          {
            Config.ContradictionDetected = true;

            // Drain remaining queue items
            while (queue.TryDequeue(out var discarded))
            {
              var dKey = new LineKey(discarded.IsRow, discarded.Index);
              inSystem.TryRemove(dKey, out _);
              if (Interlocked.Decrement(ref pendingItems) <= 0)
              {
                drainComplete.Set();
              }
            }
          }

          // Accumulate results
          lock (syncLock)
          {
            totalPermutations += line.CurrentPermutationsCount;
            anyChanged |= line.Changed;
          }

          // Remove from tracking (allows re-enqueue if dirty again)
          inSystem.TryRemove(lineKey, out _);

          // Re-check and enqueue cross-lines only if no contradiction
          if (!Config.ContradictionDetected)
          {
            // Re-check: self became dirty during processing? (cross-line merge dirtied us)
            TryEnqueue(line);

            // Enqueue affected cross-lines
            if (line.Changed)
            {
              if (line.IsRow)
              {
                for (Int32 c = 0; c < Board.ColumnCount; c++)
                {
                  if (Board.IsColumnDirty(c))
                  {
                    TryEnqueue(columnLines[c]);
                  }
                }
              }
              else
              {
                for (Int32 r = 0; r < Board.RowCount; r++)
                {
                  if (Board.IsRowDirty(r))
                  {
                    TryEnqueue(rowLines[r]);
                  }
                }
              }
            }
          }

          // Signal completion of this item
          if (Interlocked.Decrement(ref pendingItems) <= 0)
          {
            drainComplete.Set();
          }
        }
      }

      // Dispatch workers
      Int32 maxWorkers = Config.MultithreadEnabled ? Environment.ProcessorCount : 1;
      var workersExited = new CountdownEvent(maxWorkers);

      for (Int32 i = 0; i < maxWorkers; i++)
      {
        ThreadPool.QueueUserWorkItem(_ =>
        {
          try
          {
            WorkerProc();
          }
          finally
          {
            workersExited.Signal();
          }
        });
      }

      // Wait for queue to drain and all workers to exit
      drainComplete.Wait();
      workersExited.Wait();

      return (anyChanged, totalPermutations, permutationsMinLimit, currentLimit, Config.ContradictionDetected);
    }

    public static String FormatNumber(UInt64 value)
    {
      if (value >= 1_000_000_000_000_000)
      {
        return (value / 1_000_000_000_000_000.0).ToString("F1", CultureInfo.InvariantCulture) + "P";
      }
      if (value >= 1_000_000_000_000)
      {
        return (value / 1_000_000_000_000.0).ToString("F1", CultureInfo.InvariantCulture) + "T";
      }
      if (value >= 1_000_000_000)
      {
        return (value / 1_000_000_000.0).ToString("F1", CultureInfo.InvariantCulture) + "G";
      }
      if (value >= 1_000_000)
      {
        return (value / 1_000_000.0).ToString("F1", CultureInfo.InvariantCulture) + "M";
      }
      if (value >= 1_000)
      {
        return (value / 1_000.0).ToString("F1", CultureInfo.InvariantCulture) + "k";
      }
      return value.ToString();
    }

    private static Int32 CalculateScore(Hint[] hints)
    {
      return hints.Length + hints.Sum(hint => hint.Count * 2);
    }

    private static Int32 FindFirstOrNext(CellValue[] line, Int32 indexStart)
    {
      for (Int32 indexLine = indexStart; indexLine < line.Length; indexLine++)
      {
        if (line[indexLine] == CellValue.Background)
        {
          continue;
        }
        else
        {
          return indexLine;
        }
      }

      return -1;
    }

    private static CellValue GetCellValue(CellValue[] line, Int32 index)
    {
      return index >= line.Length ? CellValue.OutOfBorder : line[index];
    }

    private (Int32 row, Int32 col) FindBestGuessCell()
    {
      // Pre-compute unknown counts per row and per column
      Int32[] unknownsPerRow = new Int32[Board.RowCount];
      Int32[] unknownsPerCol = new Int32[Board.ColumnCount];

      for (Int32 row = 0; row < Board.RowCount; row++)
      {
        for (Int32 col = 0; col < Board.ColumnCount; col++)
        {
          if (Board[row, col] == CellValue.Unknown)
          {
            unknownsPerRow[row]++;
            unknownsPerCol[col]++;
          }
        }
      }

      // Find the unknown cell with the lowest combined score (most constrained)
      Int32 bestRow = -1, bestCol = -1;
      Int32 bestScore = Int32.MaxValue;

      for (Int32 row = 0; row < Board.RowCount; row++)
      {
        if (unknownsPerRow[row] == 0)
        {
          continue;
        }

        for (Int32 col = 0; col < Board.ColumnCount; col++)
        {
          if (Board[row, col] == CellValue.Unknown)
          {
            Int32 score = unknownsPerRow[row] + unknownsPerCol[col];
            if (score < bestScore)
            {
              bestScore = score;
              bestRow = row;
              bestCol = col;
            }
          }
        }
      }

      return (bestRow, bestCol);
    }

    private Boolean[][] SaveHintSolved(Hint[][] hints)
    {
      var result = new Boolean[hints.Length][];
      for (Int32 i = 0; i < hints.Length; i++)
      {
        result[i] = new Boolean[hints[i].Length];
        for (Int32 j = 0; j < hints[i].Length; j++)
        {
          result[i][j] = hints[i][j].IsSolved;
        }
      }
      return result;
    }

    private void RestoreHintSolved(Hint[][] hints, Boolean[][] saved)
    {
      for (Int32 i = 0; i < hints.Length; i++)
      {
        for (Int32 j = 0; j < hints[i].Length; j++)
        {
          hints[i][j].IsSolved = saved[i][j];
        }
      }
    }

    private void ResetSolverLines(List<SolverLine> lines)
    {
      foreach (var solverLine in lines)
      {
        solverLine.IsSolved = false;
        solverLine.HasContradiction = false;
      }
    }

    private void RestoreState(BacktrackState state, List<SolverLine> listSolverLine)
    {
      Board.CurrentState = state.BoardSnapshot;
      RestoreHintSolved(Board.HintsRow, state.HintRowSolved);
      RestoreHintSolved(Board.HintsColumn, state.HintColumnSolved);
      ResetSolverLines(listSolverLine);
      _ListStaticAnalysis.Clear();
    }

    private void StaticAnalysis()
    {
      if (Config.StaticAnalysisEnabled == false)
      {
        return;
      }

      for (Int32 indexRow = 0; indexRow < Board.RowCount; indexRow++)
      {
        if (Config.Break)
        {
          break;
        }
        StaticAnalysisCheckLine(true, indexRow);
      }
      for (Int32 indexColumn = 0; indexColumn < Board.ColumnCount; indexColumn++)
      {
        if (Config.Break)
        {
          break;
        }
        StaticAnalysisCheckLine(false, indexColumn);
      }
    }

    private void StaticAnalysisCheckLine(Boolean isRow, Int32 index)
    {
      CellValue[] line = isRow ? Board.GetRow(index) : Board.GetColumn(index);
      Hint[] hints = isRow ? Board.HintsRow[index] : Board.HintsColumn[index];
      StaticAnalysisCheckLine(isRow, index, line, hints, false);

      // refresh data
      line = line.ToList().Reverse<CellValue>().ToArray();
      hints = hints.ToList().Reverse<Hint>().ToArray();
      StaticAnalysisCheckLine(isRow, index, line, hints, true);
    }
    private void StaticAnalysisCheckLine(Boolean isRow, Int32 index, CellValue[] line, Hint[] hints, Boolean reverted)
    {
      Boolean allHintsSolved = hints.All(hint => hint.IsSolved);
      if (allHintsSolved)
      {
        return;
      }

      void createStaticAnalysis(Boolean setColor, CellValue[] line, Int32 indexOnLine)
      {
        Int32 Row = isRow ? index : indexOnLine;
        if (isRow == false && reverted)
        {
          Row = Board.RowCount - 1 - Row;
        }
        Int32 Column = isRow ? indexOnLine : index;
        if (isRow == true && reverted)
        {
          Column = Board.ColumnCount - 1 - Column;
        }

        CellValue cellValue;
        StaticAnalysisType type;
        if (setColor)
        {
          cellValue = CellValue.Color;
          type = StaticAnalysisType.SolvedColor;
        }
        else
        {
          cellValue = CellValue.Background;
          type = StaticAnalysisType.SolvedBackground;
        }

        line[indexOnLine] = cellValue;
        Board[Row, Column] = cellValue;

        _ListStaticAnalysis.Add(new StaticAnalysis()
        {
          IsRow = isRow,
          Row = Row,
          Column = Column,
          Type = type
        });
      }

      Int32 indexOnLine = FindFirstOrNext(line, 0);

      Boolean itFits = true;
      for (Int32 indexHint = 0; indexHint < hints.Length; indexHint++)
      {
        Hint hint = hints[indexHint];
        if (!hint.IsSolved)
        {
          Boolean startsWithUnknown = line[indexOnLine] == CellValue.Unknown;
          if (startsWithUnknown)
          {
            // check unknown cell before solved hint
            for (Int32 inHintCounter = 0; inHintCounter < hint.Count; inHintCounter++)
            {
              indexOnLine++;
              if (GetCellValue(line, indexOnLine) != CellValue.Color)
              {
                itFits = false;
                break;
              }
            }

            if (itFits)
            {
              indexOnLine++;
              if (GetCellValue(line, indexOnLine) == CellValue.Background)
              {
                createStaticAnalysis(false, line, indexOnLine - hint.Count - 1);
                hint.IsSolved = true;
              }
            }
          }
          else
          {
            Debug.Assert(line[indexOnLine] == CellValue.Color);
            // check continuos hint and unknown cell after solved hint
            for (Int32 inHintCounter = 1; inHintCounter < hint.Count; inHintCounter++)
            {
              indexOnLine++;
              if (GetCellValue(line, indexOnLine) == CellValue.Unknown)
              {
                createStaticAnalysis(true, line, indexOnLine);
              }
              else if (GetCellValue(line, indexOnLine) != CellValue.Color)
              {
                itFits = false;
                break;
              }
            }
            if (itFits)
            {
              indexOnLine++;
              if (GetCellValue(line, indexOnLine) == CellValue.Unknown)
              {
                createStaticAnalysis(false, line, indexOnLine);
              }
              hint.IsSolved = true;
            }
          }
        }
        else
        {
          indexOnLine += hint.Count;
        }

        if (Config.Break)
        {
          break;
        }
        if (itFits == false)
        {
          break;
        }

        indexOnLine = FindFirstOrNext(line, indexOnLine);
        if (indexOnLine == -1)
        {
          break;
        }
      }

      allHintsSolved = hints.All(hint => hint.IsSolved);
      if (allHintsSolved)
      {
        for (Int32 indexCell = 0; indexCell < line.Length; indexCell++)
        {
          if (line[indexCell] == CellValue.Unknown)
          {
            createStaticAnalysis(false, line, indexCell);
          }
        }
      }
    }

    private void OverlapAnalysis()
    {
      if (Config.OverlapAnalysisEnabled == false)
      {
        return;
      }

      for (Int32 indexRow = 0; indexRow < Board.RowCount; indexRow++)
      {
        if (Config.Break)
        {
          break;
        }
        OverlapAnalysisCheckLine(true, indexRow);
      }
      for (Int32 indexColumn = 0; indexColumn < Board.ColumnCount; indexColumn++)
      {
        if (Config.Break)
        {
          break;
        }
        OverlapAnalysisCheckLine(false, indexColumn);
      }
    }

    private void OverlapAnalysisCheckLine(Boolean isRow, Int32 index)
    {
      CellValue[] line = isRow ? Board.GetRow(index) : Board.GetColumn(index);
      Hint[] hints = isRow ? Board.HintsRow[index] : Board.HintsColumn[index];

      LineOverlap.Result? result = LineOverlap.Solve(line, hints);
      if (result == null || !result.Changed)
      {
        return;
      }

      for (Int32 cellIndex = 0; cellIndex < result.Deductions.Length; cellIndex++)
      {
        CellValue? deduction = result.Deductions[cellIndex];
        if (deduction == null)
        {
          continue;
        }

        Int32 row = isRow ? index : cellIndex;
        Int32 col = isRow ? cellIndex : index;

        Board[row, col] = deduction.Value;

        StaticAnalysisType type = deduction.Value == CellValue.Color
          ? StaticAnalysisType.SolvedColor
          : StaticAnalysisType.SolvedBackground;

        _ListStaticAnalysis.Add(new StaticAnalysis()
        {
          IsRow = isRow,
          Row = row,
          Column = col,
          Type = type
        });
      }

      for (Int32 hintIndex = 0; hintIndex < result.HintSolved.Length; hintIndex++)
      {
        if (result.HintSolved[hintIndex])
        {
          hints[hintIndex].IsSolved = true;
        }
      }
    }
  }
}
