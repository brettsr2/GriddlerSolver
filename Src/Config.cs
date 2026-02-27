using System;

namespace Griddler_Solver
{
  class Config
  {
    public String Name
    { get; set; } = String.Empty;
    public Boolean Break
    { get; set; } = false;

    public Boolean Draw
    { get; set; }
    public IProgress? Progress
    { get; set; }

    public Boolean ScoreSortingEnabled
    { get; set; }
    public Boolean PermutationAnalysisEnabled
    { get; set; }
    public Boolean MultithreadEnabled
    { get; set; }
    public Boolean PermutationsLimit
    { get; set; }
    public Boolean StaticAnalysisEnabled
    { get; set; }
    public Boolean OverlapAnalysisEnabled
    { get; set; }
    public Boolean BacktrackingEnabled
    { get; set; }

    public Boolean StepMode
    { get; set; }

    public Int64 TicksCurrentIterationStart
    { get; set; }
    public Int64 TicksCurrentIterationTimer
    { get; set; }
    public Int64 TicksStart
    { get; set; } = DateTime.Now.Ticks;
    public Int32 IterationPrefixLength
    { get; set; }
  }
}
