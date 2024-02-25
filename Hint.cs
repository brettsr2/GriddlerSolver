using System;

namespace Griddler_Solver
{
  internal class Hint
  {
    public static Int32 ColorBackground = 0;
    public static Hint DefinitionBackground = new Hint() { ColorId = ColorBackground, Count = 1 };

    public Int32 ColorId
    { get; set; }

    public Int32 Count
    { get; set; }

    public Boolean IsBackground
    {
      get
      {
        return (ColorId == ColorBackground);
      }
    }

    public override String ToString()
    {
      return $"[{ColorId}:{Count}]";
    }
  }
}
