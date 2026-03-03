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

    // DP tables — allocated once and reused across calls to avoid GC pressure
    // forward[i][j]    = "first i hints fit in cells 0..j-1"
    // backward[i][j]   = "hints i..K-1 fit in cells j..N-1"
    // nonBgRunRight[j] = count of consecutive non-Background cells starting at j going right
    private static Boolean[][]? _dpForward;
    private static Boolean[][]? _dpBackward;
    private static Boolean[]? _canBeBackground;
    private static Boolean[]? _canBeColor;
    private static Int32[]? _nonBgRunRight;

    private static void EnsureDPTables(Int32 K, Int32 N)
    {
      if (_dpForward == null || _dpForward.Length < K + 1 || _dpForward[0].Length < N + 1)
      {
        _dpForward = new Boolean[K + 1][];
        for (Int32 i = 0; i <= K; i++)
        {
          _dpForward[i] = new Boolean[N + 1];
        }
      }
      if (_dpBackward == null || _dpBackward.Length < K + 1 || _dpBackward[0].Length < N + 1)
      {
        _dpBackward = new Boolean[K + 1][];
        for (Int32 i = 0; i <= K; i++)
        {
          _dpBackward[i] = new Boolean[N + 1];
        }
      }
      if (_canBeBackground == null || _canBeBackground.Length < N)
      {
        _canBeBackground = new Boolean[N];
      }
      if (_canBeColor == null || _canBeColor.Length < N)
      {
        _canBeColor = new Boolean[N];
      }
      if (_nonBgRunRight == null || _nonBgRunRight.Length < N + 1)
      {
        _nonBgRunRight = new Int32[N + 1];
      }
    }

    /// <summary>
    /// Forward DP: F[i][j] = "first i hints fit in cells 0..j-1".
    /// Same logic as CanFit but saves ALL K+1 rows.
    /// </summary>
    private static void ComputeForwardDP(CellValue[] line, Hint[] hints, Boolean[][] forward)
    {
      Int32 N = line.Length;
      Int32 K = hints.Length;

      for (Int32 i = 0; i <= K; i++)
      {
        Array.Clear(forward[i], 0, N + 1);
      }

      // Base: forward[0][j] = no Color cell in 0..j-1
      forward[0][0] = true;
      for (Int32 j = 1; j <= N; j++)
      {
        forward[0][j] = forward[0][j - 1] && line[j - 1] != CellValue.Color;
      }

      for (Int32 i = 0; i < K; i++)
      {
        Int32 c = hints[i].Count;
        Int32 nbr = 0;
        for (Int32 j = 1; j <= N; j++)
        {
          nbr = line[j - 1] != CellValue.Background ? nbr + 1 : 0;

          // Option A: cell j-1 is gap after placing first i+1 hints
          if (forward[i + 1][j - 1] && line[j - 1] != CellValue.Color)
          {
            forward[i + 1][j] = true;
          }

          // Option B: hint i placed at cells (j-c)...(j-1)
          if (!forward[i + 1][j] && j >= c && nbr >= c)
          {
            Int32 start = j - c;
            if (start == 0)
            {
              forward[i + 1][j] = forward[i][0];
            }
            else if (line[start - 1] != CellValue.Color)
            {
              forward[i + 1][j] = forward[i][start - 1];
            }
          }
        }
      }
    }

    /// <summary>
    /// Backward DP: B[i][j] = "hints i..K-1 fit in cells j..N-1".
    /// Mirror of forward DP, scanning right to left.
    /// </summary>
    private static void ComputeBackwardDP(CellValue[] line, Hint[] hints, Boolean[][] backward, Int32[] nonBgRunRight)
    {
      Int32 N = line.Length;
      Int32 K = hints.Length;

      for (Int32 i = 0; i <= K; i++)
      {
        Array.Clear(backward[i], 0, N + 1);
      }

      // Base: backward[K][j] = no Color cell in j..N-1
      backward[K][N] = true;
      for (Int32 j = N - 1; j >= 0; j--)
      {
        backward[K][j] = backward[K][j + 1] && line[j] != CellValue.Color;
      }

      // Precompute non-Background run lengths going right: nonBgRunRight[j] = run from j going right
      // nonBgRunRight[N] must be 0 (sentinel); the array may be reused from a longer line, so initialize explicitly
      nonBgRunRight[N] = 0;
      for (Int32 j = N - 1; j >= 0; j--)
      {
        nonBgRunRight[j] = line[j] != CellValue.Background ? nonBgRunRight[j + 1] + 1 : 0;
      }

      for (Int32 i = K - 1; i >= 0; i--)
      {
        Int32 c = hints[i].Count;
        for (Int32 j = N - 1; j >= 0; j--)
        {
          // Option A: cell j is gap, hints i..K-1 placed somewhere after j
          if (backward[i][j + 1] && line[j] != CellValue.Color)
          {
            backward[i][j] = true;
          }

          // Option B: place hint i at cells j..j+c-1
          if (!backward[i][j] && j + c <= N && nonBgRunRight[j] >= c)
          {
            Int32 afterHint = j + c;
            if (afterHint == N)
            {
              backward[i][j] = backward[i + 1][N];
            }
            else if (line[afterHint] != CellValue.Color)
            {
              backward[i][j] = backward[i + 1][afterHint + 1];
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
      Boolean[][] forward = _dpForward!;
      Boolean[][] backward = _dpBackward!;
      Boolean[] canBeBackground = _canBeBackground!;
      Boolean[] canBeColor = _canBeColor!;
      Int32[] nonBgRunRight = _nonBgRunRight!;

      // 1. Forward DP
      ComputeForwardDP(line, hints, forward);
      if (!forward[K][N])
      {
        return null; // No valid placement exists — contradiction
      }

      // 2. Backward DP
      ComputeBackwardDP(line, hints, backward, nonBgRunRight);

      // 3. Compute canBeBackground: cell j can be Background if there's a valid split
      Array.Clear(canBeBackground, 0, N);
      for (Int32 j = 0; j < N; j++)
      {
        if (line[j] == CellValue.Color)
        {
          continue;
        }
        for (Int32 i = 0; i <= K; i++)
        {
          if (forward[i][j] && backward[i][j + 1])
          {
            canBeBackground[j] = true;
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
          if (nonBgRunRight[s] < c)
          {
            continue;
          }

          // Check before: first i hints fit before position s with separator
          Boolean beforeOK;
          if (s == 0)
          {
            beforeOK = forward[i][0];
          }
          else
          {
            beforeOK = line[s - 1] != CellValue.Color && forward[i][s - 1];
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
            afterOK = backward[i + 1][N];
          }
          else
          {
            afterOK = line[afterHint] != CellValue.Color && backward[i + 1][afterHint + 1];
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
        if (!canBeBackground[j] && !canBeColor[j])
        {
          return null; // Contradiction — cell can be neither
        }
        if (canBeColor[j] && !canBeBackground[j])
        {
          deductions[j] = CellValue.Color;
        }
        else if (canBeBackground[j] && !canBeColor[j])
        {
          deductions[j] = CellValue.Background;
        }
      }

      return deductions;
    }

  }
}
