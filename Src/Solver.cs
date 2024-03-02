using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Griddler_Solver
{
  class Solver
  {
    #region data
    public String Name
    { get; set; } = String.Empty;
    public String Url
    { get; set; } = String.Empty;

    public Board Board
    { get; set; } = new();
    #endregion // data

    #region drawing
    public const String TimeFormat = @"mm\:ss";

    private SolidColorBrush _BrushGrey = new(Colors.Gray);
    private SolidColorBrush _BrushBlack = new(Colors.Black);

    private Int32 _MaxHintsCountRow
    {
      get
      {
        return GetMaxItemCount(Board.HintsRow);
      }
    }
    private Int32 _MaxHintsCountColumn
    {
      get
      {
        return GetMaxItemCount(Board.HintsColumn);
      }
    }

    private Double _CellSize = 15;

    public List<PuzzleColors> ListColors
    { get; set; } = [];
    #endregion // drawing

    #region solving
    [JsonIgnore]
    public Config Config
    { get; set; } = new();
    private IProgress? _IProgress = null;
    #endregion // solving

    public Solver()
    {
    }
    public Solver(Hint[][] hintsRow, Hint[][] hintsColumn)
    {
      Board = new Board()
      {
        HintsRow = hintsRow,
        HintsColumn = hintsColumn
      };
    }

    public void Draw(Canvas canvas)
    {
      if (Board.HintsRowCount != 0 && Board.HintsColumnCount != 0)
      {
        _CellSize = Math.Min(canvas.ActualHeight / (_MaxHintsCountColumn + Board.HintsRowCount), canvas.ActualWidth / (_MaxHintsCountRow + Board.HintsColumnCount));
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
      for (Int32 col = 0; col < Board.HintsColumnCount; col++)
      {
        Hint[] list = Board.HintsColumn[col];
        currentY = (_MaxHintsCountColumn - list.Length) * _CellSize;

        foreach (Hint hint in list)
        {
          createRectangle(currentX, currentY, ListColors[hint.ColorId].ColorBrush);
          createText(currentX + _CellSize / 2, currentY + _CellSize / 2, hint.Count.ToString(), ListColors[1].ColorBrush);

          currentY += _CellSize;
        }

        currentX += _CellSize;
      }

      currentY = _MaxHintsCountColumn * _CellSize;
      for (Int32 row = 0; row < Board.HintsRowCount; row++)
      {
        Hint[] list = Board.HintsRow[row];
        currentX = (_MaxHintsCountRow - list.Length) * _CellSize;

        foreach (Hint hint in list)
        {
          createRectangle(currentX, currentY, ListColors[hint.ColorId].ColorBrush);
          createText(currentX + _CellSize / 2, currentY + _CellSize / 2, hint.Count.ToString(), ListColors[1].ColorBrush);
          currentX += _CellSize;
        }

        currentY += _CellSize;
      }

      // board itself
      currentX = _MaxHintsCountRow * _CellSize;
      currentY = _MaxHintsCountColumn * _CellSize;

      for (Int32 col = 0; col < Board.HintsColumnCount; col++)
      {
        for (Int32 row = 0; row < Board.HintsRowCount; row++)
        {
          CellValue value = Board[row, col];
          Double x = currentX + col * _CellSize;
          Double y = currentY + row * _CellSize;
          createRectangle(x, y, ListColors[(Int32)value].ColorBrush);
        }
      }

      for (Int32 row = 0; row <= Board.HintsRowCount; row++)
      {
        Double x2 = currentX + Board.HintsColumnCount * _CellSize;
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
      for (Int32 col = 0; col <= Board.HintsColumnCount; col++)
      {
        Double x = currentX + col * _CellSize;
        Double y2 = currentY + Board.HintsRowCount * _CellSize;

        SolidColorBrush brush = _BrushGrey;
        Double thickness = 1;

        if (col % 5 == 0)
        {
          brush = _BrushBlack;
          thickness = 2;
        }

        createLine(x, currentY, x, y2, thickness, brush);
      }
    }

    private Int32 GetMaxItemCount(Hint[][]? hints)
    {
      Int32 max = 0;

      foreach (var hint in hints!)
      {
        max = Math.Max(max, hint.Length);
      }

      return max;
    }

    private void PrintIterationStatistic(Int32 iteration, UInt64 generatedPermutations, TimeSpan globalElapsed, TimeSpan iterationElapsed)
    {
      Int32 unknownCount = 0, blankCount = 0, filledCount = 0;

      for (Int32 row = 0; row < Board.HintsRowCount; row++)
      {
        for (Int32 col = 0; col < Board.HintsColumnCount; col++)
        {
          if (Board[row, col] == CellValue.Unknown)
          {
            unknownCount++;
          }
          else if (Board[row, col] == CellValue.Background)
          {
            blankCount++;
          }
          else
          {
            filledCount++;
          }
        }
      }

      Int32 total = Board.HintsRowCount * Board.HintsColumnCount;
      Int32 percentUnknown = unknownCount * 100 / total;

      String remainingTime = "N/A";

      if (percentUnknown < 100)
      {
        Int32 percentDone = 100 - percentUnknown;
        Double percentDonePerSecond = globalElapsed.TotalSeconds / (100 - percentUnknown);
        Double remainingSeconds = percentDonePerSecond * 100 - globalElapsed.TotalSeconds;
        remainingTime = new TimeSpan(0, 0, (Int32)remainingSeconds).ToString(TimeFormat);
      }

      StringBuilder stringBuilder = new StringBuilder();
      stringBuilder.Append($"[{globalElapsed.ToString(TimeFormat)}]");
      stringBuilder.Append($"[{iteration}]");
      stringBuilder.Append($"[{iterationElapsed.ToString(TimeFormat)}]");
      stringBuilder.Append($" Unknown: {unknownCount} ({percentUnknown}%) Blank: {blankCount} Filled: {filledCount}");
      stringBuilder.Append($" Permutations: {generatedPermutations}");
      stringBuilder.Append($" remaining time: {remainingTime}");

      var memoryInfo = GC.GetGCMemoryInfo();
      Int64 memoryPercent = (memoryInfo.MemoryLoadBytes * 100) / memoryInfo.TotalAvailableMemoryBytes;
      stringBuilder.Append($" memory: {memoryPercent}%");

      _IProgress?.AddMessage(stringBuilder.ToString());
    }
    public void Solve(IProgress progress)
    {
      _IProgress = progress;
      _IProgress?.AddMessage($"Start Cells: {Board.HintsRowCount * Board.HintsColumnCount}");

      Board.Init();

      static Int32 CalculateScore(Hint[] hints)
      {
        return hints.Length + hints.Sum(hint => hint.Count * 2);
      }

      Config = new Config()
      {
        Name = Name,
      };

      List<SolverLine> listSolverLine = [];
      for (Int32 row = 0; row < Board.HintsRowCount; row++)
      {
        listSolverLine.Add(new SolverLine()
        {
          Config = Config,
          Index = row,
          IsRow = true,
          Score = CalculateScore(Board.HintsRow[row]),
          Board = Board,
        });
      }
      for (Int32 column = 0; column < Board.HintsColumnCount; column++)
      {
        listSolverLine.Add(new SolverLine()
        {
          Config = Config,
          Index = column,
          IsRow = false,
          Score = CalculateScore(Board.HintsColumn[column]),
          Board = Board,
        });
      }
      List<SolverLine> listSolverLineOrigin = new(listSolverLine);

      listSolverLine.Sort(delegate (SolverLine line1, SolverLine line2)
      {
        //return line1.MaxPermutationsCount.CompareTo(line2.MaxPermutationsCount);
        return -line1.Score.CompareTo(line2.Score);
      });

      Stopwatch stopWatchGlobal = Stopwatch.StartNew();
      Int32 iteration = 0;

      UInt64 permutationsLimit = 1000000;

      while (!Board.IsSolved)
      {
        if (Config.Break)
        {
          break;
        }

        Stopwatch stopWatchIteration = Stopwatch.StartNew();
        
        iteration++;
        UInt64 generatedPermutations = 0;

        DateTime dateTime = DateTime.Now;
        Int32 dateTimeOfIteration = 0;

        List<SolverLine> listSolverLineFiltered = new List<SolverLine>(listSolverLine.Count);
        foreach (SolverLine solverLine in listSolverLine)
        {
          if (!solverLine.IsSolved)
          {
            listSolverLineFiltered.Add(solverLine);
          }
        }

        listSolverLine = listSolverLineFiltered;

        Boolean changed = false;
        UInt64 permutationsMinLimit = UInt64.MaxValue;

        var options = new ParallelOptions { MaxDegreeOfParallelism = -1 };
        Parallel.ForEach(listSolverLine, options, solverLine =>
        {
          if (Config.Break)
          {
            return;
          }

          Thread.CurrentThread.Name = "SolverLine " + solverLine.ToString();

          UInt64 maxPermutationsCount = solverLine.MaxPermutationsCount;
          if (maxPermutationsCount > permutationsLimit)
          {
            permutationsMinLimit = Math.Min(permutationsMinLimit, maxPermutationsCount);
            Debug.WriteLine($"{solverLine} skipped, permutations limit {permutationsLimit}");
            return;
          }

          solverLine.Solve();
          generatedPermutations += solverLine.CurrentPermutationsCount;
          changed |= solverLine.Changed;

          if ((DateTime.Now - dateTime).TotalSeconds > 5)
          {
            PrintIterationStatistic(iteration, generatedPermutations, stopWatchGlobal.Elapsed, stopWatchIteration.Elapsed);

            dateTime = DateTime.Now;
            dateTimeOfIteration = iteration;
          }
        });

        Debug.WriteLine($"Iteration {iteration}");
        foreach (SolverLine solverLine in listSolverLine)
        {
          Debug.WriteLine(solverLine.ToString());
        }

        if (changed == false)
        {
          permutationsLimit = permutationsMinLimit + 1;
        }

        stopWatchIteration.Stop();
        if (dateTimeOfIteration != iteration || (DateTime.Now - dateTime).TotalSeconds > 1)
        {
          PrintIterationStatistic(iteration, generatedPermutations, stopWatchGlobal.Elapsed, stopWatchIteration.Elapsed);
        }
      }

      Board.Iterations = iteration;
      Board.TimeTaken = stopWatchGlobal.Elapsed;
    }
  }
}
