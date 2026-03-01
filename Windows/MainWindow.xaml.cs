using Griddler_Solver.Windows;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Griddler_Solver
{
  public partial class MainWindow : Window, IProgress
  {
    private String _FileDialogFilter = "JSON file (*.json)|*.json";

    private ProgressWindow? _ProgressWindow = null;
    private DispatcherTimer? _refreshTimer;
    private Stopwatch? _solveStopwatch;

    private Solver _Solver
    { get; set; } = new();

    private Boolean SettingComboBox
    { get; set; }

    const String UI_SETTINGS = "UISettings";
    Configuration _AppConfig = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

    public MainWindow()
    {
      InitializeComponent();

      Left = (SystemParameters.PrimaryScreenWidth - 2 * Width) / 2;
      Top = (SystemParameters.PrimaryScreenHeight - Height) / 2;

      if (_AppConfig.Sections[UI_SETTINGS] == null)
      {
        _AppConfig.Sections.Add(UI_SETTINGS, new UISetting());
      }

      DataContext = _AppConfig.Sections[UI_SETTINGS];

#if DEBUG
      Title += " - DEBUG";
#else
      Title += " - RELEASE";
#endif
      comboBoxUrl.Items.Add("Snoopy [20x15x2] 517 | https://www.griddlers.net/nonogram/-/g/183521");
      comboBoxUrl.Items.Add("Buddha [36x35x2] 42 337 | https://www.griddlers.net/nonogram/-/g/268876");
      comboBoxUrl.Items.Add("Tree in a Vase [40x65x2] 170 266 | https://www.griddlers.net/nonogram/-/g/276087");
      comboBoxUrl.Items.Add("Unicorn [35x40x2] 178 714 | https://www.griddlers.net/nonogram/-/g/276577");
      comboBoxUrl.Items.Add("Drummer [65x55x2] 693 345 | https://www.griddlers.net/nonogram/-/g/275868");
      comboBoxUrl.Items.Add("In the Countryside [50x50x2] 709 242 | https://www.griddlers.net/nonogram/-/g/275956");
      comboBoxUrl.Items.Add("Family portrait [40x40x2] ~1.1M | https://www.griddlers.net/nonogram/-/g/276558");
      comboBoxUrl.Items.Add("Pazi [100x100x2] ~2.3M | https://www.griddlers.net/nonogram/-/g/236707");
      comboBoxUrl.Items.Add("Smiling Boy [90x100x2] ~9.5M | https://www.griddlers.net/nonogram/-/g/237830");
      comboBoxUrl.Items.Add("Boat [50x50x2] ~21M | https://www.griddlers.net/nonogram/-/g/116627");
      comboBoxUrl.Items.Add("George W. Bush [88x99x2] ~42.5M | https://www.griddlers.net/nonogram/-/g/193462");
      comboBoxUrl.Items.Add("Snow Tiger [100x100x2] ~90M | https://www.griddlers.net/nonogram/-/g/166197");
      comboBoxUrl.Items.Add("RTM's Tigers [100x100x2] ~215M | https://www.griddlers.net/nonogram/-/g/293872");
      comboBoxUrl.Items.Add("King Under the Mountain [100x100x2] ~428M | https://www.griddlers.net/nonogram/-/g/278786");
      comboBoxUrl.Items.Add("Fierce Indian Chief ~617M | https://www.griddlers.net/nonogram/-/g/290035");
      comboBoxUrl.Items.Add("Angry Indian Chief [100x100x2] ~820M | https://www.griddlers.net/nonogram/-/g/290304");
      comboBoxUrl.Items.Add("Deadly Indian Chief [100x100x2] ~1G | https://www.griddlers.net/nonogram/-/g/290307");
    }
    private void Draw()
    {
      List<String> parts = new List<String>();

      parts.Add($"Name: {_Solver.Name}");
      Int32 countColors = _Solver.ListColors.Count > 0 ? _Solver.ListColors.Count - 1 : 0;
      parts.Add($"Size: [{_Solver.Board.ColumnCount}x{_Solver.Board.RowCount}x{countColors}]");
      if (_Solver.Board.IsSolved)
      {
        parts.Add($"Time elapsed: {_Solver.Board.TimeTaken.ToString(Solver.TimeFormat)}");
      }

      label.Content = String.Join(" | ", parts);

      boardCanvas.SetSolver(_Solver);
    }

    private void OnButtonSolve_Click(object sender, RoutedEventArgs e)
    {
      Config config = new Config()
      {
        Name = Name,
        Progress = this,
        MultithreadEnabled = checkBoxMultithread.IsChecked == true,
      };

      IsEnabled = false;

      _ProgressWindow?.Close();
      _ProgressWindow = new(this);
      _ProgressWindow.Title = $"Solving: {_Solver.Name}";
      _ProgressWindow.Width = Width;
      _ProgressWindow.Height = Height;

      Rect rect = VisualTreeHelper.GetDescendantBounds(this);
      _ProgressWindow.Left = Left + rect.Width;
      _ProgressWindow.Top = Top;

      _ProgressWindow.Show();

      _solveStopwatch = Stopwatch.StartNew();
      _refreshTimer = new DispatcherTimer();
      _refreshTimer.Interval = TimeSpan.FromSeconds(1);
      _refreshTimer.Tick += OnRefreshTimer_Tick;
      _refreshTimer.Start();

      Task.Run(() =>
      {
        _Solver.Solve(config);
        Completed();
      });
    }

    private void OnButtonDownload_Click(object sender, RoutedEventArgs e)
    {
      String[] split = comboBoxUrl.Text.Split('|');
      String url = split.Length == 1 ? split[0].Trim() : split[1].Trim();

      HttpClient httpClient = new();
      String http = httpClient.GetStringAsync(url).Result;

      String title = "<meta property=\"og:title\" content=\"";

      Int32 posBeg = http.IndexOf(title) + title.Length;
      Int32 posEnd = http.IndexOf("\"", posBeg);
      String name = http.Substring(posBeg, posEnd - posBeg);

      Int32 pos = url.LastIndexOf('/') + 1;
      String id = url.Substring(pos);

      url = url.Substring(0, pos);

      // https://www.griddlers.net/cs_CZ/nonogram/-/g/t1685312821998/i01?p_p_lifecycle=2&p_p_resource_id=griddlerPuzzle&p_p_cacheability=cacheLevelPage&_gpuzzles_WAR_puzzles_id=183521&_gpuzzles_WAR_puzzles_lite=false
      url += $"t1679057429974/i01?p_p_lifecycle=2&p_p_resource_id=griddlerPuzzle&p_p_cacheability=cacheLevelPage&_gpuzzles_WAR_puzzles_id={id}&_gpuzzles_WAR_puzzles_lite=false";

      String js = httpClient.GetStringAsync(url).Result;
      String puzzle = "var puzzle = ";

      pos = js.IndexOf(puzzle);
      pos += puzzle.Length;

      String solution = "var solution = ";
      Int32 pos2 = js.IndexOf(solution, pos);

      puzzle = js.Substring(pos, pos2 - pos);

      Json.Puzzle? jsonPuzzle = Newtonsoft.Json.JsonConvert.DeserializeObject<Json.Puzzle>(puzzle);
      if (jsonPuzzle == null)
      {
        return;
      }

      static Hint[][] ParseJsonInput(List<List<List<Int32>>> listItems)
      {
        Hint[][] hints = new Hint[listItems.Count][];
        for (Int32 index = 0; index < listItems.Count; index++)
        {
          List<Hint> listHint = new List<Hint>();

          foreach (var item in listItems[index])
          {
            listHint.Add(new Hint() { ColorId = item[0], Count = item[1] });
          }

          hints[index] = listHint.ToArray();
        }

        return hints;
      }

      var hintsRow = ParseJsonInput(jsonPuzzle.leftHeader);
      var hintsColumn = ParseJsonInput(jsonPuzzle.topHeader);
      _Solver = new Solver(hintsRow, hintsColumn)
      {
        Name = name,
        Url = comboBoxUrl.Text,
        ListColors = jsonPuzzle.GetListSolidColorBrush()
      };

      Draw();
    }
    private void OnButtonSave_Click(object sender, RoutedEventArgs e)
    {
      SaveFileDialog fileDialog = new()
      {
        Filter = _FileDialogFilter,
        FileName = _Solver.Name + ".json"
      };

      if (fileDialog.ShowDialog() == true)
      {
        JsonSerializerOptions options = new JsonSerializerOptions
        {
          IgnoreReadOnlyProperties = true,
          WriteIndented = true
        };

        String json = JsonSerializer.Serialize(_Solver, options);
        File.WriteAllText(fileDialog.FileName, json);
      }
    }
    private void OnButtonLoad_Click(object sender, RoutedEventArgs e)
    {
      OpenFileDialog fileDialog = new OpenFileDialog();
      fileDialog.Filter = _FileDialogFilter;

      if (fileDialog.ShowDialog() == true)
      {
        String json = File.ReadAllText(fileDialog.FileName);
        _Solver = JsonSerializer.Deserialize<Solver>(json)!;

        SettingComboBox = true;
        comboBoxUrl.Text = _Solver.Url;
        SettingComboBox = false;

        Draw();
      }
    }
    private void OnButtonClearBoard_Click(object sender, RoutedEventArgs e)
    {
      _Solver.Clear();
      Draw();
    }

    private void OnCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
      Draw();
    }
    private void OnComboBoxUrl_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
      if (!SettingComboBox)
      {
        _Solver = new Solver();
      }
      Draw();
    }

    public void AddMessage(String message)
    {
      Dispatcher.Invoke(new Action(() =>
      {
        if (_ProgressWindow != null)
        {
          _ProgressWindow.textBoxOutput.AppendText(message + Environment.NewLine);
          _ProgressWindow.textBoxOutput.ScrollToEnd();
        }
      }));
    }

    public void Completed()
    {
      Dispatcher.Invoke(new Action(() =>
      {
        _refreshTimer?.Stop();
        _refreshTimer = null;
        _solveStopwatch = null;

        AddMessage($"Time elapsed: {_Solver.Board.TimeTaken.ToString(Solver.TimeFormat)}");
        AddMessage($"End");

        IsEnabled = true;

        Draw();
      }));
    }
    public void ProgressWindowClosed()
    {
      _Solver.Config.Break = true;
      _refreshTimer?.Stop();
    }

    private void OnRefreshTimer_Tick(object? sender, EventArgs e)
    {
      if (_solveStopwatch == null)
      {
        return;
      }

      Int32 solvedCount = 0;
      Int32 total = _Solver.Board.RowCount * _Solver.Board.ColumnCount;

      for (Int32 row = 0; row < _Solver.Board.RowCount; row++)
      {
        for (Int32 col = 0; col < _Solver.Board.ColumnCount; col++)
        {
          if (_Solver.Board[row, col] != CellValue.Unknown)
          {
            solvedCount++;
          }
        }
      }

      Int32 percentSolved = total > 0 ? solvedCount * 100 / total : 0;
      String elapsed = _solveStopwatch.Elapsed.ToString(Solver.TimeFormat);

      AddMessage($"[{elapsed}] Solved: {solvedCount}({percentSolved}%)");
      Draw();
    }

    private void OnWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
      _ProgressWindow?.Close();
      _AppConfig.Save();
    }

    private void OnCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
      Double originX = _Solver.MaxHintsCountRow * _Solver.CellSize;
      Double originY = _Solver.MaxHintsCountColumn * _Solver.CellSize;

      Point point = e.GetPosition(boardCanvas);
      Double x = point.X - originX;
      Double y = point.Y - originY;

      Int32 column = (Int32)(x / _Solver.CellSize) + 1;
      Int32 row = (Int32)(y / _Solver.CellSize) + 1;
      labelCoordinates.Content = $"Row: {row}, Column: {column}";
    }
  }
}
