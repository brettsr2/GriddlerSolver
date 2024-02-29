using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;

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

    public UInt64 CalculatePermutations()
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

      BigInteger nFactorial = Factorial(n);
      BigInteger kFactorial = Factorial(k);
      BigInteger nMinuskFactorial = Factorial(n-k);

      BigInteger combinations = nFactorial / (kFactorial * nMinuskFactorial);
      return (UInt64)combinations;
    }

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
