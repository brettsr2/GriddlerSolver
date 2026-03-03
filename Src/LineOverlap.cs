using System;
using System.Linq;

namespace Griddler_Solver
{
  internal static class LineOverlap
  {
    internal class Result
    {
      public CellValue?[] Deductions
      { get; set; } = Array.Empty<CellValue?>();
      public Boolean Changed
      { get; set; }
    }

    public static Result? Solve(CellValue[] line, Hint[] hints)
    {
      // All cells already known
      if (!line.Contains(CellValue.Unknown))
      {
        return new Result { Changed = false };
      }

      // No hints — all Unknown cells become Background
      if (hints.Length == 0)
      {
        CellValue?[] deductions = new CellValue?[line.Length];
        Boolean changed = false;
        for (Int32 j = 0; j < line.Length; j++)
        {
          if (line[j] == CellValue.Unknown)
          {
            deductions[j] = CellValue.Background;
            changed = true;
          }
        }
        return new Result { Deductions = deductions, Changed = changed };
      }

      // Forward+Backward DP: find ALL forced cells in O(NK) — single pass
      CellValue?[]? dpDeductions = SolveDP(line, hints);
      if (dpDeductions == null)
      {
        return null; // Contradiction
      }

      Boolean anyChanged = false;
      for (Int32 j = 0; j < line.Length; j++)
      {
        if (dpDeductions[j] is CellValue)
        {
          anyChanged = true;
          break;
        }
      }

      return new Result
      {
        Deductions = dpDeductions,
        Changed = anyChanged
      };
    }

    // ThreadStatic DP tables — one allocation per thread, reused across calls
    [ThreadStatic] private static Boolean[][]? _tls_F;
    [ThreadStatic] private static Boolean[][]? _tls_B;
    [ThreadStatic] private static Boolean[]? _tls_canBeBg;
    [ThreadStatic] private static Boolean[]? _tls_canBeColor;
    [ThreadStatic] private static Int32[]? _tls_nbrFwd;

    private static void EnsureDPTables(Int32 K, Int32 N)
    {
      if (_tls_F == null || _tls_F.Length < K + 1 || _tls_F[0].Length < N + 1)
      {
        _tls_F = new Boolean[K + 1][];
        for (Int32 i = 0; i <= K; i++)
        {
          _tls_F[i] = new Boolean[N + 1];
        }
      }
      if (_tls_B == null || _tls_B.Length < K + 1 || _tls_B[0].Length < N + 1)
      {
        _tls_B = new Boolean[K + 1][];
        for (Int32 i = 0; i <= K; i++)
        {
          _tls_B[i] = new Boolean[N + 1];
        }
      }
      if (_tls_canBeBg == null || _tls_canBeBg.Length < N)
      {
        _tls_canBeBg = new Boolean[N];
      }
      if (_tls_canBeColor == null || _tls_canBeColor.Length < N)
      {
        _tls_canBeColor = new Boolean[N];
      }
      if (_tls_nbrFwd == null || _tls_nbrFwd.Length < N + 1)
      {
        _tls_nbrFwd = new Int32[N + 1];
      }
    }

    /// <summary>
    /// Forward DP: F[i][j] = "first i hints fit in cells 0..j-1".
    /// Same logic as CanFit but saves ALL K+1 rows.
    /// </summary>
    private static void ComputeForwardDP(CellValue[] line, Hint[] hints, Boolean[][] F)
    {
      Int32 N = line.Length;
      Int32 K = hints.Length;

      for (Int32 i = 0; i <= K; i++)
      {
        Array.Clear(F[i], 0, N + 1);
      }

      // Base: F[0][j] = no Color cell in 0..j-1
      F[0][0] = true;
      for (Int32 j = 1; j <= N; j++)
      {
        F[0][j] = F[0][j - 1] && line[j - 1] != CellValue.Color;
      }

      for (Int32 i = 0; i < K; i++)
      {
        Int32 c = hints[i].Count;
        Int32 nbr = 0;
        for (Int32 j = 1; j <= N; j++)
        {
          nbr = line[j - 1] != CellValue.Background ? nbr + 1 : 0;

          // Option A: cell j-1 is gap after placing first i+1 hints
          if (F[i + 1][j - 1] && line[j - 1] != CellValue.Color)
          {
            F[i + 1][j] = true;
          }

          // Option B: hint i placed at cells (j-c)...(j-1)
          if (!F[i + 1][j] && j >= c && nbr >= c)
          {
            Int32 start = j - c;
            if (start == 0)
            {
              F[i + 1][j] = F[i][0];
            }
            else if (line[start - 1] != CellValue.Color)
            {
              F[i + 1][j] = F[i][start - 1];
            }
          }
        }
      }
    }

    /// <summary>
    /// Backward DP: B[i][j] = "hints i..K-1 fit in cells j..N-1".
    /// Mirror of forward DP, scanning right to left.
    /// </summary>
    private static void ComputeBackwardDP(CellValue[] line, Hint[] hints, Boolean[][] B, Int32[] nbrFwd)
    {
      Int32 N = line.Length;
      Int32 K = hints.Length;

      for (Int32 i = 0; i <= K; i++)
      {
        Array.Clear(B[i], 0, N + 1);
      }

      // Base: B[K][j] = no Color cell in j..N-1
      B[K][N] = true;
      for (Int32 j = N - 1; j >= 0; j--)
      {
        B[K][j] = B[K][j + 1] && line[j] != CellValue.Color;
      }

      // Precompute forward non-Background run lengths: nbrFwd[j] = run from j going right
      // nbrFwd[N] must be 0 (sentinel); the array may be reused from a longer line, so initialize explicitly
      nbrFwd[N] = 0;
      for (Int32 j = N - 1; j >= 0; j--)
      {
        nbrFwd[j] = line[j] != CellValue.Background ? nbrFwd[j + 1] + 1 : 0;
      }

      for (Int32 i = K - 1; i >= 0; i--)
      {
        Int32 c = hints[i].Count;
        for (Int32 j = N - 1; j >= 0; j--)
        {
          // Option A: cell j is gap, hints i..K-1 placed somewhere after j
          if (B[i][j + 1] && line[j] != CellValue.Color)
          {
            B[i][j] = true;
          }

          // Option B: place hint i at cells j..j+c-1
          if (!B[i][j] && j + c <= N && nbrFwd[j] >= c)
          {
            Int32 afterHint = j + c;
            if (afterHint == N)
            {
              B[i][j] = B[i + 1][N];
            }
            else if (line[afterHint] != CellValue.Color)
            {
              B[i][j] = B[i + 1][afterHint + 1];
            }
          }
        }
      }
    }

    /// <summary>
    /// Forward+Backward DP line solver: finds ALL forced cells in O(NK).
    /// Returns deductions array (null entries = no deduction), or null if contradiction.
    /// </summary>
    private static CellValue?[]? SolveDP(CellValue[] line, Hint[] hints)
    {
      Int32 N = line.Length;
      Int32 K = hints.Length;

      EnsureDPTables(K, N);
      Boolean[][] F = _tls_F!;
      Boolean[][] B = _tls_B!;
      Boolean[] canBeBg = _tls_canBeBg!;
      Boolean[] canBeColor = _tls_canBeColor!;
      Int32[] nbrFwd = _tls_nbrFwd!;

      // 1. Forward DP
      ComputeForwardDP(line, hints, F);
      if (!F[K][N])
      {
        return null; // No valid placement exists — contradiction
      }

      // 2. Backward DP
      ComputeBackwardDP(line, hints, B, nbrFwd);

      // 3. Compute canBeBg: cell j can be Background if there's a valid split
      Array.Clear(canBeBg, 0, N);
      for (Int32 j = 0; j < N; j++)
      {
        if (line[j] == CellValue.Color)
        {
          continue;
        }
        for (Int32 i = 0; i <= K; i++)
        {
          if (F[i][j] && B[i][j + 1])
          {
            canBeBg[j] = true;
            break;
          }
        }
      }

      // 4. Compute canBeColor: cell j can be Color if some hint can cover it
      Array.Clear(canBeColor, 0, N);
      for (Int32 i = 0; i < K; i++)
      {
        Int32 c = hints[i].Count;
        for (Int32 s = 0; s <= N - c; s++)
        {
          // Check span: all cells s..s+c-1 are non-Background
          if (nbrFwd[s] < c)
          {
            continue;
          }

          // Check before: first i hints fit before position s with separator
          Boolean beforeOK;
          if (s == 0)
          {
            beforeOK = F[i][0];
          }
          else
          {
            beforeOK = line[s - 1] != CellValue.Color && F[i][s - 1];
          }
          if (!beforeOK)
          {
            continue;
          }

          // Check after: remaining hints fit after hint i with separator
          Int32 afterHint = s + c;
          Boolean afterOK;
          if (afterHint >= N)
          {
            afterOK = B[i + 1][N];
          }
          else
          {
            afterOK = line[afterHint] != CellValue.Color && B[i + 1][afterHint + 1];
          }
          if (!afterOK)
          {
            continue;
          }

          // Valid placement — mark cells s..s+c-1
          for (Int32 j = s; j < s + c; j++)
          {
            canBeColor[j] = true;
          }
        }
      }

      // 5. Build deductions
      CellValue?[] deductions = new CellValue?[N];
      for (Int32 j = 0; j < N; j++)
      {
        if (line[j] != CellValue.Unknown)
        {
          continue;
        }
        if (!canBeBg[j] && !canBeColor[j])
        {
          return null; // Contradiction — cell can be neither
        }
        if (canBeColor[j] && !canBeBg[j])
        {
          deductions[j] = CellValue.Color;
        }
        else if (canBeBg[j] && !canBeColor[j])
        {
          deductions[j] = CellValue.Background;
        }
      }

      return deductions;
    }

  }
}
