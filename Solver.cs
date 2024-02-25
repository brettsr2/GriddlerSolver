using System;
using System.Collections.Generic;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

using NonogramSolver;

namespace Griddler_Solver
{
  internal class Solver
  {
    private SolidColorBrush _BrushGrey = new(Colors.Gray);
    private SolidColorBrush _BrushBlack = new(Colors.Black);

    private Double _CellSize = 15;

    private Int32 _MaxRowItemsCount
    {
      get
      {
        return GetMaxItemCount(HintsRow);
      }
    }
    private Int32 _MaxColItemsCount
    {
      get
      {
        return GetMaxItemCount(HintsColumn);
      }
    }

    private Int32 HintsRowCount
    {
      get
      {
        return HintsRow.Length;
      }
    }
    private Int32 HintsColumnCount
    {
      get
      {
        return HintsColumn.Length;
      }
    }

    public Hint[][] HintsRow
    { get; set; } = Array.Empty<Hint[]>();
    public Hint[][] HintsColumn
    { get; set; } = Array.Empty<Hint[]>();

    public Int32[][] Rows
    {
      get
      {
        return GetHints(HintsRow);
      }
    }
    public Int32[][] Cols
    {
      get
      {
        return GetHints(HintsColumn);
      }
    }

    public String Url
    { get; set; } = String.Empty;

    public NonogramSolveResult Result
    { get; set; } = new();

    private Int32[][] GetHints(Hint[][] list)
    {
      Int32[][] hints = new Int32[list.Length][];
      for (Int32 index = 0; index < list.Length; index++)
      {
        List<Int32> listHint = new List<Int32>();
        foreach (var hint in list[index])
        {
          listHint.Add(hint.Count);
        }
        hints[index] = listHint.ToArray();
      }

      return hints;
    }
    private Int32 GetMaxItemCount(Hint[][] hints)
    {
      Int32 max = 0;
      
      foreach (var hint in hints)
      {
        max = Math.Max(max, hint.Length);
      }

      return max;
    }

    public List<PuzzleColors> ListColors
    { get; set; } = [];

    public void Draw(Canvas canvas)
    {
      if (HintsRowCount != 0 && HintsColumnCount != 0)
      {
        _CellSize = Math.Min(canvas.ActualHeight / (_MaxColItemsCount + HintsRowCount), canvas.ActualWidth / (_MaxRowItemsCount + HintsColumnCount));
      }
      Double FontSize = _CellSize * 0.8;

      Action<Double, Double, SolidColorBrush> createRectangle = (left, top, brush) =>
      {
        Rectangle rect = new Rectangle();
        canvas.Children.Add(rect);

        rect.Stroke = brush;
        rect.Fill = brush;

        rect.Width = _CellSize;
        rect.Height = _CellSize;
        
        Canvas.SetLeft(rect, left);
        Canvas.SetTop(rect, top);
      };
      Action<Double, Double, String, SolidColorBrush> createText = (left, top, text, brush) =>
      {
        Label label = new Label();
        canvas.Children.Add(label);

        label.Content = text;
        label.FontSize = FontSize;
        label.Foreground = brush;
        label.Padding = new Thickness(0, 0, 0, 0);

        label.Measure(new Size(double.MaxValue, double.MaxValue));

        left -= label.DesiredSize.Width / 2;
        top -= label.DesiredSize.Height / 2;

        Canvas.SetLeft(label, left);
        Canvas.SetTop(label, top);
      };
      Action<Double, Double, Double, Double, Double, SolidColorBrush> createLine = (x1, y1, x2, y2, thickness, brush) =>
      {
        Line line = new();
        canvas.Children.Add(line);

        line.Stroke = brush;
        line.StrokeThickness = thickness;

        line.X1 = x1;
        line.X2 = x2;

        line.Y1 = y1;
        line.Y2 = y2;
      };

      Double currentX, currentY;

      currentX = _MaxRowItemsCount * _CellSize;
      for (Int32 col = 0; col < HintsColumnCount; col++)
      {
        Hint[] list = HintsColumn[col];
        currentY = (_MaxColItemsCount - list.Length) * _CellSize;

        foreach (Hint hint in list)
        {
          createRectangle(currentX, currentY, ListColors[hint.ColorId].ColorBrush);
          createText(currentX + _CellSize / 2, currentY + _CellSize / 2, hint.Count.ToString(), ListColors[1].ColorBrush);

          currentY += _CellSize;
        }

        currentX += _CellSize;
      }

      currentY = _MaxColItemsCount  * _CellSize;
      for (Int32 row = 0; row < HintsRowCount; row++)
      {
        Hint[] list = HintsRow[row];
        currentX = (_MaxRowItemsCount - list.Length) * _CellSize;

        foreach (Hint hint in list)
        {
          createRectangle(currentX, currentY, ListColors[hint.ColorId].ColorBrush);
          createText(currentX + _CellSize / 2, currentY + _CellSize / 2, hint.Count.ToString(), ListColors[1].ColorBrush);
          currentX += _CellSize;
        }

        currentY += _CellSize;
      }

      if (Result?.IsSolved == true)
      {
        currentX = _MaxRowItemsCount * _CellSize;
        currentY = _MaxColItemsCount * _CellSize;

        for (Int32 row = 0; row <= HintsRowCount; row++)
        {
          Double x2 = currentX + HintsColumnCount * _CellSize;
          Double y = currentY + row * _CellSize;
          
          SolidColorBrush brush = _BrushGrey;
          Double thickness = 1;

          if (row % 5 == 0)
          {
            brush = _BrushBlack;
            thickness = 2;
          }

          createLine(currentX, y, x2, y, thickness, brush);
        }
        for (Int32 col = 0; col <= HintsColumnCount; col++)
        {
          Double x = currentX + col * _CellSize;
          Double y2 = currentY + HintsRowCount * _CellSize;

          SolidColorBrush brush = _BrushGrey;
          Double thickness = 1;

          if (col % 5 == 0)
          {
            brush = _BrushBlack;
            thickness = 2;
          }

          createLine(x, currentY, x, y2, thickness, brush);
        }

        for (Int32 col = 0; col < HintsColumnCount; col++)
        {
          for (Int32 row = 0; row < HintsRowCount; row++)
          {
            if (Result.Result?[row, col] == 1)
            {
              Double x = currentX + col * _CellSize;
              Double y = currentY + row * _CellSize;
              createRectangle(x, y, _BrushBlack);
            }
          }
        }
      }
    }
  }
}
