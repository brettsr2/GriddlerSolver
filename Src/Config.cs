using System;
using static System.Formats.Asn1.AsnWriter;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Xml.Linq;

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
    public Boolean ThreadsEnabled
    { get; set; }
    public Boolean MultithreadEnabled
    { get; set; }
    public Boolean PermutationsLimit
    { get; set; }
    public Boolean StaticAnalysisEnabled
    { get; set; }

    public Boolean StepMode
    { get; set; }

    public Int64 TicksCurrentIteration
    { get; set; } = DateTime.Now.Ticks;
    public Int64 TicksStart
    { get; set; } = DateTime.Now.Ticks;
  }
}
