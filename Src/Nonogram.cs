using System;
using System.Diagnostics;
using System.Linq;

namespace Griddler_Solver
{
  internal class Nonogram
  {
    public class NonogramSolveResult
    {
      public bool IsSolved { get; set; }
      public Int32[][]? Result { get; set; }
      public TimeSpan TimeTaken { get; set; }
      public int Iterations { get; set; }
    }

    private readonly int[][] rowHints;
    private readonly int[][] columnHints;
    private readonly IProgress? logger;
    private readonly CellValue[,] map;
    private readonly LineSolver lineSolver;
    private readonly int width;
    private readonly int height;

    public Nonogram(int[][] rowHints, int[][] columnHints, IProgress? logger = null)
    {
      this.rowHints = rowHints;
      this.columnHints = columnHints;
      this.logger = logger;
      this.width = columnHints.Length;
      this.height = rowHints.Length;
      this.lineSolver = new LineSolver();

      map = GenerateEmptyMap();
    }

    private CellValue[,] GenerateEmptyMap()
    {
      var map = new CellValue[height, width];

      for (int row = 0; row < height; row++)
      {
        for (int col = 0; col < width; col++)
        {
          map[row, col] = CellValue.Unknown;
        }
      }

      return map;
    }

    public NonogramSolveResult Solve()
    {
      bool hasChanged = true;
      int iteration = 0;

      Stopwatch stopWatchGlobal = new Stopwatch();
      stopWatchGlobal.Start();

      while (hasChanged)
      {
        Stopwatch stopWatchIteration = new Stopwatch();
        stopWatchIteration.Start();

        iteration++;
        logger?.AddMessage($"Iteration {iteration} started");

        hasChanged = false;

        for (int row = 0; row < height; row++)
        {
          var currentRow = GetRow(row);
          var updatedRow = lineSolver.Solve(GetRow(row), rowHints[row]);

          bool hasLineChanged = !currentRow.SequenceEqual(updatedRow);

          if (hasLineChanged)
          {
            ReplaceRow(row, updatedRow);
            hasChanged = true;
          }
        }

        for (int col = 0; col < width; col++)
        {
          var currentColumn = GetColumn(col);
          var updatedColumn = lineSolver.Solve(GetColumn(col), columnHints[col]);

          bool hasLineChanged = !currentColumn.SequenceEqual(updatedColumn);

          if (hasLineChanged)
          {
            ReplaceColumn(col, updatedColumn);
            hasChanged = true;
          }
        }

        stopWatchIteration.Stop();
        logger?.AddMessage($"Iteration {iteration} ended");
        PrintIterationStatistic(stopWatchGlobal.Elapsed, stopWatchIteration.Elapsed);
      }

      stopWatchGlobal.Stop();

      return new NonogramSolveResult
      {
        IsSolved = IsSolved(),
        Result = Convert(),
        Iterations = iteration,
        TimeTaken = stopWatchGlobal.Elapsed,
      };
    }

    private void PrintIterationStatistic(TimeSpan globalElapsed, TimeSpan iterationElapsed)
    {
      Int32 unknownCount = 0, blankCount = 0, filledCount = 0;

      for (int row = 0; row < height; row++)
      {
        for (int col = 0; col < width; col++)
        {
          if (map[row, col] == CellValue.Unknown)
          {
            unknownCount++;
          }
          else if (map[row, col] == CellValue.Blank)
          {
            blankCount++;
          }
          else if (map[row, col] == CellValue.Filled)
          {
            filledCount++;
          }
        }
      }

      Int32 total = height * width;
      Int32 percentUnknown = unknownCount * 100 / total;

      logger?.AddMessage($"[{globalElapsed.ToString(@"mm\:ss")}]: Cells: {total} Unknown: {unknownCount} ({percentUnknown}%), Blank: {blankCount}, Filled {filledCount}, Time: {iterationElapsed.TotalSeconds} seconds");
    }

    private bool IsSolved()
    {
      for (int row = 0; row < height; row++)
      {
        if (!LineSolver.IsLineLogicallyComplete(GetRow(row), rowHints[row]))
        {
          return false;
        }
      }

      for (int col = 0; col < width; col++)
      {
        if (!LineSolver.IsLineLogicallyComplete(GetColumn(col), columnHints[col]))
        {
          return false;
        }
      }

      return true;
    }

    private CellValue[] GetColumn(int colIdx)
    {
      CellValue[] column = new CellValue[height];

      for (int row = 0; row < column.Length; row++)
      {
        column[row] = map[row, colIdx];
      }

      return column;
    }

    private CellValue[] GetRow(int rowIdx)
    {
      CellValue[] row = new CellValue[width];

      for (int col = 0; col < row.Length; col++)
      {
        row[col] = map[rowIdx, col];
      }

      return row;
    }

    private void ReplaceColumn(int colIdx, CellValue[] column)
    {
      for (int row = 0; row < column.Length; row++)
      {
        map[row, colIdx] = column[row];
      }
    }

    private void ReplaceRow(int rowIdx, CellValue[] row)
    {
      for (int col = 0; col < row.Length; col++)
      {
        map[rowIdx, col] = row[col];
      }
    }

    private Int32[][] Convert()
    {

      Int32[][] map = new Int32[height][];

      for (int row = 0; row < height; row++)
      {
        map[row] = new Int32[width];
        for (int col = 0; col < width; col++)
        {
          map[row][col] = this.map[row, col] == CellValue.Filled ? 1 : 0;
        }
      }

      return map;
    }
  }
}
