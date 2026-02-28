using System;
using System.Linq;

namespace Griddler_Solver
{
  internal static class LineOverlap
  {
    internal class Result
    {
      public CellValue?[] Deductions
      { get; set; } = Array.Empty<CellValue?>();
      public Boolean[] HintSolved
      { get; set; } = Array.Empty<Boolean>();
      public Boolean Changed
      { get; set; }
    }

    public static Result? Solve(CellValue[] line, Hint[] hints)
    {
      // All cells already known
      if (!line.Contains(CellValue.Unknown))
      {
        return new Result { Changed = false };
      }

      // No hints — all Unknown cells become Background
      if (hints.Length == 0)
      {
        CellValue?[] deductions = new CellValue?[line.Length];
        Boolean changed = false;
        for (Int32 j = 0; j < line.Length; j++)
        {
          if (line[j] == CellValue.Unknown)
          {
            deductions[j] = CellValue.Background;
            changed = true;
          }
        }
        return new Result { Deductions = deductions, Changed = changed };
      }

      // All hints already solved
      if (hints.All(h => h.IsSolved))
      {
        return new Result { Changed = false };
      }

      Int32[] leftStart = new Int32[hints.Length];
      if (!TryFitLeft(line, hints, 0, 0, leftStart))
      {
        return null;
      }

      Int32[] rightStart = new Int32[hints.Length];
      if (!TryFitRight(line, hints, rightStart))
      {
        return null;
      }

      CellValue?[] result = new CellValue?[line.Length];
      Boolean[] hintSolved = new Boolean[hints.Length];
      Boolean anyChanged = false;

      // Color overlap
      for (Int32 i = 0; i < hints.Length; i++)
      {
        Int32 overlapStart = rightStart[i];
        Int32 overlapEnd = leftStart[i] + hints[i].Count - 1;

        for (Int32 j = overlapStart; j <= overlapEnd; j++)
        {
          if (line[j] == CellValue.Unknown)
          {
            result[j] = CellValue.Color;
            anyChanged = true;
          }
        }
      }

      // Unreachable cells: not in any hint's possible range → Background
      Boolean[] reachable = new Boolean[line.Length];
      for (Int32 i = 0; i < hints.Length; i++)
      {
        Int32 rangeStart = leftStart[i];
        Int32 rangeEnd = rightStart[i] + hints[i].Count - 1;
        for (Int32 j = rangeStart; j <= rangeEnd; j++)
        {
          reachable[j] = true;
        }
      }
      for (Int32 j = 0; j < line.Length; j++)
      {
        if (!reachable[j] && line[j] == CellValue.Unknown)
        {
          result[j] = CellValue.Background;
          anyChanged = true;
        }
      }

      return new Result
      {
        Deductions = result,
        HintSolved = hintSolved,
        Changed = anyChanged
      };
    }

    private static Boolean TryFitLeft(CellValue[] line, Hint[] hints, Int32 hintIdx, Int32 startPos, Int32[] result)
    {
      Int32 n = line.Length;

      if (hintIdx >= hints.Length)
      {
        for (Int32 j = startPos; j < n; j++)
        {
          if (line[j] == CellValue.Color)
          {
            return false;
          }
        }
        return true;
      }

      Int32 count = hints[hintIdx].Count;

      // Minimum space needed for remaining hints after this one
      Int32 minRemaining = 0;
      for (Int32 h = hintIdx + 1; h < hints.Length; h++)
      {
        minRemaining += hints[h].Count + 1;
      }

      Int32 maxStart = n - count - minRemaining;

      // Find first Color cell at or after startPos (uncovered-Color deadline)
      Int32 firstColor = -1;
      for (Int32 j = startPos; j < n; j++)
      {
        if (line[j] == CellValue.Color)
        {
          firstColor = j;
          break;
        }
      }

      for (Int32 pos = startPos; pos <= maxStart; pos++)
      {
        // If there's an uncovered Color cell before pos, fail
        if (firstColor >= 0 && firstColor < pos)
        {
          return false;
        }

        // Check hint span [pos, pos+count-1] has no Background
        Boolean spanOk = true;
        for (Int32 j = pos; j < pos + count; j++)
        {
          if (line[j] == CellValue.Background)
          {
            // Jump past the Background cell
            pos = j; // outer for loop will increment to j+1
            spanOk = false;
            break;
          }
        }
        if (!spanOk)
        {
          continue;
        }

        // Check separator after hint is not Color
        if (pos + count < n && line[pos + count] == CellValue.Color)
        {
          continue;
        }

        // Try placing hint here and recursively fit remaining
        result[hintIdx] = pos;
        if (TryFitLeft(line, hints, hintIdx + 1, pos + count + 1, result))
        {
          return true;
        }

        // Recursion failed — if current pos is a Color cell, we can't skip it
        if (line[pos] == CellValue.Color)
        {
          return false;
        }
      }

      return false;
    }

    private static Boolean TryFitRight(CellValue[] line, Hint[] hints, Int32[] rightStart)
    {
      Int32 n = line.Length;
      Int32 k = hints.Length;

      // Reverse the line
      CellValue[] reversedLine = new CellValue[n];
      for (Int32 j = 0; j < n; j++)
      {
        reversedLine[j] = line[n - 1 - j];
      }

      // Reverse the hints
      Hint[] reversedHints = new Hint[k];
      for (Int32 i = 0; i < k; i++)
      {
        reversedHints[i] = hints[k - 1 - i];
      }

      // Run leftmost fit on reversed data
      Int32[] leftStartRev = new Int32[k];
      if (!TryFitLeft(reversedLine, reversedHints, 0, 0, leftStartRev))
      {
        return false;
      }

      // Convert back: rightStart[i] = n - leftStartRev[k-1-i] - hints[i].Count
      for (Int32 i = 0; i < k; i++)
      {
        rightStart[i] = n - leftStartRev[k - 1 - i] - hints[i].Count;
      }

      return true;
    }
  }
}
