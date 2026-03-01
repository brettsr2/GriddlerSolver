using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Threading;

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

    public Int32 MaxHintsCountRow
    {
      get
      {
        return GetMaxItemCount(Board.HintsRow);
      }
    }
    public Int32 MaxHintsCountColumn
    {
      get
      {
        return GetMaxItemCount(Board.HintsColumn);
      }
    }

    public Double CellSize
    { get; set; } = 15;

    public List<PuzzleColors> ListColors
    { get; set; } = [];
    #endregion // drawing

    #region solving
    [JsonIgnore]
    public Config Config
    { get; set; } = new();

    private readonly record struct LineKey(Boolean IsRow, Int32 Index);
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

      List<SolverLine> listSolverLine = [];
      for (Int32 row = 0; row < Board.RowCount; row++)
      {
        listSolverLine.Add(new SolverLine()
        {
          Config = Config,
          Index = row,
          IsRow = true,
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
          Board = Board,
        });
      }
      // Build lookup arrays for O(1) row/column → SolverLine mapping
      SolverLine[] rowLines = new SolverLine[Board.RowCount];
      SolverLine[] columnLines = new SolverLine[Board.ColumnCount];
      foreach (SolverLine solverLine in listSolverLine)
      {
        if (solverLine.IsRow)
        {
          rowLines[solverLine.Index] = solverLine;
        }
        else
        {
          columnLines[solverLine.Index] = solverLine;
        }
      }

      Stopwatch stopWatchGlobal = Stopwatch.StartNew();

      ProcessWorkQueue(listSolverLine, rowLines, columnLines);

      if (!Board.IsSolved && !Config.Break)
      {
        Config.Progress?.AddMessage("Solver stuck.");
      }

      Config.Break = true;

      Board.TimeTaken = stopWatchGlobal.Elapsed;
    }
    private void ProcessWorkQueue(
            List<SolverLine> listSolverLine,
            SolverLine[] rowLines,
            SolverLine[] columnLines)
    {
      var queue = new ConcurrentQueue<SolverLine>();
      var inSystem = new ConcurrentDictionary<LineKey, byte>();
      Int32 pendingItems = 0;
      var drainComplete = new ManualResetEventSlim(false);

      // Local helper: try to enqueue a line if dirty, unsolved, and not already in system
      void TryEnqueue(SolverLine line)
      {
        if (Config.Break)
        {
          return;
        }

        if (line.IsSolved)
        {
          return;
        }

        Boolean dirty = line.IsRow ? Board.IsRowDirty(line.Index) : Board.IsColumnDirty(line.Index);
        if (!dirty)
        {
          return;
        }

        var key = new LineKey(line.IsRow, line.Index);
        if (inSystem.TryAdd(key, 0))
        {
          Interlocked.Increment(ref pendingItems);
          queue.Enqueue(line);
        }
      }

      // Seed the queue with all eligible lines
      foreach (SolverLine solverLine in listSolverLine)
      {
        TryEnqueue(solverLine);
      }

      if (Volatile.Read(ref pendingItems) == 0)
      {
        return;
      }

      // Worker procedure
      void WorkerProc()
      {
        while (!Config.Break)
        {
          if (!queue.TryDequeue(out SolverLine? line))
          {
            if (Volatile.Read(ref pendingItems) <= 0)
            {
              break;
            }
            Thread.Sleep(1);
            continue;
          }

          var lineKey = new LineKey(line.IsRow, line.Index);

          // Clear dirty BEFORE reading snapshot
          if (line.IsRow)
          {
            Board.ClearRowDirty(line.Index);
          }
          else
          {
            Board.ClearColumnDirty(line.Index);
          }

          line.Solve();

          // Remove from tracking (allows re-enqueue if dirty again)
          inSystem.TryRemove(lineKey, out _);

          // Re-check: self became dirty during processing? (cross-line merge dirtied us)
          TryEnqueue(line);

          // Enqueue affected cross-lines
          if (line.Changed)
          {
            if (line.IsRow)
            {
              for (Int32 c = 0; c < Board.ColumnCount; c++)
              {
                if (Board.IsColumnDirty(c))
                {
                  TryEnqueue(columnLines[c]);
                }
              }
            }
            else
            {
              for (Int32 r = 0; r < Board.RowCount; r++)
              {
                if (Board.IsRowDirty(r))
                {
                  TryEnqueue(rowLines[r]);
                }
              }
            }
          }

          // Signal completion of this item
          if (Interlocked.Decrement(ref pendingItems) <= 0)
          {
            drainComplete.Set();
          }
        }

        // If exiting due to Break, signal drain so main thread isn't stuck
        if (Config.Break)
        {
          drainComplete.Set();
        }
      }

      // Dispatch workers
      Int32 maxWorkers = Config.MultithreadEnabled ? Environment.ProcessorCount : 1;
      var workersExited = new CountdownEvent(maxWorkers);

      for (Int32 i = 0; i < maxWorkers; i++)
      {
        ThreadPool.QueueUserWorkItem(_ =>
        {
          try
          {
            WorkerProc();
          }
          finally
          {
            workersExited.Signal();
          }
        });
      }

      // Wait for queue to drain and all workers to exit
      drainComplete.Wait();
      workersExited.Wait();
    }

  }
}
