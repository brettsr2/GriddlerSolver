using System;
using System.Collections.Generic;
using System.Diagnostics;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

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
    private const Int32 cellSize = 15;
    private readonly SolidColorBrush BrushLime = new SolidColorBrush(Colors.Lime);

    private Int32 MaxRowItemsCount
    { get; set; } = 0;
    private Int32 MaxColItemsCount
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

    public List<SolidColorBrush> ListSolidColorBrush = new List<SolidColorBrush>();

    private ListDefinition DefinitionListRows = new ListDefinition();
    private ListDefinition DefinitionListColumns = new ListDefinition();

    private Boolean ChangeMade
    { get; set; }
    private Definition[,] _Board = new Definition[0,0];

    public void AddSingleDefinitionRow(ListSingleDefinition list)
    {
      MaxRowItemsCount = Math.Max(MaxRowItemsCount, list.Data.Count);
      DefinitionListRows.Add(list);
    }
    public void AddSingleDefinitionCol(ListSingleDefinition list)
    {
      MaxColItemsCount = Math.Max(MaxColItemsCount, list.Data.Count);
      DefinitionListColumns.Add(list);
    }

    public void Draw(Canvas canvas)
    {
      Double FontSize = Math.Min(canvas.ActualWidth, canvas.ActualHeight) / 40;

      Action<Double, Double, SolidColorBrush> createRectangle = (left, top, brush) =>
      {
        Rectangle rect = new Rectangle();
        canvas.Children.Add(rect);

        rect.Stroke = brush;
        rect.Fill = brush;

        rect.Width = cellSize;
        rect.Height = cellSize;
        
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

      Int32 currentX, currentY;

      currentX = MaxRowItemsCount * cellSize;
      for (Int32 col = 0; col < CountColumns; col++)
      {
        ListSingleDefinition list = DefinitionListColumns[col];
        currentY = (MaxColItemsCount - list.Data.Count) * cellSize;

        foreach (var definition in list.Data)
        {
          createRectangle(currentX, currentY, ListSolidColorBrush[definition.ColorId]);
          createText(currentX + cellSize / 2, currentY + cellSize / 2, definition.Count.ToString(), ListSolidColorBrush[1]);

          currentY += cellSize;
        }

        currentX += cellSize;
      }

      currentY = MaxColItemsCount  * cellSize;
      for (Int32 row = 0; row < CountRows; row++)
      {
        ListSingleDefinition list = DefinitionListRows[row];
        currentX = (MaxRowItemsCount - list.Data.Count) * cellSize;

        foreach (var definition in list.Data)
        {
          createRectangle(currentX, currentY, ListSolidColorBrush[definition.ColorId]);
          createText(currentX + cellSize / 2, currentY + cellSize / 2, definition.Count.ToString(), ListSolidColorBrush[1]);
          currentX += cellSize;
        }

        currentY += cellSize;
      }

      return;

      /*for (Int32 row = 0; row < CountRows; row++)
      {
        ListSingleDefinition list = DefinitionListRows[row];
        if (list.Solved)
        {
          canvas.Children.Add(CreateRectangle(0, cellSize + row * cellSize, BrushLime));
        }

        for (Int32 column = 0; column < CountColumns; column++)
        {
          if (row == 0)
          {
            list = DefinitionListColumns[column];
            if (list.Solved)
            {
              canvas.Children.Add(CreateRectangle(cellSize + column * cellSize, 0, BrushLime));
            }
          }

          SolidColorBrush brush;
          if (_Board[row, column] == null)
          {
            brush = new SolidColorBrush(Colors.LightGray);
          }
          else if (_Board[row, column].ColorId == Definition.ColorBackground)
          {
            brush = new SolidColorBrush(Colors.White);
          }
          else
          {
            brush = new SolidColorBrush(Colors.Black);
          }

          canvas.Children.Add(CreateRectangle(cellSize + column * cellSize, cellSize + row * cellSize, brush));
        }
      }*/
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
