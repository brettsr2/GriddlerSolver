using System;

namespace Griddler_Solver
{
  internal class SolverLine
  {
    public Int32 Index
    { get; set; }

    public Boolean IsRow
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

    public Boolean Changed
    { get; set; } = false;

    public Boolean IsSolved
    { get; set; } = false;

    public Boolean HasContradiction
    { get; set; } = false;

    public void Solve()
    {
      Changed = false;
      HasContradiction = false;

      CellValue[] snapshot;
      if (IsRow)
      {
        snapshot = Board.GetRow(Index);
      }
      else
      {
        snapshot = Board.GetColumn(Index);
      }

      var result = Solve(snapshot, Hints);

      if (IsRow)
      {
        Board.MergeRow(Index, result);
      }
      else
      {
        Board.MergeColumn(Index, result);
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

      // Forward+Backward DP via LineOverlap — O(NK) per line
      var overlapResult = LineOverlap.Solve(clone, hints);
      if (overlapResult == null)
      {
        HasContradiction = true;
        return clone;
      }
      if (overlapResult.Changed)
      {
        Changed = true;
        for (Int32 j = 0; j < clone.Length; j++)
        {
          if (overlapResult.Deductions[j] is CellValue val)
          {
            clone[j] = val;
          }
        }
      }
      if (IsLineFull(clone))
      {
        IsSolved = true;
      }
      return clone;
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
  }
}
