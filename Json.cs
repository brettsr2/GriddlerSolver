using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace Griddler_Solver.Json
{
  class Puzzle
  {
    public List<SolidColorBrush> GetListSolidColorBrush()
    {
      List<SolidColorBrush> listSolidColorBrush = new List<SolidColorBrush>();
      listSolidColorBrush.Add(new SolidColorBrush());

      foreach (Int32 color in usedColors)
      {
        String hexColor = colors[color];

        var solidBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#" + hexColor));
        solidBrush.Freeze();

        listSolidColorBrush.Add(solidBrush);
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
