using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Griddler_Solver
{
  internal class Definition
  {
    public static Int32 ColorBackground = 0;
    public static Definition DefinitionBackground = new Definition() { Color = ColorBackground, Count = 1 };

    public Int32 Color
    { get; set; }

    public Int32 Count
    { get; set; }

    public Boolean IsBackground
    {
      get
      {
        return (Color == ColorBackground);
      }
    }

    public override String ToString()
    {
      return $"[{Color}:{Count}]";
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

      if (Data.Count > 0)
      {
        if (Data[Data.Count - 1].Color == color)
        {
          Data.Add(Definition.DefinitionBackground);
        }
      }
      Data.Add(new Definition { Color = color,Count = count });
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
    private const Int32 cellSize = 50;
    private readonly SolidColorBrush BrushLime = new SolidColorBrush(Colors.Lime);

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

    private ListDefinition DefinitionListRows = new ListDefinition();
    private ListDefinition DefinitionListColumns = new ListDefinition();

    private Boolean ChangeMade
    { get; set; }
    private Definition[,] _Board = new Definition[0,0];

    public void AddSingleDefinitionRow(ListSingleDefinition list)
    {
      DefinitionListRows.Add(list);
    }
    public void AddSingleDefinitionColumn(ListSingleDefinition list)
    {
      DefinitionListColumns.Add(list);
    }

    private Rectangle CreateRectangle(Int32 left, Int32 top, SolidColorBrush brush)
    {
      Rectangle rect = new Rectangle();

      rect.Stroke = brush;
      rect.Fill = brush;

      rect.Width = cellSize;
      rect.Height = cellSize;
      Canvas.SetLeft(rect, left);
      Canvas.SetTop(rect, top);

      return rect;
    }

    public void Draw(Canvas canvas)
    {
      for (Int32 row = 0; row < CountRows; row++)
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
          else if (_Board[row, column].Color == Definition.ColorBackground)
          {
            brush = new SolidColorBrush(Colors.White);
          }
          else
          {
            brush = new SolidColorBrush(Colors.Black);
          }

          canvas.Children.Add(CreateRectangle(cellSize + column * cellSize, cellSize + row * cellSize, brush));
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
            if (cell != null && cell.Color != definition.Color)
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

            if (solved == false || cell == null || cell.Color != definition.Color)
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
