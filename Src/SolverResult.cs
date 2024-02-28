using System;

namespace Griddler_Solver
{
  class SolverResult
  {
    public Boolean IsSolved
    { get; set; }
    public CellValue[][] Result
    { get; set; } = new CellValue[0][];
    public TimeSpan TimeTaken
    { get; set; }
    public Int32 Iterations
    { get; set; }
  }
}
