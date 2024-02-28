using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace Griddler_Solver.Json
{
  class Puzzle
  {
    public List<PuzzleColors> GetListSolidColorBrush()
    {
      List<PuzzleColors> listSolidColorBrush = [];
      listSolidColorBrush.Add(new PuzzleColors());

      foreach (Int32 color in usedColors)
      {
        listSolidColorBrush.Add(new PuzzleColors()
        { 
          HexColor = "#" + colors[color] 
        });
      }

      return listSolidColorBrush;
    }

    // js variables
    public List<String> colors { get; set; } = new List<String>();
    public List<Int32> usedColors { get; set; } = new List<Int32>();

    public List<List<List<Int32>>> topHeader { get; set; } = new List<List<List<Int32>>>();
    public List<List<List<Int32>>> leftHeader { get; set; } = new List<List<List<Int32>>>();
  }
}
