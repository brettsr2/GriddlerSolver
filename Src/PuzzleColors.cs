using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Griddler_Solver
{
  internal class PuzzleColors
  {
    public String HexColor
    { get; set; } = String.Empty;

    private SolidColorBrush? _ColorBrush = null;
    public SolidColorBrush ColorBrush
    { 
      get
      {
        if (_ColorBrush == null)
        {
          _ColorBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(HexColor));
          _ColorBrush.Freeze();
        }

        return _ColorBrush;
      }
    }
  }
}
