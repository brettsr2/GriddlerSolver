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
    public Int32 ListIndex // index in the sorted list by score
    { get; set; }

    public Board Board
    { get; set; } = new();

    public Hint[] Hints
    { get; set; } = Array.Empty<Hint>();

    public SolverLineSolver SolverLineSolver
    { get; set; } = new();

    public void Solve()
    {
      SolverLineSolver.Config = Config;

      if (IsRow)
      {
        Board.MergeRow(Index, SolverLineSolver.Solve(Board.GetRow(Index), Hints));
      }
      else
      {
        Board.MergeColumn(Index, SolverLineSolver.Solve(Board.GetColumn(Index), Hints));
      }
    }

    public override String ToString()
    {
      return $"{ListIndex} - {(IsRow ? "Row" : "Column")} {Number} - {Score}";
    }
  }
}
