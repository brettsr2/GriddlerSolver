using System;

namespace Griddler_Solver
{
  class Config
  {
    public String Name
    { get; set; } = String.Empty;
    public volatile Boolean Break;

    public IProgress? Progress
    { get; set; }

    public Boolean MultithreadEnabled
    { get; set; }
  }
}
