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
      public Boolean[] HintSolved
      { get; set; } = Array.Empty<Boolean>();
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

      // All hints already solved
      if (hints.All(h => h.IsSolved))
      {
        return new Result { Changed = false };
      }

      Int32[] leftStart = new Int32[hints.Length];
      if (!TryFitLeft(line, hints, 0, 0, leftStart))
      {
        return null;
      }

      Int32[] rightStart = new Int32[hints.Length];
      if (!TryFitRight(line, hints, rightStart))
      {
        return null;
      }

      CellValue?[] result = new CellValue?[line.Length];
      Boolean[] hintSolved = new Boolean[hints.Length];
      Boolean anyChanged = false;

      // Color overlap
      for (Int32 i = 0; i < hints.Length; i++)
      {
        Int32 overlapStart = rightStart[i];
        Int32 overlapEnd = leftStart[i] + hints[i].Count - 1;

        for (Int32 j = overlapStart; j <= overlapEnd; j++)
        {
          if (line[j] == CellValue.Unknown)
          {
            result[j] = CellValue.Color;
            anyChanged = true;
          }
        }
      }

      // Pinned hints: hint at exactly one position → write all its cells + separators
      for (Int32 i = 0; i < hints.Length; i++)
      {
        if (leftStart[i] == rightStart[i])
        {
          Int32 pos = leftStart[i];
          Int32 count = hints[i].Count;

          // All hint cells → Color
          for (Int32 j = pos; j < pos + count; j++)
          {
            if (line[j] == CellValue.Unknown)
            {
              result[j] = CellValue.Color;
              anyChanged = true;
            }
          }

          // Separator before hint → Background
          if (pos > 0 && line[pos - 1] == CellValue.Unknown)
          {
            result[pos - 1] = CellValue.Background;
            anyChanged = true;
          }

          // Separator after hint → Background
          Int32 afterHint = pos + count;
          if (afterHint < line.Length && line[afterHint] == CellValue.Unknown)
          {
            result[afterHint] = CellValue.Background;
            anyChanged = true;
          }
        }
      }

      // Unreachable cells: not in any hint's possible range → Background
      Boolean[] reachable = new Boolean[line.Length];
      for (Int32 i = 0; i < hints.Length; i++)
      {
        Int32 rangeStart = leftStart[i];
        Int32 rangeEnd = rightStart[i] + hints[i].Count - 1;
        for (Int32 j = rangeStart; j <= rangeEnd; j++)
        {
          reachable[j] = true;
        }
      }
      for (Int32 j = 0; j < line.Length; j++)
      {
        if (!reachable[j] && line[j] == CellValue.Unknown)
        {
          result[j] = CellValue.Background;
          anyChanged = true;
        }
      }

      // Apply prior deductions to build the line state for DP
      CellValue[] probeLine = (CellValue[])line.Clone();
      for (Int32 j = 0; j < probeLine.Length; j++)
      {
        if (result[j] is CellValue val)
        {
          probeLine[j] = val;
        }
      }

      // Forward+Backward DP: find ALL forced cells in O(NK) — single pass, no iteration
      CellValue?[]? dpDeductions = SolveDP(probeLine, hints);
      if (dpDeductions == null)
      {
        return null; // Contradiction
      }
      for (Int32 j = 0; j < line.Length; j++)
      {
        if (dpDeductions[j] is CellValue dpVal && result[j] == null)
        {
          result[j] = dpVal;
          anyChanged = true;
        }
      }

      return new Result
      {
        Deductions = result,
        HintSolved = hintSolved,
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

    /// <summary>
    /// DP-based feasibility check: can all hints fit on the line?
    /// O(N*K) guaranteed, no backtracking. Pre-allocated arrays avoid GC pressure.
    /// </summary>
    private static Boolean CanFit(CellValue[] line, Hint[] hints, Boolean[] dpPrev, Boolean[] dpCurr)
    {
      Int32 N = line.Length;
      Int32 K = hints.Length;

      if (K == 0)
      {
        for (Int32 j = 0; j < N; j++)
        {
          if (line[j] == CellValue.Color)
          {
            return false;
          }
        }
        return true;
      }

      // Base: dpPrev[j] = dp[0][j] = no Color cell in cells 0..j-1
      dpPrev[0] = true;
      for (Int32 j = 1; j <= N; j++)
      {
        dpPrev[j] = dpPrev[j - 1] && line[j - 1] != CellValue.Color;
      }

      for (Int32 i = 0; i < K; i++)
      {
        Int32 c = hints[i].Count;
        Array.Clear(dpCurr, 0, N + 1);

        Int32 nbr = 0; // non-Background run length
        for (Int32 j = 1; j <= N; j++)
        {
          nbr = line[j - 1] != CellValue.Background ? nbr + 1 : 0;

          // Option A: cell j-1 is background (gap)
          if (dpCurr[j - 1] && line[j - 1] != CellValue.Color)
          {
            dpCurr[j] = true;
          }

          // Option B: hint i placed at cells (j-c)...(j-1)
          if (!dpCurr[j] && j >= c && nbr >= c)
          {
            Int32 start = j - c;
            if (start == 0)
            {
              dpCurr[j] = dpPrev[0];
            }
            else if (line[start - 1] != CellValue.Color)
            {
              dpCurr[j] = dpPrev[start - 1];
            }
          }
        }

        (dpPrev, dpCurr) = (dpCurr, dpPrev);
      }

      return dpPrev[N];
    }

    internal static Boolean TryFitLeft(CellValue[] line, Hint[] hints, Int32 hintIdx, Int32 startPos, Int32[] result)
    {
      Int32 n = line.Length;

      if (hintIdx >= hints.Length)
      {
        for (Int32 j = startPos; j < n; j++)
        {
          if (line[j] == CellValue.Color)
          {
            return false;
          }
        }
        return true;
      }

      Int32 count = hints[hintIdx].Count;

      // Minimum space needed for remaining hints after this one
      Int32 minRemaining = 0;
      for (Int32 h = hintIdx + 1; h < hints.Length; h++)
      {
        minRemaining += hints[h].Count + 1;
      }

      Int32 maxStart = n - count - minRemaining;

      // Find first Color cell at or after startPos (uncovered-Color deadline)
      Int32 firstColor = -1;
      for (Int32 j = startPos; j < n; j++)
      {
        if (line[j] == CellValue.Color)
        {
          firstColor = j;
          break;
        }
      }

      for (Int32 pos = startPos; pos <= maxStart; pos++)
      {
        // If there's an uncovered Color cell before pos, fail
        if (firstColor >= 0 && firstColor < pos)
        {
          return false;
        }

        // Check hint span [pos, pos+count-1] has no Background
        Boolean spanOk = true;
        for (Int32 j = pos; j < pos + count; j++)
        {
          if (line[j] == CellValue.Background)
          {
            // Jump past the Background cell
            pos = j; // outer for loop will increment to j+1
            spanOk = false;
            break;
          }
        }
        if (!spanOk)
        {
          continue;
        }

        // Check separator after hint is not Color
        if (pos + count < n && line[pos + count] == CellValue.Color)
        {
          continue;
        }

        // Try placing hint here and recursively fit remaining
        result[hintIdx] = pos;
        if (TryFitLeft(line, hints, hintIdx + 1, pos + count + 1, result))
        {
          return true;
        }

        // Recursion failed — if current pos is a Color cell, we can't skip it
        if (line[pos] == CellValue.Color)
        {
          return false;
        }
      }

      return false;
    }

    internal static Boolean TryFitRight(CellValue[] line, Hint[] hints, Int32[] rightStart)
    {
      Int32 n = line.Length;
      Int32 k = hints.Length;

      // Reverse the line
      CellValue[] reversedLine = new CellValue[n];
      for (Int32 j = 0; j < n; j++)
      {
        reversedLine[j] = line[n - 1 - j];
      }

      // Reverse the hints
      Hint[] reversedHints = new Hint[k];
      for (Int32 i = 0; i < k; i++)
      {
        reversedHints[i] = hints[k - 1 - i];
      }

      // Run leftmost fit on reversed data
      Int32[] leftStartRev = new Int32[k];
      if (!TryFitLeft(reversedLine, reversedHints, 0, 0, leftStartRev))
      {
        return false;
      }

      // Convert back: rightStart[i] = n - leftStartRev[k-1-i] - hints[i].Count
      for (Int32 i = 0; i < k; i++)
      {
        rightStart[i] = n - leftStartRev[k - 1 - i] - hints[i].Count;
      }

      return true;
    }
  }
}
