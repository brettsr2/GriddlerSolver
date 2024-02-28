using System;

namespace Griddler_Solver
{
  class Hint
  {
    public static Int32 ColorBackgroundId = 0;

    public Int32 ColorId
    { get; set; }

    public Int32 Count
    { get; set; }

    public override String ToString()
    {
      return $"[{ColorId}:{Count}]";
    }
  }
}
