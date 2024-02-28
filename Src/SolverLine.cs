using System;

namespace Griddler_Solver
{
  internal class SolverLine
  {
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
    public Boolean IsColumn
    { 
      get
      {
        return !IsRow;
      }
    }

    public Int32 Score
    { get; set; }
    public Boolean Solved
    { get; set; }

    public override String ToString()
    {
      return $"{(IsRow ? "Row" : "Column")} {Number} - {Score}";
    }
  }
}
