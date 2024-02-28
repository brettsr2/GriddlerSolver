using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Griddler_Solver
{
  internal class Board
  {
    private Object _Lock = new();

    private CellValue[,] _Board
    { get; set; } = new CellValue[0, 0];

    public CellValue this[Int32 row, Int32 column]
    {
      get
      {
        return _Board[row, column];
      }
    }

    public Hint[][] HintsRow
    { get; set; } = Array.Empty<Hint[]>();
    public Hint[][] HintsColumn
    { get; set; } = Array.Empty<Hint[]>();

    public Int32 HintsRowCount
    {
      get
      {
        return HintsRow.Length;
      }
    }
    public Int32 HintsColumnCount
    {
      get
      {
        return HintsColumn.Length;
      }
    }

    public void Init()
    {
      _Board = new CellValue[HintsRowCount, HintsColumnCount];

      for (Int32 row = 0; row < HintsRowCount; row++)
      {
        for (Int32 col = 0; col < HintsColumnCount; col++)
        {
          _Board[row, col] = CellValue.Unknown;
        }
      }
    }

    public CellValue[] GetColumn(Int32 indexColumn)
    {
      CellValue[] column = new CellValue[HintsRowCount];

      for (int row = 0; row < column.Length; row++)
      {
        column[row] = _Board[row, indexColumn];
      }

      return column;
    }
    public CellValue[] GetRow(Int32 indexRow)
    {
      CellValue[] row = new CellValue[HintsColumnCount];

      for (Int32 column = 0; column < row.Length; column++)
      {
        row[column] = _Board[indexRow, column];
      }

      return row;
    }

    public void ReplaceColumn(Int32 indexColumn, CellValue[] column)
    {
      lock (_Lock)
      {
        for (Int32 row = 0; row < column.Length; row++)
        {
          _Board[row, indexColumn] = column[row];
        }
      }
    }
    public void ReplaceRow(Int32 indexRow, CellValue[] row)
    {
      lock (_Lock)
      {
        for (Int32 column = 0; column < row.Length; column++)
        {
          _Board[indexRow, column] = row[column];
        }
      }
    }

    public CellValue[][] Convert()
    {
      CellValue[][] board = new CellValue[HintsRowCount][];

      for (Int32 row = 0; row < HintsRowCount; row++)
      {
        board[row] = GetRow(row);
      }

      return board;
    }

    public Boolean IsSolved()
    {
      for (Int32 row = 0; row < HintsRowCount; row++)
      {
        for (Int32 col = 0; col < HintsColumnCount; col++)
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
}
