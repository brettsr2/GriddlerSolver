using System;

namespace Griddler_Solver
{
  class StaticAnalysis
  {
    public Boolean IsRow
    { get; set; }
    public Int32 Row
    { get; set; }
    public Int32 Column
    { get; set; }

    public StaticAnalysisType Type
    { get; set; }

    public override String ToString()
    {
      return $"[{(IsRow ? "Row" : "Column")} - {Row}:{Column}:{Type}]";
    } 
  }
}
