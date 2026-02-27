using System;
using System.Text.Json.Serialization;

namespace Griddler_Solver
{
  class Hint
  {
    public Int32 ColorId
    { get; set; }

    public Int32 Count
    { get; set; }

    [JsonIgnore]
    public Boolean IsSolved
    { get; set; }

    public override String ToString()
    {
      return $"[{ColorId}:{Count}]";
    }
  }
}
