using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Griddler_Solver
{
  class BoardCanvas : FrameworkElement
  {
    private Solver? _solver;

    private readonly Pen _penGrey;
    private readonly Pen _penBlack;
    private readonly Typeface _typeface = new("Segoe UI");

    public BoardCanvas()
    {
      _penGrey = new Pen(Brushes.Gray, 1);
      _penGrey.Freeze();
      _penBlack = new Pen(Brushes.Black, 2);
      _penBlack.Freeze();
    }

    public void SetSolver(Solver solver)
    {
      _solver = solver;
      InvalidateVisual();
    }

    public void Refresh() => InvalidateVisual();

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
      base.OnRenderSizeChanged(sizeInfo);
      InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
      if (_solver == null || _solver.Board.RowCount == 0 || _solver.Board.ColumnCount == 0)
        return;

      var board = _solver.Board;
      Double cellSize = Math.Min(
        ActualHeight / (_solver.MaxHintsCountColumn + board.RowCount),
        ActualWidth / (_solver.MaxHintsCountRow + board.ColumnCount)
      );
      _solver.CellSize = cellSize;

      Double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
      Double fontSize = cellSize * 0.8;
      Double originX = _solver.MaxHintsCountRow * cellSize;
      Double originY = _solver.MaxHintsCountColumn * cellSize;

      void DrawCell(Double left, Double top, SolidColorBrush brush)
      {
        dc.DrawRectangle(brush, null, new Rect(left, top, cellSize, cellSize));
      }

      void DrawHintText(Double centerX, Double centerY, String text, SolidColorBrush brush)
      {
        var ft = new FormattedText(
          text,
          CultureInfo.CurrentCulture,
          FlowDirection.LeftToRight,
          _typeface,
          fontSize,
          brush,
          pixelsPerDip
        );
        dc.DrawText(ft, new Point(centerX - ft.Width / 2, centerY - ft.Height / 2));
      }

      void DrawCross(Double left, Double top)
      {
        dc.DrawLine(_penGrey, new Point(left, top), new Point(left + cellSize, top + cellSize));
        dc.DrawLine(_penGrey, new Point(left + cellSize, top), new Point(left, top + cellSize));
      }

      // 1. Column hints
      Double cx = originX;
      for (Int32 col = 0; col < board.ColumnCount; col++)
      {
        Hint[] hints = board.HintsColumn[col];
        Double cy = (_solver.MaxHintsCountColumn - hints.Length) * cellSize;
        foreach (Hint hint in hints)
        {
          DrawCell(cx, cy, _solver.ListColors[hint.ColorId].ColorBrush);
          DrawHintText(cx + cellSize / 2, cy + cellSize / 2, hint.Count.ToString(), _solver.ListColors[1].ColorBrush);
          if (hint.IsSolved)
            DrawCross(cx, cy);
          cy += cellSize;
        }
        cx += cellSize;
      }

      // 2. Row hints
      Double ry = originY;
      for (Int32 row = 0; row < board.RowCount; row++)
      {
        Hint[] hints = board.HintsRow[row];
        Double rx = (_solver.MaxHintsCountRow - hints.Length) * cellSize;
        foreach (Hint hint in hints)
        {
          DrawCell(rx, ry, _solver.ListColors[hint.ColorId].ColorBrush);
          DrawHintText(rx + cellSize / 2, ry + cellSize / 2, hint.Count.ToString(), _solver.ListColors[1].ColorBrush);
          if (hint.IsSolved)
            DrawCross(rx, ry);
          rx += cellSize;
        }
        ry += cellSize;
      }

      // 3. Board cells
      for (Int32 col = 0; col < board.ColumnCount; col++)
      {
        for (Int32 row = 0; row < board.RowCount; row++)
        {
          Int32 colorIdx = (Int32)board[row, col];
          Double x = originX + col * cellSize;
          Double y = originY + row * cellSize;
          DrawCell(x, y, _solver.ListColors[colorIdx].ColorBrush);
        }
      }

      // 4. Grid lines
      for (Int32 row = 0; row <= board.RowCount; row++)
      {
        Double y = originY + row * cellSize;
        Pen pen = row % 5 == 0 ? _penBlack : _penGrey;
        dc.DrawLine(pen, new Point(originX, y), new Point(originX + board.ColumnCount * cellSize, y));
      }
      for (Int32 col = 0; col <= board.ColumnCount; col++)
      {
        Double x = originX + col * cellSize;
        Pen pen = col % 5 == 0 ? _penBlack : _penGrey;
        dc.DrawLine(pen, new Point(x, originY), new Point(x, originY + board.RowCount * cellSize));
      }

      // 5. Static analysis markers
      foreach (StaticAnalysis sa in _solver.ListStaticAnalysis)
      {
        Double x = originX + sa.Column * cellSize;
        Double y = originY + sa.Row * cellSize;
        DrawCross(x, y);
      }
    }
  }
}
