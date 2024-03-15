using System;
using System.Collections.Generic;
using System.Data.Common;
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

    private List<StaticAnalysis> _ListStaticAnalysis = [];
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
      Board.Init();
    }

    public void Clear()
    {
      Board.Clear();
      _ListStaticAnalysis = [];
    }
    public void Draw(Canvas canvas)
    {
      if (Board.RowCount != 0 && Board.ColumnCount != 0)
      {
        _CellSize = Math.Min(canvas.ActualHeight / (_MaxHintsCountColumn + Board.RowCount), canvas.ActualWidth / (_MaxHintsCountRow + Board.ColumnCount));
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
      Action<Double, Double, SolidColorBrush> createCross = (left, top, brush) =>
      {
        createLine(left, top, left + _CellSize, top + _CellSize, 1, brush);
        createLine(left + _CellSize, top, left, top + _CellSize, 1, brush);
      };

      Double currentX, currentY;

      currentX = _MaxHintsCountRow * _CellSize;
      for (Int32 col = 0; col < Board.ColumnCount; col++)
      {
        Hint[] list = Board.HintsColumn[col];
        currentY = (_MaxHintsCountColumn - list.Length) * _CellSize;

        foreach (Hint hint in list)
        {
          createRectangle(currentX, currentY, ListColors[hint.ColorId].ColorBrush);
          createText(currentX + _CellSize / 2, currentY + _CellSize / 2, hint.Count.ToString(), ListColors[1].ColorBrush);
          if (hint.IsSolved)
          {
            createCross(currentX, currentY, _BrushGrey);
          }

          currentY += _CellSize;
        }

        currentX += _CellSize;
      }

      currentY = _MaxHintsCountColumn * _CellSize;
      for (Int32 row = 0; row < Board.RowCount; row++)
      {
        Hint[] list = Board.HintsRow[row];
        currentX = (_MaxHintsCountRow - list.Length) * _CellSize;

        foreach (Hint hint in list)
        {
          createRectangle(currentX, currentY, ListColors[hint.ColorId].ColorBrush);
          createText(currentX + _CellSize / 2, currentY + _CellSize / 2, hint.Count.ToString(), ListColors[1].ColorBrush);
          if (hint.IsSolved)
          {
            createCross(currentX, currentY, _BrushGrey);
          }

          currentX += _CellSize;
        }

        currentY += _CellSize;
      }

      currentX = _MaxHintsCountRow * _CellSize;
      currentY = _MaxHintsCountColumn * _CellSize;

      // board itself
      for (Int32 col = 0; col < Board.ColumnCount; col++)
      {
        for (Int32 row = 0; row < Board.RowCount; row++)
        {
          CellValue value = Board[row, col];
          Double x = currentX + col * _CellSize;
          Double y = currentY + row * _CellSize;
          createRectangle(x, y, ListColors[(Int32)value].ColorBrush);
        }
      }
      // grid
      for (Int32 row = 0; row <= Board.RowCount; row++)
      {
        Double x2 = currentX + Board.ColumnCount * _CellSize;
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
      for (Int32 col = 0; col <= Board.ColumnCount; col++)
      {
        Double x = currentX + col * _CellSize;
        Double y2 = currentY + Board.RowCount * _CellSize;

        SolidColorBrush brush = _BrushGrey;
        Double thickness = 1;

        if (col % 5 == 0)
        {
          brush = _BrushBlack;
          thickness = 2;
        }

        createLine(x, currentY, x, y2, thickness, brush);
      }
      // static analysis
      foreach (var StaticAnalysis in _ListStaticAnalysis)
      {
        Double x = currentX + StaticAnalysis.Column * _CellSize;
        Double y = currentY + StaticAnalysis.Row * _CellSize;

        createCross(x, y, _BrushGrey);
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

    private void PrintIterationStatistic(Int32 iteration, UInt64 currentPermutationsCount, UInt64 permutationsLimit, TimeSpan globalElapsed, TimeSpan iterationElapsed)
    {
      Int32 unknownCount = 0, blankCount = 0, filledCount = 0;

      for (Int32 row = 0; row < Board.RowCount; row++)
      {
        for (Int32 col = 0; col < Board.ColumnCount; col++)
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

      Int32 total = Board.RowCount * Board.ColumnCount;
      Int32 percentUnknown = unknownCount * 100 / total;

      StringBuilder stringBuilder = new StringBuilder();
      stringBuilder.Append($"[{globalElapsed.ToString(TimeFormat)}]");
      stringBuilder.Append($"[{iteration}]");
      stringBuilder.Append($"[{iterationElapsed.ToString(TimeFormat)}]");
      stringBuilder.Append($" Unknown: {unknownCount} ({percentUnknown}%) Blank: {blankCount} Filled: {filledCount}");
      stringBuilder.Append($" Permutations: {currentPermutationsCount}");
      stringBuilder.Append($" Permutations limit: {permutationsLimit}");

      var memoryInfo = GC.GetGCMemoryInfo();
      Int64 memoryPercent = (memoryInfo.MemoryLoadBytes * 100) / memoryInfo.TotalAvailableMemoryBytes;
      stringBuilder.Append($" memory: {memoryPercent}%");

      Config.Progress?.AddMessage(stringBuilder.ToString());
    }

    public void Solve(Config config)
    {
      Config = config;
      Config.Progress?.AddMessage($"Start");
      Config.Progress?.AddMessage($"Cells to solve: {Board.RowCount * Board.ColumnCount}");

      if (Board.RowCount == 0 || Board.ColumnCount == 0)
      {
        Config.Progress?.AddMessage($"Empty board. Nothing to solve.");
        return;
      }

      static Int32 CalculateScore(Hint[] hints)
      {
        return hints.Length + hints.Sum(hint => hint.Count * 2);
      }

      List<SolverLine> listSolverLine = [];
      for (Int32 row = 0; row < Board.RowCount; row++)
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
      for (Int32 column = 0; column < Board.ColumnCount; column++)
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

      if (Config.ScoreSortingEnabled)
      {
        listSolverLine.Sort(delegate (SolverLine line1, SolverLine line2)
        {
          //return line1.MaxPermutationsCount.CompareTo(line2.MaxPermutationsCount);
          return -line1.Score.CompareTo(line2.Score);
        });
      }

      Stopwatch stopWatchGlobal = Stopwatch.StartNew();
      Int32 iteration = 0;

      UInt64 permutationsLimit = Config.PermutationsLimit ? 1000000 : UInt32.MaxValue;

      StaticAnalysis();

      while (!Board.IsSolved)
      {
        if (Config.Break)
        {
          break;
        }

        Stopwatch stopWatchIteration = Stopwatch.StartNew();
        
        iteration++;
        UInt64 currentPermutationsCount = 0;

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

        Config.TicksCurrentIteration = DateTime.Now.Ticks;

        if (Config.ThreadsEnabled)
        {
          var options = new ParallelOptions { MaxDegreeOfParallelism = Config.MultithreadEnabled ? -1 : 1 };
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
              Config.Progress?.AddDebugMessage($"{solverLine} skipped, permutations limit {permutationsLimit}");
              return;
            }

            if (solverLine.IterationOfGenerating == 0)
            {
              solverLine.IterationOfGenerating = iteration;
            }

            solverLine.Solve();
            currentPermutationsCount += solverLine.CurrentPermutationsCount;
            changed |= solverLine.Changed;
          });
        }

        StaticAnalysis();

        Config.Progress?.AddDebugMessage($"Iteration {iteration}");
        foreach (SolverLine solverLine in listSolverLine)
        {
          Config.Progress?.AddDebugMessage(solverLine.ToString());
        }

        if (changed == false)
        {
          permutationsLimit = permutationsMinLimit + 1;
        }

        stopWatchIteration.Stop();
        PrintIterationStatistic(iteration, currentPermutationsCount, permutationsLimit, stopWatchGlobal.Elapsed, stopWatchIteration.Elapsed);

        if (Config.StepMode)
        {
          break;
        }
      }

      foreach (SolverLine solverLine in listSolverLineOrigin)
      {
        Config.Progress?.AddMessage(solverLine.ToString());
      }

      Config.Break = true;

      Board.Iterations = iteration;
      Board.TimeTaken = stopWatchGlobal.Elapsed;
    }
    private void StaticAnalysis()
    {
      if (Config.StaticAnalysisEnabled == false)
      {
        return;
      }

      for (Int32 indexRow = 0; indexRow < Board.RowCount; indexRow++)
      {
        if (Config.Break)
        {
          break;
        }
        StaticAnalysisCheckLine(true, indexRow);
      }
      for (Int32 indexColumn = 0; indexColumn < Board.ColumnCount; indexColumn++)
      {
        if (Config.Break)
        {
          break;
        }
        StaticAnalysisCheckLine(false, indexColumn);
      }
    }

    private void StaticAnalysisCheckLine(Boolean isRow, Int32 index)
    {
      CellValue[] line = isRow ? Board.GetRow(index) : Board.GetColumn(index);
      Hint[] hints = isRow ? Board.HintsRow[index] : Board.HintsColumn[index];
      StaticAnalysisCheckLine(isRow, index, line, hints, false);

      // refresh data
      line = line.ToList().Reverse<CellValue>().ToArray();
      hints = hints.ToList().Reverse<Hint>().ToArray();
      StaticAnalysisCheckLine(isRow, index, line, hints, true);
    }
    private void StaticAnalysisCheckLine(Boolean isRow, Int32 index, CellValue[] line, Hint[] hints, Boolean reverted)
    {
      Boolean allHintsSolved = hints.All(hint => hint.IsSolved);
      if (allHintsSolved)
      {
        return;
      }

      // find first available cell on line
      Int32 findFirstOrNext(Int32 indexStart)
      {
        for (Int32 indexLine = indexStart; indexLine < line.Length; indexLine++)
        {
          if (line[indexLine] == CellValue.Background)
          {
            continue;
          }
          else
          {
            return indexLine;
          }
        }

        throw new NotSupportedException();
      }
      CellValue getCellValue(Int32 index)
      {
        return index >= line.Length ? CellValue.OutOfBorder : line[index];
      }
      void createStaticAnalysis(Boolean setColor, CellValue[] line, Int32 indexOnLine)
      {
        Int32 Row = isRow ? index : indexOnLine;
        if (isRow == false && reverted)
        {
          Row = Board.RowCount - 1 - Row;
        }
        Int32 Column = isRow ? indexOnLine : index;
        if (isRow == true && reverted)
        {
          Column = Board.ColumnCount - 1 - Column;
        }

        CellValue cellValue;
        StaticAnalysisType type;
        if (setColor)
        {
          cellValue = CellValue.Color;
          type = StaticAnalysisType.SolvedColor;
        }
        else
        {
          cellValue = CellValue.Background;
          type = StaticAnalysisType.SolvedBackground;
        }

        line[indexOnLine] = cellValue;
        Board[Row, Column] = cellValue;

        _ListStaticAnalysis.Add(new StaticAnalysis()
        {
          IsRow = isRow,
          Row = Row,
          Column = Column,
          Type = type
        });
      }

      Int32 indexOnLine = findFirstOrNext(0);

      Boolean itFits = true;
      for (Int32 indexHint = 0; indexHint < hints.Length; indexHint++)
      {
        Hint hint = hints[indexHint];
        if (!hint.IsSolved)
        {
          Boolean startsWithUnknown = line[indexOnLine] == CellValue.Unknown;
          if (startsWithUnknown)
          {
            // check unknown cell before solved hint
            for (Int32 inHintCounter = 0; inHintCounter < hint.Count; inHintCounter++)
            {
              indexOnLine++;
              if (getCellValue(indexOnLine) != CellValue.Color)
              {
                itFits = false;
                break;
              }
            }

            if (itFits)
            {
              indexOnLine++;
              if (getCellValue(indexOnLine) == CellValue.Background)
              {
                createStaticAnalysis(false, line, indexOnLine - hint.Count - 1);
                hint.IsSolved = true;
              }
            }
          }
          else
          {
            Debug.Assert(line[indexOnLine] == CellValue.Color);
            // check continuos hint and unknown cell after solved hint
            for (Int32 inHintCounter = 1; inHintCounter < hint.Count; inHintCounter++)
            {
              indexOnLine++;
              if (getCellValue(indexOnLine) == CellValue.Unknown)
              {
                createStaticAnalysis(true, line, indexOnLine);
              }
              else if (getCellValue(indexOnLine) != CellValue.Color)
              {
                itFits = false;
                break;
              }
            }
            if (itFits)
            {
              indexOnLine++;
              if (getCellValue(indexOnLine) == CellValue.Unknown)
              {
                createStaticAnalysis(false, line, indexOnLine);
              }
              hint.IsSolved = true;
            }
          }
        }
        else
        {
          indexOnLine += hint.Count;
        }

        if (Config.Break)
        {
          break;
        }
        if (itFits == false)
        {
          break;
        }
        if (indexOnLine == line.Length)
        {
          break;
        }

        indexOnLine = findFirstOrNext(indexOnLine);
      }

      allHintsSolved = hints.All(hint => hint.IsSolved);
      if (allHintsSolved)
      {
        for (int indexCell = 0; indexCell < line.Length; indexCell++)
        {
          if (line[indexCell] == CellValue.Unknown)
          {
            createStaticAnalysis(false, line, indexCell);
          }
        }
      }
    }
  }
}
