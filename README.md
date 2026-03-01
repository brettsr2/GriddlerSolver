# GriddlerSolver

WPF nonogram (griddler) solver v C# (.NET 8, Windows).
Stahuje a automaticky lusti puzzly z [griddlers.net](https://www.griddlers.net).

---

## Architektura solveru

Solver spusti `ProcessWorkQueue` — reaktivni multi-thread fixni-bod algoritmus.
Vsechny radky/sloupce zacnou jako dirty. Workery je zpracovavaji pomoci
`LineOverlap.Solve` (Forward+Backward DP), propaguji zmeny pres dirty flags
na krizove linie a opakuji, dokud se fronta nevyprazdni.

```
  Solver.Solve()
     |
     v
  ProcessWorkQueue
     |
     +---> N worker threadu (= pocet CPU jader)
     |        |
     |        +---> Dequeue dirty radek/sloupec
     |        |
     |        +---> SolverLine.Solve
     |        |        |
     |        |        +---> LineOverlap.Solve (Forward+Backward DP)
     |        |
     |        +---> Board.MergeRow/Column
     |        |        |
     |        |        +---> dirty flags na krizovych liniich
     |        |        |
     |        |        +---> re-enqueue dirty linii
     |        |
     |        +---> opakuj dokud fronta neni prazdna
     |
     +---> drainComplete → workersExited → navrat
     |
     v
  Board.IsSolved?  ano → hotovo  /  ne → "Solver stuck."
```

---

## LineOverlap DP — jadro solveru — O(N*K)

Pro kazdy radek/sloupec vola `LineOverlap.Solve()`,
ktery najde **vsechny vynucene bunky** pomoci dynamickeho programovani.

### Krok 1: TryFitLeft / TryFitRight

Nalezne nejlevejsi a nejpravejsi mozne umisteni kazdeho hintu.

```
Radek delky 10, hinty [3, 2]:

TryFitLeft  (nejlevejsi):  hint0 na pozici 0, hint1 na pozici 4
TryFitRight (nejpravejsi): hint0 na pozici 5, hint1 na pozici 8 (= 10-2)

  pozice:  0  1  2  3  4  5  6  7  8  9
  left:   [###][.][##][.][.][.][.][.][.][.]     hint0@0, hint1@4
  right:  [.][.][.][.][.][###][.][.][##][.]     hint0@5, hint1@8
                                                (. = cokoliv, # = Color)
```

### Krok 2: Overlap — vynucene bunky

Kde se levy a pravy rozsah prekryvaji, tam **musi** byt Color:

```
  Pro hint [7] na radku delky 10:

  pozice:  0  1  2  3  4  5  6  7  8  9
  left:   [#  #  #  #  #  #  #][.][.][.]        hint@0
  right:  [.][.][.][#  #  #  #  #  #  #]        hint@3

  prekryv: pozice 3..6 → MUSI byt Color
  vysledek:[?][?][?][#][#][#][#][?][?][?]
```

### Krok 3: Pinned hinty a nedosazitelne bunky

- **Pinned hint**: pokud `leftStart == rightStart`, hint ma jedinou pozici
  → vsechny jeho bunky = Color, separatory pred/za = Background
- **Nedosazitelne bunky**: bunka mimo rozsah vsech hintu → Background

### Krok 4: Forward+Backward DP — O(N*K)

Nejsilnejsi cast. Spocita DP tabulky ktery urcuji, co kazda bunka **muze byt**.

```
Definice:
  F[i][j] = "prvnich i hintu se vejde do bunek 0..j-1"    (Forward)
  B[i][j] = "hinty i..K-1 se vejdou do bunek j..N-1"      (Backward)

Prechody pro Forward:
  Moznost A: bunka j-1 je mezera (Background)
    F[i+1][j] = true  pokud  F[i+1][j-1] && line[j-1] != Color

  Moznost B: hint i umisten na pozice (j-c)...(j-1)
    F[i+1][j] = true  pokud  span neobsahuje Background
                              && pred spanem neni Color
                              && F[i][start-1] == true
```

Backward DP je zrcadlovy — skenuje zprava doleva.

#### Urceni vynucenych bunek

```
canBeBg[j]    = existuje rozdeleni kde j je Background
              = existuje i: F[i][j] && B[i][j+1]

canBeColor[j] = existuje platne umisteni hintu pokryvajici j
              = existuje hint i a pozice s: span je validni,
                F[i][s-1] && B[i+1][s+c+1]
```

Vysledek pro kazdou Unknown bunku:

```
  canBeColor && !canBeBg  →  musi byt Color
  canBeBg && !canBeColor  →  musi byt Background
  oba true                →  zatim neurceno
  oba false               →  KONTRADIKCE (puzzle nema reseni)
```

### Priklad DP na konkretnim radku

```
Radek delky 8, hint [3]:   [.][#][?][?][?][?][#][.]

TryFitLeft:   hint@1  (nejdrive kde muze byt — bunka 1 uz je Color)
TryFitRight:  hint@4  (nejpozdeji)

Overlap:      pozice 4..3 → prazdny
Ale DP najde:
  - bunka 1 uz je Color a musi byt soucasti hintu
  - hint delky 3 pokryvajici bunku 1 muze byt na pozicich 0,1,2 nebo 1,2,3
  - bunky 0..3: canBeColor=true
  - bunky 0,3: canBeBg=true take (muze byt separator)
  - bunky 1,2: canBeBg=false (uz jsou Color nebo hint je musi pokryt)
  → bunky 1,2 MUSI byt Color

Vysledek:  [?][#][#][?][?][?][#][?]
```

---

## ProcessWorkQueue — reaktivni multi-thread

Paralelni zpracovani radku/sloupcu s reaktivni propagaci zmen.

```
+------------------------------------------------------------------+
|                      ProcessWorkQueue                             |
|                                                                   |
|  1. Naplneni fronty:                                             |
|     vsechny dirty + unsolved radky/sloupce → ConcurrentQueue     |
|                                                                   |
|  2. Worker thready (N = pocet CPU jader):                        |
|                                                                   |
|     +----------+  +----------+  +----------+                     |
|     | Worker 1 |  | Worker 2 |  | Worker N |                     |
|     +----+-----+  +----+-----+  +----+-----+                     |
|          |              |              |                          |
|          v              v              v                          |
|     +---------+    +---------+    +---------+                    |
|     | Dequeue |    | Dequeue |    | Dequeue |                    |
|     +---------+    +---------+    +---------+                    |
|          |              |              |                          |
|          v              v              v                          |
|     +---------+    +---------+    +---------+                    |
|     |  Solve  |    |  Solve  |    |  Solve  |   LineOverlap.Solve|
|     +---------+    +---------+    +---------+                    |
|          |              |              |                          |
|          v              v              v                          |
|     +---------+    +---------+    +---------+                    |
|     |  Merge  |    |  Merge  |    |  Merge  |   Board.MergeRow/  |
|     +---------+    +---------+    +---------+   MergeColumn      |
|          |              |              |                          |
|          v              v              v                          |
|     Merge oznaci krizove radky/sloupce jako dirty                |
|     → dirty krizove linie se znovu enqueue                       |
|                                                                   |
|  3. Fronta se vyprezdni → drainComplete.Set()                    |
|     Vsechny workery skonci → workersExited.Signal()              |
+------------------------------------------------------------------+
```

### Dirty propagace

Kdyz worker vyresi radek 5 a zmeni bunku `[5, 10]`:
- `Board.MergeRow` zapise zmenu a oznaci **sloupec 10** jako dirty
- Worker re-enqueue sloupec 10 do fronty
- Jiny worker vyresi sloupec 10, zmeni bunku `[3, 10]`
- `Board.MergeColumn` oznaci **radek 3** jako dirty
- Radek 3 se re-enqueue...

Tento retezovy efekt propaguje informaci pres celou desku,
dokud se fronta nevyprazdni (zadne dalsi zmeny).

### Synchronizace

| Mechanismus | Ucel |
|---|---|
| `ConcurrentQueue<SolverLine>` | Thread-safe fronta prace |
| `ConcurrentDictionary<LineKey, byte>` | Prevence duplicitniho enqueue |
| `Interlocked.Increment/Decrement` | Atomicke pocitadlo pending items |
| `ManualResetEventSlim` | Signal ze fronta je prazdna |
| `CountdownEvent` | Signal ze vsechny workery skoncily |
| `Board._Lock` | Mutex pro GetRow/MergeRow operace |
| `Volatile.Read/Write` | Dirty flags viditelne vsem threadum |
| `Config.Break` (volatile) | Cancel signal — kontrolovan ve vsech smyckach |

---

## Optimalizace

### 1. Forward+Backward DP misto enumerace permutaci

Puvodne solver generoval vsechny mozne permutace hintu na radku (exponencialni).
Nyni pouziva O(N*K) dynamicke programovani, ktere v jednom pruchodu
najde **vsechny vynucene bunky**.

```
Permutace:    O(C(N,K)) — exponencialni, pro 100x100 nezvladnutelne
DP solver:    O(N*K)    — linearni, resi i 100x100 okamzite
```

### 2. ThreadStatic DP tabulky

DP tabulky (`F[][]`, `B[][]`, `canBeBg[]`, `canBeColor[]`) jsou alokovany
jednou per thread a znovupouzity pro vsechny volani. Tim se eliminuje
tlak na garbage collector pri stovkach volani za sekundu.

```csharp
[ThreadStatic] private static Boolean[][]? _tls_F;
[ThreadStatic] private static Boolean[][]? _tls_B;
```

### 3. Dirty flags — lazy evaluation

Board udrzuje pole `_DirtyRows[]` a `_DirtyColumns[]`. Kdyz se zmeni bunka,
oznaci se **jen** prislusny radek a sloupec. ProcessWorkQueue zpracuje
**jen dirty linie**, coz dramaticky snizuje praci.

### 4. Version counter — O(1) detekce zaseku

`Int32 _Version` counter se atomicky inkrementuje pri kazde zmene bunky.
Pokud se po ProcessWorkQueue Board.IsSolved == false, solver hlasi "stuck".

### 5. Rendering: jeden OnRender pass

`BoardCanvas` dedi z `FrameworkElement` a prepisuje `OnRender(DrawingContext)`.
Kresli **vsechny bunky, hinty a cary v jedinem pruchodu** pomoci
`dc.DrawRectangle`, `dc.DrawLine`, `dc.DrawText`.

### 6. Frozen Pen objekty

Pera pro kresleni mrize (`_penGrey`, `_penBlack`) jsou vytvorena
v konstruktoru a zmrazena (`Freeze()`). Frozen objekty WPF nevyzaduji
synchronizaci a jsou rychlejsi pro rendering.

---

## Struktura souboru

```
GriddlerSolver/
|
+-- Src/
|   +-- Solver.cs            Orchestrace solveru, ProcessWorkQueue
|   +-- SolverLine.cs        Obalka pro reseni jednoho radku/sloupce
|   +-- LineOverlap.cs       DP solver (TryFit, Forward+Backward DP)
|   +-- Board.cs             Stav desky, dirty tracking, version counter
|   +-- Config.cs            Konfigurace (Break, threading)
|   +-- Hint.cs              Datova trida pro hint (ColorId, Count)
|   +-- Enums.cs             CellValue enum
|   +-- PuzzleColors.cs      Paleta barev s lazy SolidColorBrush
|   +-- UISetting.cs         Perzistence UI nastaveni
|   +-- Json.cs              Parser JSON z griddlers.net
|
+-- Windows/
|   +-- MainWindow.xaml/.cs   Hlavni okno, Solve/Download/Load/Save
|   +-- BoardCanvas.cs        Custom renderer (OnRender)
|   +-- ProgressWindow.xaml   Okno prubehu s Cancel tlacitkem
|
+-- Interfaces/
    +-- IProgress.cs          Rozhrani pro zpetnou vazbu (AddMessage, Completed)
```

---

## Interakce komponent

```
  Uzivatel
     |
     | klik "Solve"
     v
  MainWindow ──────> Config (nastaveni z UI)
     |
     | Task.Run (background thread)
     v
  Solver.Solve(config)
     |
     +---> ProcessWorkQueue
              |
              +---> N worker threadu
              |        |
              |        +---> SolverLine.Solve
              |        |        |
              |        |        +---> LineOverlap.Solve (DP)
              |        |
              |        +---> Board.MergeRow/Column
              |                  |
              |                  +---> dirty flags na krizovych liniich
              |                  |
              |                  +---> re-enqueue dirty linii
              |
              +---> drainComplete → workersExited → navrat
     |
     v
  Completed() → MainWindow.Draw() → BoardCanvas.OnRender()
```
