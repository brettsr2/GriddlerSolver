using System;
using System.Numerics;

namespace Griddler_Solver
{
  internal struct LineBitmask
  {
    public ulong Color0, Color1;
    public ulong Bg0, Bg1;
    public Int32 Length;

    public static LineBitmask FromLine(CellValue[] line)
    {
      var bm = new LineBitmask { Length = line.Length };
      for (Int32 i = 0; i < line.Length; i++)
      {
        ulong bit = 1UL << (i & 63);
        if (i < 64)
        {
          if (line[i] == CellValue.Color) bm.Color0 |= bit;
          else if (line[i] == CellValue.Background) bm.Bg0 |= bit;
        }
        else
        {
          if (line[i] == CellValue.Color) bm.Color1 |= bit;
          else if (line[i] == CellValue.Background) bm.Bg1 |= bit;
        }
      }
      return bm;
    }

    public CellValue[] ToLine()
    {
      var line = new CellValue[Length];
      for (Int32 i = 0; i < Length; i++)
      {
        ulong bit = 1UL << (i & 63);
        ulong c = i < 64 ? Color0 : Color1;
        ulong b = i < 64 ? Bg0 : Bg1;

        if ((c & bit) != 0)
          line[i] = CellValue.Color;
        else if ((b & bit) != 0)
          line[i] = CellValue.Background;
      }
      return line;
    }

    public Boolean FillRange(Int32 start, Int32 count, Int32 maxHintCellCount)
    {
      MaskRange(start, count, out ulong m0, out ulong m1);

      if (((Bg0 & m0) | (Bg1 & m1)) != 0)
        return false;

      Color0 |= m0;
      Color1 |= m1;

      Int32 colorCount = BitOperations.PopCount(Color0)
                       + BitOperations.PopCount(Color1);
      return colorCount <= maxHintCellCount;
    }

    public void FillEmptyCells()
    {
      MaskRange(0, Length, out ulong a0, out ulong a1);
      Bg0 |= a0 & ~(Color0 | Bg0);
      Bg1 |= a1 & ~(Color1 | Bg1);
    }

    public Boolean IsValid(LineBitmask origin)
    {
      ulong mismatch = (origin.Color0 & Bg0) | (origin.Bg0 & Color0)
                      | (origin.Color1 & Bg1) | (origin.Bg1 & Color1);
      return mismatch == 0;
    }

    public void MergeWith(LineBitmask perm)
    {
      Color0 &= perm.Color0;
      Color1 &= perm.Color1;
      Bg0 &= perm.Bg0;
      Bg1 &= perm.Bg1;
    }

    public Boolean HasNewDeductions(LineBitmask origin)
    {
      MaskRange(0, Length, out ulong valid0, out ulong valid1);
      ulong new0 = (Color0 | Bg0) & ~(origin.Color0 | origin.Bg0) & valid0;
      ulong new1 = (Color1 | Bg1) & ~(origin.Color1 | origin.Bg1) & valid1;
      return (new0 | new1) != 0;
    }

    public Boolean HasColorInRange(Int32 start, Int32 count)
    {
      MaskRange(start, count, out ulong m0, out ulong m1);
      return ((Color0 & m0) | (Color1 & m1)) != 0;
    }

    public Boolean HasColorAt(Int32 pos)
    {
      ulong bit = 1UL << (pos & 63);
      return pos < 64 ? (Color0 & bit) != 0 : (Color1 & bit) != 0;
    }

    public Boolean IsFull()
    {
      MaskRange(0, Length, out ulong a0, out ulong a1);
      return ((Color0 | Bg0) & a0) == a0
          && ((Color1 | Bg1) & a1) == a1;
    }

    private static ulong WordMask(Int32 lo, Int32 hi)
    {
      Int32 count = hi - lo;
      if (count >= 64) return ulong.MaxValue;
      return ((1UL << count) - 1) << lo;
    }

    private static void MaskRange(Int32 start, Int32 count, out ulong m0, out ulong m1)
    {
      m0 = 0; m1 = 0;
      Int32 end = start + count;

      if (start < 64 && end > 0)
        m0 = WordMask(Math.Max(start, 0), Math.Min(end, 64));
      if (start < 128 && end > 64)
        m1 = WordMask(Math.Max(start - 64, 0), Math.Min(end - 64, 64));
    }
  }
}
