using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

using Griddler_Solver.Windows;

using Microsoft.Win32;

namespace Griddler_Solver
{
  public partial class MainWindow : Window, IProgress
  {
    private String _FileDialogFilter = "JSON file (*.json)|*.json";

#if DEBUG
    private String _LogFile = "LogFile.txt";
    private Object _LogFileLock = new Object();
#endif

    private ProgressWindow? _ProgressWindow = null;

    private Solver _Solver
    { get; set; } = new();

    private Boolean SettingComboBox
    { get; set; }

    public MainWindow()
    {
      InitializeComponent();

      comboBoxUrl.Items.Add("Buddha [36x35x2] 42337 | https://www.griddlers.net/nonogram/-/g/268876");
      comboBoxUrl.Items.Add("Snoopy [20x15x2] 517 | https://www.griddlers.net/nonogram/-/g/183521");
      comboBoxUrl.Items.Add("Tree in a Vase [40x65x2] 170266 | https://www.griddlers.net/nonogram/-/g/276087");
      comboBoxUrl.Items.Add("Unicorn [35x40x2] 178714 | https://www.griddlers.net/nonogram/-/g/276577");
      comboBoxUrl.Items.Add("In the Countryside [50x50x2] ~0.7M | https://www.griddlers.net/nonogram/-/g/275956");
      comboBoxUrl.Items.Add("Family portrait [40x40x2] ~1.1M | https://www.griddlers.net/nonogram/-/g/276558");
      comboBoxUrl.Items.Add("Drummer [65x55x2] 693345 | https://www.griddlers.net/nonogram/-/g/275868");
      comboBoxUrl.Items.Add("Boat [50x50x2] ~21M | https://www.griddlers.net/nonogram/-/g/116627");
    }
    private void Draw()
    {
      StringBuilder stringBuilder = new StringBuilder();

      stringBuilder.AppendLine($"{_Solver.Name}");
      stringBuilder.AppendLine($"[{_Solver.Board.HintsColumnCount}x{_Solver.Board.HintsRowCount}x{_Solver.ListColors.Count - 1}]");
      if (_Solver.Board.IsSolved)
      {
        stringBuilder.AppendLine($"{_Solver.Board.Iterations}");
        stringBuilder.AppendLine($"{_Solver.Board.TimeTaken.ToString(Solver.TimeFormat)}");
      }

      label.Content = stringBuilder.ToString();

      canvas.Children.Clear();
      _Solver.Draw(canvas);
    }

    private void OnSolve_Click(object sender, RoutedEventArgs e)
    {
#if DEBUG
      if (File.Exists(_LogFile))
      {
        File.Delete(_LogFile);
      }
#endif

      _ProgressWindow = new(this);
      _ProgressWindow.Show();

      Task.Run(() =>
      {
        _Solver.Solve(this);
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

      static Hint[][] ParseJsonInput(List<List<List<int>>> listItems)
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
      var hintsColumn= ParseJsonInput(jsonPuzzle.topHeader);
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
      AddDebugMessage(message);

      Dispatcher.Invoke(new Action(() =>
      {
        if (_ProgressWindow != null)
        {
          _ProgressWindow.textBoxOutput.AppendText(message + Environment.NewLine);
          _ProgressWindow.textBoxOutput.ScrollToEnd();

          Draw();
        }
      }));
    }
    public void AddDebugMessage(String message)
    {
#if DEBUG
      lock(_LogFileLock)
      {
        File.AppendAllText(_LogFile, message + Environment.NewLine);
      }
#endif
    }
    public void Completed()
    {
      Dispatcher.Invoke(new Action(() =>
      {
        AddMessage($"Iterations: {_Solver.Board.Iterations}, Time elapsed: {_Solver.Board.TimeTaken.ToString(Solver.TimeFormat)}");
        AddMessage($"End");

        _ProgressWindow = null;

        Draw();
      }));
    }
    public void ProgressWindowClosed()
    {
      _Solver.Config.Break = true;
    }
  }
}
