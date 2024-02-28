using System;
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
    public UInt64 GeneratedPermutations
    { get; set; }

    private readonly IList<CellValue[]> _CurrentLinePermutations;

    public LineSolver()
    {
      _CurrentLinePermutations = new List<CellValue[]>();
    }

    public CellValue[] Solve(CellValue[] line, Hint[] hints)
    {
      _CurrentLinePermutations.Clear();
      GeneratedPermutations = 0;

      if (IsLineFull(line))
      {
        return line;
      }

      var clone = line.ToArray();

      // If line is empty
      if (hints.Length == 0)
      {
        FillEmptyCells(clone);
        return clone;
      }

      GeneratePermutations(line, hints);
      GeneratedPermutations = Convert.ToUInt64(_CurrentLinePermutations.Count);

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
      CellValue[] line = new CellValue[lineOrigin.Length];
      for (Int32 index = 0; index < lineOrigin.Length; index++)
      {
        line[index] = CellValue.Unknown;
      }

      GeneratePermutations(lineOrigin, line, 0, new Queue<Hint>(hints));
    }

    private void GeneratePermutations(CellValue[] lineOrigin, CellValue[] line, int startIdx, Queue<Hint> hints)
    {
      if (!hints.Any())
      {
        FillEmptyCells(line);
        if (IsPermutationValid(lineOrigin, line))
        {
          _CurrentLinePermutations.Add(line.ToArray());
        }

        return;
      }

      var hint = hints.Dequeue();

      // This maximum index this hint can be and still fit the others on
      int maxStartingIndex = line.Length - hints.Sum(h => h.Count) - hints.Count - hint.Count + 1;

      for (int i = startIdx; i < maxStartingIndex; i++)
      {
        var clone = line.ToArray();
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

      for (int i = 0; i < line.Length; i++)
      {
        if (line[i] != CellValue.Unknown)
        {
          continue;
        }

        var value = permutations[0][i];

        bool allMatch = permutations.All(p => p[i] == value);

        if (allMatch)
        {
          line[i] = value;
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
