﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Griddler_Solver
{
  internal class SolverLine
  {
    class FindResult
    {
      public Int32 BegIndex
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

    public UInt64 FirstPermutationsCount
    { get; set; }

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
        return (UInt64)_CurrentLinePermutations.Count;
      }
    }

    public Boolean Changed
    { get; set; } = false;

    public Boolean IsSolved
    { get; set; } = false;

    public Int32 IterationOfGenerating
    { get; set; } = 0;

    private List<CellValue[]> _CurrentLinePermutations = [];

    public void Solve()
    {
      Changed = false;
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

      if (_CurrentLinePermutations.Count == 0)
      {
        GeneratePermutations(line, hints);
        FirstPermutationsCount = CurrentPermutationsCount;
      }
      else
      {
        List<CellValue[]> newCurrent = new List<CellValue[]>(_CurrentLinePermutations.Count);
        foreach (var permutation in _CurrentLinePermutations)
        {
          if (IsPermutationValid(line, permutation))
          {
            newCurrent.Add(permutation);
          }
        }

        _CurrentLinePermutations = newCurrent;
      }

      if (Config.Break)
      {
        return clone;
      }

      Merge(clone, _CurrentLinePermutations);

      if (Config.PermutationsLimit == false)
      {
        _CurrentLinePermutations = [];
      }

      return clone;
    }

    private Boolean IsPermutationValid(CellValue[] line, CellValue[] permutation)
    {
      for (Int32 index = 0; index < permutation.Length; index++)
      {
        CellValue cellValue = line[index];
        CellValue cellValuePermutation = permutation[index];
        if (cellValue != CellValue.Unknown && cellValue != cellValuePermutation)
        {
          return false;
        }
      }

      return true;
    }
    private void GeneratePermutations(CellValue[] lineOrigin, Hint[] hints)
    {
      if (Config.Break)
      {
        return;
      }

      Int32 maxHintCellCount = hints.Sum(h => h.Count);

      Int32 begIndex = 0;
      if (Config.PermutationsLimit)
      {
        FindResult? findResult = FindLastFitIndex(lineOrigin, hints);
        if (findResult != null)
        {
          begIndex = findResult.BegIndex;
          hints = findResult.Hints;
        }
      }

      //CellValue[] line = new CellValue[lineOrigin.Length];
      CellValue[] line = (CellValue[])lineOrigin.Clone();
      GeneratePermutations(lineOrigin, line, begIndex, new Queue<Hint>(hints), maxHintCellCount);
    }
    private void GeneratePermutations(CellValue[] lineOrigin, CellValue[] line, Int32 startIdx, Queue<Hint> hints, Int32 maxHintCellCount)
    {
      if (Config.Break)
      {
        return;
      }

      TimeSpan timeSpanCurrentIteration = TimeSpan.FromTicks(DateTime.Now.Ticks - Config.TicksCurrentIteration);
      if (timeSpanCurrentIteration.TotalSeconds >= 10)
      {
        Config.TicksCurrentIteration = DateTime.Now.Ticks;

        TimeSpan timeSpanStart = TimeSpan.FromTicks(DateTime.Now.Ticks - Config.TicksStart);

        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.Append($"[{timeSpanStart.ToString(Solver.TimeFormat)}] ");
        stringBuilder.Append($"{ToString()}");
        Solver.PrintMemoryInfo(Config, stringBuilder);

        Config.Progress?.AddMessage(stringBuilder.ToString());
      }

      if (hints.Count == 0)
      {
        FillEmptyCells(line);
        if (IsPermutationValid(lineOrigin, line))
        {
          _CurrentLinePermutations.Add(line);
        }

        return;
      }

      var hint = hints.Dequeue();

      // This maximum index this hint can be and still fit the others on
      Int32 maxStartingIndex = line.Length - hints.Sum(h => h.Count) - hints.Count - hint.Count + 1;
      for (Int32 index = startIdx; index < maxStartingIndex; index++)
      {
        var clone = (CellValue[])line.Clone();
        if (FillCells(clone, index, hint.Count, (CellValue)hint.ColorId, maxHintCellCount))
        {
          GeneratePermutations(lineOrigin, clone, index + hint.Count + 1, new Queue<Hint>(hints), maxHintCellCount);
        }
      }
    }

    private void Merge(CellValue[] line, IList<CellValue[]> permutations)
    {
      if (permutations.Count == 0)
      {
        return;
      }

      for (Int32 index = 0; index < line.Length; index++)
      {
        if (line[index] != CellValue.Unknown)
        {
          continue;
        }

        CellValue value = permutations[0][index];

        Boolean allMatch = true;
        foreach (CellValue[] permutation in permutations)
        {
          if (permutation[index] != value)
          {
            allMatch = false;
            break;
          }
        }
        if (allMatch)
        {
          Changed = true;
          line[index] = value;
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
    private Boolean FillCells(CellValue[] line, Int32 indexStart, Int32 numberOfCells, CellValue value, Int32 maxHintCellCount)
    {
      Debug.Assert(numberOfCells > 0);
      
      for (Int32 index = indexStart; index < indexStart + numberOfCells; index++)
      {
        if (line[index] == CellValue.Background) // wrong permutation
        {
          return false;
        }
        line[index] = value;
      }

      Int32 hintCellCount = 0;
      foreach (CellValue cellValue in line)
      {
        if (cellValue == CellValue.Color)
        {
          hintCellCount++;
        }
      }

      return hintCellCount <= maxHintCellCount;
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
        indexOnLine += hint.Count - 1 + 1;

        Int32 indexOnLineNew = FindFirst(line, indexOnLine);
        if (indexOnLineNew == -1)
        {
          break;
        }

        indexOnLine = indexOnLineNew;
      }

      return new FindResult()
      {
        BegIndex = indexOnLine,
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
      UInt64 percentFirst = FirstPermutationsCount * 100 / MaxPermutationsCount;
      UInt64 percentCurrent = CurrentPermutationsCount * 100 / MaxPermutationsCount;

      StringBuilder stringBuilder = new StringBuilder();

      stringBuilder.Append($"{(IsRow ? "Row:" : "Column:")} {Number} ");
      stringBuilder.Append($"Score: {Score} ");
      stringBuilder.Append($"MaxPermCount: {MaxPermutationsCount} ");
      if (FirstPermutationsCount > 0)
      {
        stringBuilder.Append($"Iteration: {IterationOfGenerating} ");
        stringBuilder.Append($"FirstPermCount: {FirstPermutationsCount}({percentFirst}%) ");
      }
      stringBuilder.Append($"CurrentPermCount: {CurrentPermutationsCount}({percentCurrent}%) ");

      return stringBuilder.ToString();
    }
  }
}
