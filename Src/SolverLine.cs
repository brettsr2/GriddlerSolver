using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Griddler_Solver
{
  internal class SolverLine
  {
    class FindResult
    {
      public Int32 BeginIndex
      { get; set; }
      public Int32 EndIndex
      { get; set; }
      public Hint[] Hints
      { get; set; } = Array.Empty<Hint>();
    };

    public Config Config
    { get; set; } = new Config();

    public Int32 Index
    { get; set; }
    public Int32 Number
    {
      get
      {
        return Index + 1;
      }
    }

    public Boolean IsRow
    { get; set; }

    public Int32 Score
    { get; set; }

    public Board Board
    { get; set; } = new();

    public Hint[] Hints
    {
      get
      {
        return IsRow ? Board.HintsRow[Index] : Board.HintsColumn[Index];
      }
    }

    private UInt64 _MaxPermutationsCount = UInt64.MaxValue;
    public UInt64 MaxPermutationsCount
    {
      get
      {
        if (_MaxPermutationsCount == UInt64.MaxValue)
        {
          var line = IsRow ? Board.GetRow(Index) : Board.GetColumn(Index);
          Int32 n = ((line.Length) - (Hints.Sum(hint => hint.Count)) + (1));
          Int32 k = Hints.Length;

          static BigInteger Factorial(BigInteger number)
          {
            if (number <= 1)
            {
              return 1;
            }
            return number * Factorial(number - 1);
          }

          _MaxPermutationsCount = (UInt64)(Factorial(n) / (Factorial(k) * Factorial(n - k)));
        }

        return _MaxPermutationsCount;
      }
    }

    public UInt64 CurrentPermutationsCount
    {
      get
      {
        return _PermutationCount;
      }
    }

    public Boolean Changed
    { get; set; } = false;

    public Boolean IsSolved
    { get; set; } = false;

    public Boolean HasContradiction
    { get; set; } = false;

    private LineBitmask _MergedLine;
    private Boolean _HasMergedLine = false;
    private UInt64 _PermutationCount = 0;

    public void Solve()
    {
      Changed = false;
      HasContradiction = false;
      if (IsRow)
      {
        var row = Solve(Board.GetRow(Index), Hints);
        Board.MergeRow(Index, row);
      }
      else
      {
        var column = Solve(Board.GetColumn(Index), Hints);
        Board.MergeColumn(Index, column);
      }
    }
    private CellValue[] Solve(CellValue[] line, Hint[] hints)
    {
      if (IsLineFull(line))
      {
        Changed = IsSolved = true;
        return line;
      }

      var clone = (CellValue[])line.Clone();
      if (hints.Length == 0)
      {
        Changed = IsSolved = true;

        FillEmptyCells(clone);
        return clone;
      }

      _HasMergedLine = false;
      _PermutationCount = 0;

      GeneratePermutations(line, hints);

      if (Config.Break)
      {
        return clone;
      }

      if (!_HasMergedLine && _PermutationCount == 0)
      {
        HasContradiction = true;
        return clone;
      }

      if (_HasMergedLine)
      {
        CellValue[] merged = _MergedLine.ToLine();
        for (Int32 index = 0; index < clone.Length; index++)
        {
          if (clone[index] == CellValue.Unknown && merged[index] != CellValue.Unknown)
          {
            Changed = true;
            clone[index] = merged[index];
          }
        }
      }

      return clone;
    }

    private void GeneratePermutations(CellValue[] lineOrigin, Hint[] hints)
    {
      if (Config.Break)
      {
        return;
      }

      Int32 maxHintCellCount = hints.Sum(hint => hint.Count);
      Int32 remainingHintCells = maxHintCellCount;

      Hint[] originalHints = hints;
      Int32 beginIndex = 0;
      Boolean optimized = false;
      if (Config.PermutationsLimit)
      {
        FindResult? findResult = FindLastFitIndex(lineOrigin, hints);
        if (findResult != null)
        {
          beginIndex = findResult.BeginIndex;
          hints = findResult.Hints;
          remainingHintCells = hints.Sum(hint => hint.Count);
          optimized = true;
        }
      }

      LineBitmask bmOrigin = LineBitmask.FromLine(lineOrigin);
      LineBitmask bmLine = bmOrigin;
      GeneratePermutations(bmOrigin, bmLine, beginIndex, hints, 0, remainingHintCells, maxHintCellCount);

      // Fallback: if the optimized pass found 0 permutations, retry without optimization
      if (_PermutationCount == 0 && optimized && !Config.Break)
      {
        _HasMergedLine = false;
        _PermutationCount = 0;

        remainingHintCells = maxHintCellCount;
        bmLine = bmOrigin;
        GeneratePermutations(bmOrigin, bmLine, 0, originalHints, 0, remainingHintCells, maxHintCellCount);
      }
    }
    private void GeneratePermutations(LineBitmask lineOrigin, LineBitmask line, Int32 startIndex, Hint[] hints, Int32 hintIndex, Int32 remainingHintCells, Int32 maxHintCellCount)
    {
      if (Config.Break)
      {
        return;
      }

      TimeSpan timeSpanCurrentIteration = TimeSpan.FromTicks(DateTime.Now.Ticks - Config.TicksCurrentIterationTimer);
      if (timeSpanCurrentIteration.TotalSeconds >= 10)
      {
        Config.TicksCurrentIterationTimer = DateTime.Now.Ticks;

        TimeSpan timeSpanStart = TimeSpan.FromTicks(DateTime.Now.Ticks - Config.TicksCurrentIterationStart);

        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.Append(new String(' ', Config.IterationPrefixLength));
        stringBuilder.Append($"[{timeSpanStart.ToString(Solver.TimeFormat)}] ");
        stringBuilder.Append($"{ToString()}");
        stringBuilder.Append($" Workers: {Config.ActiveWorkers}");

        Config.Progress?.AddMessage(stringBuilder.ToString());
      }

      if (hintIndex >= hints.Length)
      {
        line.FillEmptyCells();
        if (line.IsValid(lineOrigin))
        {
          _PermutationCount++;
          if (!_HasMergedLine)
          {
            _MergedLine = line;
            _HasMergedLine = true;
          }
          else
          {
            _MergedLine.MergeWith(line);
          }
        }

        return;
      }

      var hint = hints[hintIndex];
      Int32 remainingAfter = remainingHintCells - hint.Count;
      Int32 separatorsAfter = hints.Length - hintIndex - 1;

      // This maximum index this hint can be and still fit the others on
      Int32 maxStartingIndex = line.Length - remainingAfter - separatorsAfter - hint.Count + 1;
      for (Int32 index = startIndex; index < maxStartingIndex; index++)
      {
        // Gap-Color pruning: if there's a Color cell in origin between startIndex and index,
        // no hint covers it → invalid, and all later positions are worse → break
        if (index > startIndex && lineOrigin.HasColorInRange(startIndex, index - startIndex))
        {
          break;
        }

        // Separator check: cell right after the hint must not be Color in origin
        Int32 afterHint = index + hint.Count;
        if (afterHint < line.Length && lineOrigin.HasColorAt(afterHint))
        {
          continue;
        }

        var clone = line;
        if (clone.FillRange(index, hint.Count, maxHintCellCount))
        {
          GeneratePermutations(lineOrigin, clone, afterHint + 1, hints, hintIndex + 1, remainingAfter, maxHintCellCount);
        }
      }
    }

    private void FillEmptyCells(CellValue[] line)
    {
      for (Int32 index = 0; index < line.Length; index++)
      {
        if (line[index] == CellValue.Unknown)
        {
          line[index] = CellValue.Background;
        }
      }
    }
    private Int32 FindFirst(CellValue[] line, Int32 indexStart)
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
    private FindResult? FindLastFitIndex(CellValue[] line, Hint[] hints)
    {
      List<Hint> listHints = new List<Hint>(hints);

      Int32 indexOnLine = FindFirst(line, 0);
      if (indexOnLine == -1)
      {
        return null;
      }

      for (Int32 indexHint = 0; indexHint < hints.Length; indexHint++)
      {
        Hint hint = hints[indexHint];
        if (!hint.IsSolved)
        {
          break;
        }

        listHints.Remove(hint);
        indexOnLine += hint.Count + 1; // skip hint cells + mandatory separator

        Int32 indexOnLineNew = FindFirst(line, indexOnLine);
        if (indexOnLineNew == -1)
        {
          break;
        }

        indexOnLine = indexOnLineNew;
      }

      return new FindResult()
      {
        BeginIndex = indexOnLine,
        Hints = listHints.ToArray(),
      };
    }

    public Boolean IsLineFull(CellValue[] line)
    {
      foreach (CellValue value in line)
      {
        if (value == CellValue.Unknown)
        {
          return false;
        }
      }

      return true;
    }

    public override String ToString()
    {
      UInt64 percentCurrent = CurrentPermutationsCount * 100 / MaxPermutationsCount;

      StringBuilder stringBuilder = new StringBuilder();

      stringBuilder.Append($"{(IsRow ? "Row:" : "Col:")} {Number}, ");
      stringBuilder.Append($"Current: {Solver.FormatNumber(CurrentPermutationsCount)}({percentCurrent}%) ");

      return stringBuilder.ToString();
    }
  }
}
