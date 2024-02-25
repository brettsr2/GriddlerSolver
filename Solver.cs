using System;
using System.Collections.Generic;
using System.Diagnostics;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

using NonogramSolver;

namespace Griddler_Solver
{
  internal class Definition
  {
    public static Int32 ColorBackground = 0;
    public static Definition DefinitionBackground = new Definition() { ColorId = ColorBackground, Count = 1 };

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
  internal class ListSingleDefinition
  {
    public List<Definition> Data
    { get; private set; } = new List<Definition>();

    public Boolean Solved
    { get; set; }

    public void AddDefinition(Int32 color, Int32 count)
    {
      Debug.Assert(color != Definition.ColorBackground);
      Data.Add(new Definition { ColorId = color, Count = count });
    }

    public override String ToString()
    {
      return $"[{Data.Count}:{Solved}]";
    }
  }

  internal class ListDefinition : List<ListSingleDefinition>
  {
  }

  internal class Solver
  {
    private SolidColorBrush _BrushGrey = new(Colors.Gray);
    private SolidColorBrush _BrushBlack = new(Colors.Black);

    private Double _CellSize = 15;

    private Int32 _MaxRowItemsCount
    { get; set; } = 0;
    private Int32 _MaxColItemsCount
    { get; set; } = 0;

    private Int32 CountRows
    {
      get
      {
        return DefinitionListRows.Count;
      }
    }
    private Int32 CountColumns
    {
      get
      {
        return DefinitionListColumns.Count;
      }
    }

    public Int32[][] Rows
    {
      get
      {
        return GetHints(DefinitionListRows);
      }
    }
    public Int32[][] Cols
    {
      get
      {
        return GetHints(DefinitionListColumns);
      }
    }

    public String Url
    { get; set; } = String.Empty;

    public NonogramSolveResult? Result
    { get; set; } = null;

    private Int32[][] GetHints(ListDefinition list)
    {
      Int32[][] hints = new Int32[list.Count][];
      for (Int32 index = 0; index < list.Count; index++)
      {
        List<Int32> listHint = new List<Int32>();
        foreach (var hint in list[index].Data)
        {
          listHint.Add(hint.Count);
        }
        hints[index] = listHint.ToArray();
      }

      return hints;
    }
    public List<SolidColorBrush> ListSolidColorBrush = new List<SolidColorBrush>();

    private ListDefinition DefinitionListRows = new ListDefinition();
    private ListDefinition DefinitionListColumns = new ListDefinition();

    private Boolean ChangeMade
    { get; set; }
    private Definition[,] _Board = new Definition[0,0];

    public void AddSingleDefinitionRow(ListSingleDefinition list)
    {
      _MaxRowItemsCount = Math.Max(_MaxRowItemsCount, list.Data.Count);
      DefinitionListRows.Add(list);
    }
    public void AddSingleDefinitionCol(ListSingleDefinition list)
    {
      _MaxColItemsCount = Math.Max(_MaxColItemsCount, list.Data.Count);
      DefinitionListColumns.Add(list);
    }

    public void Draw(Canvas canvas)
    {
      if (CountRows != 0 && CountColumns != 0)
      {
        _CellSize = Math.Min(canvas.ActualHeight / (_MaxColItemsCount + CountRows), canvas.ActualWidth / (_MaxRowItemsCount + CountColumns));
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
      for (Int32 col = 0; col < CountColumns; col++)
      {
        ListSingleDefinition list = DefinitionListColumns[col];
        currentY = (_MaxColItemsCount - list.Data.Count) * _CellSize;

        foreach (var definition in list.Data)
        {
          createRectangle(currentX, currentY, ListSolidColorBrush[definition.ColorId]);
          createText(currentX + _CellSize / 2, currentY + _CellSize / 2, definition.Count.ToString(), ListSolidColorBrush[1]);

          currentY += _CellSize;
        }

        currentX += _CellSize;
      }

      currentY = _MaxColItemsCount  * _CellSize;
      for (Int32 row = 0; row < CountRows; row++)
      {
        ListSingleDefinition list = DefinitionListRows[row];
        currentX = (_MaxRowItemsCount - list.Data.Count) * _CellSize;

        foreach (var definition in list.Data)
        {
          createRectangle(currentX, currentY, ListSolidColorBrush[definition.ColorId]);
          createText(currentX + _CellSize / 2, currentY + _CellSize / 2, definition.Count.ToString(), ListSolidColorBrush[1]);
          currentX += _CellSize;
        }

        currentY += _CellSize;
      }

      if (Result?.IsSolved == true)
      {
        currentX = _MaxRowItemsCount * _CellSize;
        currentY = _MaxColItemsCount * _CellSize;

        for (Int32 row = 0; row <= CountRows; row++)
        {
          Double x2 = currentX + CountColumns * _CellSize;
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
        for (Int32 col = 0; col <= CountColumns; col++)
        {
          Double x = currentX + col * _CellSize;
          Double y2 = currentY + CountRows * _CellSize;

          SolidColorBrush brush = _BrushGrey;
          Double thickness = 1;

          if (col % 5 == 0)
          {
            brush = _BrushBlack;
            thickness = 2;
          }

          createLine(x, currentY, x, y2, thickness, brush);
        }

        for (Int32 col = 0; col < CountColumns; col++)
        {
          for (Int32 row = 0; row < CountRows; row++)
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

    private Definition GetCell(Boolean isRow, Int32 indexFirst, Int32 indexSecond)
    {
      if (isRow)
      {
        return _Board[indexFirst, indexSecond];
      }
      else
      {
        return _Board[indexSecond, indexFirst];
      }
    }
    private void SetCell(Boolean isRow, Int32 indexFirst, Int32 indexSecond, Definition definition)
    {
      if (isRow)
      {
        _Board[indexFirst, indexSecond] = definition;
      }
      else
      {
        _Board[indexSecond, indexFirst] = definition;
      }
      ChangeMade = true;
    }

    private void CheckDefinitionList(ListDefinition listDefinition, Boolean isRows)
    {
      Int32 countFirst = isRows ? CountRows : CountColumns;
      Int32 countSecond = isRows ? CountColumns : CountRows;

      // check out if SingleDefinitionList fits whole row/column
      for (Int32 indexFirst = 0; indexFirst < countFirst; indexFirst++)
      {
        ListSingleDefinition list = listDefinition[indexFirst];
        if (list.Solved)
        {
          continue;
        }

        // empty row/column
        if (list.Data.Count == 0)
        {
          for (Int32 indexSecond = 0; indexSecond < countSecond; indexSecond++)
          {
            SetCell(isRows, indexFirst, indexSecond, Definition.DefinitionBackground);
          }

          list.Solved = true;
          continue;
        }

        Int32 currentSecond = 0;
        Boolean doNotFit = false;

        foreach (Definition definition in list.Data)
        {
          if (doNotFit)
          {
            break;
          }

          for (Int32 indexSecond = 0; indexSecond < definition.Count; indexSecond++)
          {
            Definition cell = GetCell(isRows, indexFirst, currentSecond);
            if (cell != null && cell.ColorId != definition.ColorId)
            {
              doNotFit = true;
              break;
            }

            currentSecond++;
          }
        }

        if (currentSecond == countSecond && doNotFit == false)
        {
          currentSecond = 0;
          foreach (Definition definition in list.Data)
          {
            for (Int32 indexSecond = 0; indexSecond < definition.Count; indexSecond++)
            {
              SetCell(isRows, indexFirst, currentSecond, definition);
              currentSecond++;
            }
          }

          list.Solved = true;
        }
      }

      // check if row/column is solved and fill backgrounds instead null
      for (Int32 indexFirst = 0; indexFirst < countFirst; indexFirst++)
      {
        ListSingleDefinition list = listDefinition[indexFirst];
        if (list.Solved)
        {
          continue;
        }

        Int32 currentSecond = 0;
        Boolean solved = true;

        foreach (Definition definition in list.Data)
        {
          if (solved == false)
          {
            break;
          }

          if (definition.IsBackground)
          {
            continue;
          }

          for (Int32 indexSecond = 0; indexSecond < definition.Count; indexSecond++)
          {
            if (solved == false || currentSecond >= countSecond)
            {
              solved = false;
              break;
            }

            Definition cell = GetCell(isRows, indexFirst, currentSecond);

            // find first not null cell
            while (cell == null || cell.IsBackground)
            {
              currentSecond++;
              if (currentSecond >= countSecond)
              {
                solved = false;
                break;
              }

              cell = GetCell(isRows, indexFirst, currentSecond);
              continue;
            }

            if (solved == false || cell == null || cell.ColorId != definition.ColorId)
            {
              solved = false;
              continue;
            }

            currentSecond++;
          }
        }

        if (solved)
        {
          for (Int32 indexSecond = 0; indexSecond < countSecond; indexSecond++)
          {
            Definition cell = GetCell(isRows, indexFirst, indexSecond);
            if (cell == null)
            {
              SetCell(isRows, indexFirst, indexSecond, Definition.DefinitionBackground);
            }
          }
          
          list.Solved = true;
        }
      }
    }

    public Boolean Solve()
    {
      _Board = new Definition[CountRows, CountColumns];

      ChangeMade = true;
      while (ChangeMade)
      {
        ChangeMade = false;

        CheckDefinitionList(DefinitionListRows, true);
        CheckDefinitionList(DefinitionListColumns, false);
      }

      return true;
    }
  }
}
