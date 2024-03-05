using System;
using System.Text.Json.Serialization;

namespace Griddler_Solver
{
  internal class Board
  {
    private Object _Lock = new();

    public Boolean IsSolved
    {
      get
      {
        if (_Board.GetLength(0) != RowCount || _Board.GetLength(1) != ColumnCount)
        {
          return false;
        }

        for (Int32 row = 0; row < RowCount; row++)
        {
          for (Int32 col = 0; col < ColumnCount; col++)
          {
            if (this[row, col] == CellValue.Unknown)
            {
              return false;
            }
          }
        }

        return true;
      }
    }

    [JsonIgnore]
    public TimeSpan TimeTaken
    { get; set; }
    [JsonIgnore]
    public Int32 Iterations
    { get; set; }

    public CellValue this[Int32 row, Int32 column]
    {
      get
      {
        return _Board[row, column];
      }
      set
      {
        _Board[row, column] = value;
      }
    }

    public Hint[][] HintsRow
    { get; set; } = Array.Empty<Hint[]>();
    public Hint[][] HintsColumn
    { get; set; } = Array.Empty<Hint[]>();

    public Int32 RowCount
    {
      get
      {
        return HintsRow.Length;
      }
    }
    public Int32 ColumnCount
    {
      get
      {
        return HintsColumn.Length;
      }
    }

    [JsonIgnore]
    private CellValue[,] _Board
    { get; set; } = new CellValue[0, 0];

    public CellValue[][] CurrentState
    {
      get
      {
        return Convert();
      }
      set
      {
        Init();
        Convert(value);
      }
    }

    public void Init()
    {
      _Board = new CellValue[RowCount, ColumnCount];
    }

    public CellValue[] GetColumn(Int32 indexColumn)
    {
      CellValue[] column = new CellValue[RowCount];

      lock (_Lock)
      {
        for (int row = 0; row < column.Length; row++)
        {
          column[row] = _Board[row, indexColumn];
        }
      }

      return column;
    }
    public CellValue[] GetRow(Int32 indexRow)
    {
      CellValue[] row = new CellValue[ColumnCount];

      lock (_Lock)
      {
        for (Int32 column = 0; column < row.Length; column++)
        {
          row[column] = _Board[indexRow, column];
        }
      }

      return row;
    }

    public void MergeColumn(Int32 indexColumn, CellValue[] column)
    {
      lock (_Lock)
      {
        for (Int32 row = 0; row < column.Length; row++)
        {
          if (_Board[row, indexColumn] == CellValue.Unknown)
          {
            _Board[row, indexColumn] = column[row];
          }
        }
      }
    }
    public void MergeRow(Int32 indexRow, CellValue[] row)
    {
      lock (_Lock)
      {
        for (Int32 column = 0; column < row.Length; column++)
        {
          if (_Board[indexRow, column] == CellValue.Unknown)
          {
            _Board[indexRow, column] = row[column];
          }
        }
      }
    }

    private CellValue[][] Convert()
    {
      CellValue[][] board = new CellValue[RowCount][];

      for (Int32 row = 0; row < RowCount; row++)
      {
        board[row] = GetRow(row);
      }

      return board;
    }
    private void Convert(CellValue[][] cellValues)
    {
      for (Int32 row = 0; row < RowCount; row++)
      {
        MergeRow(row, cellValues[row]);
      }
    }
  }
}
