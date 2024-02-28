using System;
using System.Collections.Generic;
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

    private ProgressWindow? _ProgressWindow = null;

    private Solver _Solver
    { get; set; } = new();

    public MainWindow()
    {
      InitializeComponent();

      comboBoxUrl.Items.Add("https://www.griddlers.net/nonogram/-/g/183521");
      comboBoxUrl.Items.Add("https://www.griddlers.net/nonogram/-/g/276577");
      comboBoxUrl.Items.Add("https://www.griddlers.net/nonogram/-/g/276558");
      comboBoxUrl.Items.Add("https://www.griddlers.net/nonogram/-/g/116627");
    }
    private void Draw()
    {
      label.Content = $"{_Solver.Name}{Environment.NewLine}[{_Solver.HintsRowCount},{_Solver.HintsColumnCount}]";

      canvas.Children.Clear();
      _Solver.Draw(canvas);
    }

    private void OnSolve_Click(object sender, RoutedEventArgs e)
    {
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
      String url = comboBoxUrl.Text;

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

        Draw();
      }
    }

    private void OnCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
      Draw();
    }

    private void OnComboBoxUrl_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
      _Solver = new Solver();
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
        AddMessage($"Iterations: {_Solver.Result.Iterations}, Time elapsed: {_Solver.Result.TimeTaken.ToString(@"mm\:ss\.ff")}");

        if (!_Solver.Break)
        {
          MessageBox.Show("Done", String.Empty, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        _ProgressWindow?.Close();
        _ProgressWindow = null;

        Draw();
      }));
    }
    public void ProgressWindowClosed()
    {
      _Solver.Break = true;
    }
  }
}
