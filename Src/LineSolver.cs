using System;
using System.Collections.Generic;
using System.Linq;

namespace Griddler_Solver
{
  public enum CellValue
  {
    Unknown = -1,
    Blank = 0,
    Filled = 1,
  }

  public class LineSolver
  {
    public UInt64 GeneratedPermutations
    { get; set; }

    private readonly IList<CellValue[]> _CurrentLinePermutations;

    public LineSolver()
    {
      _CurrentLinePermutations = new List<CellValue[]>();
    }

    internal CellValue[] Solve(CellValue[] line, Hint[] hints)
    {
      _CurrentLinePermutations.Clear();
      GeneratedPermutations = 0;

      if (IsLineFull(line))
      {
        return line;
      }

      var clone = line.ToArray();

      // If line is empty
      if (hints.Length <= 1 && hints[0].IsBackground)
      {
        FillEmptyCells(clone);
        return clone;
      }

      GeneratePermutations(line.Length, hints);
      GeneratedPermutations = Convert.ToUInt64(_CurrentLinePermutations.Count);

      var filteredPermutations = FilterPermutations(clone);
      Merge(clone, filteredPermutations);

      return clone;
    }

    private IList<CellValue[]> FilterPermutations(CellValue[] line)
    {
      List<CellValue[]> validPermutations = new List<CellValue[]>();

      foreach (var permutation in _CurrentLinePermutations)
      {
        bool isValid = true;

        for (int i = 0; i < permutation.Length; i++)
        {
          CellValue cellValue = line[i];
          CellValue permutationValue = permutation[i];

          if (cellValue != CellValue.Unknown && cellValue != permutationValue)
          {
            isValid = false;
            break;
          }
        }

        if (isValid)
        {
          validPermutations.Add(permutation);
        }
      }

      return validPermutations;
    }

    internal void GeneratePermutations(int length, Hint[] hints)
    {
      CellValue[] line = new CellValue[length];

      for (int i = 0; i < length; i++)
      {
        line[i] = CellValue.Unknown;
      }

      GeneratePermutations(line, 0, new Queue<Hint>(hints));
    }

    private void GeneratePermutations(CellValue[] line, int startIdx, Queue<Hint> hints)
    {
      if (!hints.Any())
      {
        FillEmptyCells(line);
        _CurrentLinePermutations.Add(line.ToArray());

        return;
      }

      var hint = hints.Dequeue();

      // This maximum index this hint can be and still fit the others on
      int maxStartingIndex = line.Length - hints.Sum(h => h.Count) - hints.Count - hint.Count + 1;

      for (int i = startIdx; i < maxStartingIndex; i++)
      {
        var clone = line.ToArray();
        FillCells(clone, i, hint.Count, CellValue.Filled);

        GeneratePermutations(clone, i + hint.Count + 1, new Queue<Hint>(hints));
      }
    }

    internal void Merge(CellValue[] line, IList<CellValue[]> permutations)
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
          line[i] = CellValue.Blank;
        }
      }
    }

    private void FillCells(CellValue[] line, int startIdx, int numberOfCells, CellValue value)
    {
      if (numberOfCells == 0)
      {
        return;
      }

      for (int i = startIdx; i < startIdx + numberOfCells; i++)
      {
        line[i] = value;
      }
    }

    public static bool IsLineFull(CellValue[] line)
    {
      return line.All(i => i != CellValue.Unknown);
    }

    public static int[] CreateLineHints(CellValue[] line)
    {
      int currentCount = 0;
      IList<int> hints = new List<int>();

      for (int i = 0; i < line.Length; i++)
      {
        if (line[i] == CellValue.Filled)
        {
          currentCount++;
        }
        else if (currentCount > 0)
        {
          hints.Add(currentCount);
          currentCount = 0;
        }
      }

      if (currentCount > 0)
      {
        hints.Add(currentCount);
      }

      if (!hints.Any())
      {
        hints.Add(0);
      }

      return hints.ToArray();
    }

    public static bool IsLineLogicallyComplete(CellValue[] line, int[] hints)
    {
      return CreateLineHints(line).SequenceEqual(hints);
    }
  }
}
