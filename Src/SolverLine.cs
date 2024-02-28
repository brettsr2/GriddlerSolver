using System;
using System.Linq;

namespace Griddler_Solver
{
  internal class SolverLine
  {
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
    public Boolean Solved
    { get; set; }

    public Board Board
    { get; set; } = new();

    public Hint[] Hints
    { get; set; } = Array.Empty<Hint>();

    public LineSolver LineSolver
    { get; set; } = new();

    public Boolean Solve()
    {
      LineSolver.Config = Config;

      CellValue[] currentLine, updatedLine;

      if (IsRow)
      {
        currentLine = Board.GetRow(Index);
        updatedLine = LineSolver.Solve(Board.GetRow(Index), Hints);
      }
      else
      {
        currentLine = Board.GetColumn(Index);
        updatedLine = LineSolver.Solve(Board.GetColumn(Index), Hints);
      }

      bool hasLineChanged = !currentLine.SequenceEqual(updatedLine);
      if (hasLineChanged)
      {
        if (IsRow)
        {
          Board.ReplaceRow(Index, updatedLine);
        }
        else
        {
          Board.ReplaceColumn(Index, updatedLine);
        }
      }

      return hasLineChanged;
    }

    public override String ToString()
    {
      return $"{(IsRow ? "Row" : "Column")} {Number} - {Score}";
    }
  }
}
