using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Griddler_Solver
{
  enum CellValue
  {
    Unknown = 0,
    Background = 1,
    Color1 = 2,
  }

  class LineSolver
  {
    public Config Config
    { get; set; } = new Config();

    public UInt64 GeneratedPermutations
    { get; set; }

    private List<CellValue[]> _CurrentLinePermutations = [];

    public CellValue[] Solve(CellValue[] line, Hint[] hints)
    {
      if (IsLineFull(line))
      {
        return line;
      }

      var clone = (CellValue[])line.Clone();
      if (hints.Length == 0)
      {
        FillEmptyCells(clone);
        return clone;
      }

      if (_CurrentLinePermutations.Count == 0)
      {
        GeneratePermutations(line, hints);
        GeneratedPermutations = Convert.ToUInt64(_CurrentLinePermutations.Count);
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
        GeneratedPermutations = Convert.ToUInt64(_CurrentLinePermutations.Count);
      }

      if (Config.Break)
      {
        return clone;
      }

      Merge(clone, _CurrentLinePermutations);
      return clone;
    }

    private Boolean IsPermutationValid(CellValue[] line, CellValue[] permutation)
    {
      for (int i = 0; i < permutation.Length; i++)
      {
        CellValue cellValue = line[i];
        CellValue cellValuePermutation = permutation[i];
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

      CellValue[] line = new CellValue[lineOrigin.Length];
      GeneratePermutations(lineOrigin, line, 0, new Queue<Hint>(hints));
    }
    private void GeneratePermutations(CellValue[] lineOrigin, CellValue[] line, int startIdx, Queue<Hint> hints)
    {
      if (Config.Break)
      {
        return;
      }

      if (hints.Count == 0)
      {
        FillEmptyCells(line);
        _CurrentLinePermutations.Add(line);

        return;
      }

      var hint = hints.Dequeue();

      // This maximum index this hint can be and still fit the others on
      int maxStartingIndex = line.Length - hints.Sum(h => h.Count) - hints.Count - hint.Count + 1;

      for (int i = startIdx; i < maxStartingIndex; i++)
      {
        var clone = (CellValue[])line.Clone();
        FillCells(clone, i, hint.Count, (CellValue)hint.ColorId);

        GeneratePermutations(lineOrigin, clone, i + hint.Count + 1, new Queue<Hint>(hints));
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
          line[index] = value;
        }
      }
    }

    private void FillEmptyCells(CellValue[] line)
    {
      for (int i = 0; i < line.Length; i++)
      {
        if (line[i] == CellValue.Unknown)
        {
          line[i] = CellValue.Background;
        }
      }
    }
    private void FillCells(CellValue[] line, Int32 indexStart, Int32 numberOfCells, CellValue value)
    {
      Debug.Assert(numberOfCells > 0);

      for (int i = indexStart; i < indexStart + numberOfCells; i++)
      {
        line[i] = value;
      }
    }

    public static bool IsLineFull(CellValue[] line)
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
  }
}
