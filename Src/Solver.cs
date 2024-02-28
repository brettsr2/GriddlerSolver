using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Griddler_Solver
{
  internal class Solver
  {
    public class SolveResult
    {
      public bool IsSolved { get; set; }
      public CellValue[][]? Result { get; set; }
      public TimeSpan TimeTaken { get; set; }
      public int Iterations { get; set; }
    }

    private SolidColorBrush _BrushGrey = new(Colors.Gray);
    private SolidColorBrush _BrushBlack = new(Colors.Black);

    private Double _CellSize = 15;

    private CellValue[,] _Board;
    private readonly Hint[][] _HintsRow;
    private readonly Hint[][] _HintsColumn;
    private IProgress? _IProgress = null;

    private LineSolver _LineSolver;

    private Int32 _MaxHintsCountRow
    {
      get
      {
        return GetMaxItemCount(_HintsRow);
      }
    }
    private Int32 _MaxHintsCountColumn
    {
      get
      {
        return GetMaxItemCount(_HintsColumn);
      }
    }

    public Int32 HintsRowCount
    {
      get
      {
        return _HintsRow.Length;
      }
    }
    public Int32 HintsColumnCount
    {
      get
      {
        return _HintsColumn.Length;
      }
    }

    public String Url
    { get; set; } = String.Empty;
    public String Name
    { get; set; } = String.Empty;

    public SolveResult Result
    { get; set; } = new();

    public Solver()
    {
    }

    public Solver(Hint[][] hintsRow, Hint[][] hintsColumn)
    {
      _HintsRow = hintsRow;
      _HintsColumn = hintsColumn;

      _Board = new CellValue[HintsRowCount, HintsColumnCount];
      for (int row = 0; row < HintsRowCount; row++)
      {
        for (int col = 0; col < HintsColumnCount; col++)
        {
          _Board[row, col] = CellValue.Unknown;
        }
      }


      _LineSolver = new LineSolver();
    }

    public void Solve(IProgress progress)
    {
      _IProgress = progress;

      _IProgress?.AddMessage("Start");

      Boolean hasChanged = true;
      Int32 iteration = 0;

      Stopwatch stopWatchGlobal = new();
      stopWatchGlobal.Start();

      while (hasChanged)
      {
        Stopwatch stopWatchIteration = new();
        stopWatchIteration.Start();

        iteration++;
        UInt64 generatedPermutations = 0;

        hasChanged = false;

        for (Int32 row = 0; row < HintsRowCount; row++)
        {
          var currentRow = GetRow(row);
          var updatedRow = _LineSolver.Solve(GetRow(row), _HintsRow[row]);
          generatedPermutations += _LineSolver.GeneratedPermutations;

          bool hasLineChanged = !currentRow.SequenceEqual(updatedRow);

          if (hasLineChanged)
          {
            ReplaceRow(row, updatedRow);
            hasChanged = true;
          }
        }

        for (Int32 col = 0; col < HintsColumnCount; col++)
        {
          var currentColumn = GetColumn(col);
          var updatedColumn = _LineSolver.Solve(GetColumn(col), _HintsColumn[col]);
          generatedPermutations += _LineSolver.GeneratedPermutations;

          bool hasLineChanged = !currentColumn.SequenceEqual(updatedColumn);

          if (hasLineChanged)
          {
            ReplaceColumn(col, updatedColumn);
            hasChanged = true;
          }
        }

        stopWatchIteration.Stop();
        PrintIterationStatistic(iteration, generatedPermutations, stopWatchGlobal.Elapsed, stopWatchIteration.Elapsed);
      }

      stopWatchGlobal.Stop();
      Result = new SolveResult
      {
        IsSolved = IsSolved(),
        Result = Convert(),
        Iterations = iteration,
        TimeTaken = stopWatchGlobal.Elapsed,
      };
    }

    private void PrintIterationStatistic(Int32 iteration, UInt64 generatedPermutations, TimeSpan globalElapsed, TimeSpan iterationElapsed)
    {
      Int32 unknownCount = 0, blankCount = 0, filledCount = 0;

      for (Int32 row = 0; row < HintsRowCount; row++)
      {
        for (Int32 col = 0; col < HintsColumnCount; col++)
        {
          if (_Board[row, col] == CellValue.Unknown)
          {
            unknownCount++;
          }
          else if (_Board[row, col] == CellValue.Blank)
          {
            blankCount++;
          }
          else if (_Board[row, col] == CellValue.Filled)
          {
            filledCount++;
          }
        }
      }

      Int32 total = HintsRowCount * HintsColumnCount;
      Int32 percentUnknown = unknownCount * 100 / total;

      const String timeFormat = @"mm\:ss";
      String remainingTime = "N/A";

      if (percentUnknown < 100)
      {
        Int32 percentDone = 100 - percentUnknown;
        Double percentDonePerSecond = globalElapsed.TotalSeconds / (100 - percentUnknown);
        Double remainingSeconds = percentDonePerSecond * 100 - globalElapsed.TotalSeconds;
        remainingTime = new TimeSpan(0, 0, (Int32)remainingSeconds).ToString(timeFormat);
      }

      StringBuilder stringBuilder = new StringBuilder();
      stringBuilder.Append($"[{globalElapsed.ToString(timeFormat)}]");
      stringBuilder.Append($"[{iteration}]");
      stringBuilder.Append($"[{iterationElapsed.ToString(timeFormat)}]");
      stringBuilder.Append($" Cells: {total} Unknown: {unknownCount} ({percentUnknown}%) Blank: {blankCount} Filled: {filledCount}");
      stringBuilder.Append($" Permutations: {generatedPermutations}");
      stringBuilder.Append($" remaining time: {remainingTime}");

      _IProgress?.AddMessage(stringBuilder.ToString());
    }

    private CellValue[][] Convert()
    {
      CellValue[][] board = new CellValue[HintsRowCount][];

      for (Int32 row = 0; row < HintsRowCount; row++)
      {
        board[row] = GetRow(row);
        //board[row] = new CellValue[HintsColumnCount];
        //for (Int32 column = 0; column < HintsColumnCount; column++)
        {
          //board[row][column] = _Board[row, column];
        }
      }

      return board;
    }

    private Boolean IsSolved()
    {
      for (Int32 row = 0; row < HintsRowCount; row++)
      {
        for (Int32 col = 0; col < HintsColumnCount; col++)
        {
          if (_Board[row, col] == CellValue.Unknown)
          {
            return false;
          }
        }
      }

      return true;
    }
    private CellValue[] GetColumn(Int32 colIdx)
    {
      CellValue[] column = new CellValue[HintsRowCount];

      for (int row = 0; row < column.Length; row++)
      {
        column[row] = _Board[row, colIdx];
      }

      return column;
    }

    private CellValue[] GetRow(Int32 indexRow)
    {
      CellValue[] row = new CellValue[HintsColumnCount];

      for (Int32 column = 0; column < row.Length; column++)
      {
        row[column] = _Board[indexRow, column];
      }

      return row;
    }

    private void ReplaceColumn(Int32 indexColumn, CellValue[] column)
    {
      for (Int32 row = 0; row < column.Length; row++)
      {
        _Board[row, indexColumn] = column[row];
      }
    }

    private void ReplaceRow(Int32 indexRow, CellValue[] row)
    {
      for (Int32 column = 0; column < row.Length; column++)
      {
        _Board[indexRow, column] = row[column];
      }
    }

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
      //return;

      if (HintsRowCount != 0 && HintsColumnCount != 0)
      {
        _CellSize = Math.Min(canvas.ActualHeight / (_MaxHintsCountColumn + HintsRowCount), canvas.ActualWidth / (_MaxHintsCountRow + HintsColumnCount));
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

      currentX = _MaxHintsCountRow * _CellSize;
      for (Int32 col = 0; col < HintsColumnCount; col++)
      {
        Hint[] list = _HintsColumn[col];
        currentY = (_MaxHintsCountColumn - list.Length) * _CellSize;

        foreach (Hint hint in list)
        {
          createRectangle(currentX, currentY, ListColors[hint.ColorId].ColorBrush);
          createText(currentX + _CellSize / 2, currentY + _CellSize / 2, hint.Count.ToString(), ListColors[1].ColorBrush);

          currentY += _CellSize;
        }

        currentX += _CellSize;
      }

      currentY = _MaxHintsCountColumn  * _CellSize;
      for (Int32 row = 0; row < HintsRowCount; row++)
      {
        Hint[] list = _HintsRow[row];
        currentX = (_MaxHintsCountRow - list.Length) * _CellSize;

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
        currentX = _MaxHintsCountRow * _CellSize;
        currentY = _MaxHintsCountColumn * _CellSize;

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
            if (Result.Result?[row][col] == CellValue.Filled)
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
