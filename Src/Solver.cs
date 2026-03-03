using System;
using System.Collections.Generic;

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

    public void Solve()
    {
      if (Board.RowCount == 0 || Board.ColumnCount == 0)
      {
        return;
      }
      if (Board.IsSolved)
      {
        return;
      }

      List<SolverLine> listSolverLine = [];
      for (Int32 row = 0; row < Board.RowCount; row++)
      {
        listSolverLine.Add(new SolverLine()
        {
          Index = row,
          IsRow = true,
          Board = Board,
        });
      }
      for (Int32 column = 0; column < Board.ColumnCount; column++)
      {
        listSolverLine.Add(new SolverLine()
        {
          Index = column,
          IsRow = false,
          Board = Board,
        });
      }

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

      ProcessWorkQueue(listSolverLine, rowLines, columnLines);
    }

    private void ProcessWorkQueue(List<SolverLine> listSolverLine, SolverLine[] rowLines, SolverLine[] columnLines)
    {
      var queue = new Queue<SolverLine>();
      var inSystem = new HashSet<LineKey>();

      foreach (SolverLine solverLine in listSolverLine)
      {
        TryEnqueue(solverLine, queue, inSystem);
      }

      while (queue.Count > 0)
      {
        SolverLine line = queue.Dequeue();
        inSystem.Remove(new LineKey(line.IsRow, line.Index));

        if (line.IsRow)
        {
          Board.ClearRowDirty(line.Index);
        }
        else
        {
          Board.ClearColumnDirty(line.Index);
        }

        line.Solve();

        TryEnqueue(line, queue, inSystem);

        if (line.Changed)
        {
          if (line.IsRow)
          {
            for (Int32 c = 0; c < Board.ColumnCount; c++)
            {
              if (Board.IsColumnDirty(c))
              {
                TryEnqueue(columnLines[c], queue, inSystem);
              }
            }
          }
          else
          {
            for (Int32 r = 0; r < Board.RowCount; r++)
            {
              if (Board.IsRowDirty(r))
              {
                TryEnqueue(rowLines[r], queue, inSystem);
              }
            }
          }
        }
      }
    }

    private void TryEnqueue(SolverLine line, Queue<SolverLine> queue, HashSet<LineKey> inSystem)
    {
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
      if (inSystem.Add(key))
      {
        queue.Enqueue(line);
      }
    }

  }
}
