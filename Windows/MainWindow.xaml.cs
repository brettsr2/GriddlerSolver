using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Griddler_Solver.Windows;
using Microsoft.Win32;

using NonogramSolver;

namespace Griddler_Solver
{
  public partial class MainWindow : Window
  {
    private String _FileDialogFilter = "JSON file (*.json)|*.json";

    private Solver? _Solver
    { get; set; } = new Solver();

    public MainWindow()
    {
      InitializeComponent();

      comboBoxUrl.Items.Add("https://www.griddlers.net/cs_CZ/nonogram/-/g/183521");
      comboBoxUrl.Items.Add("https://www.griddlers.net/cs_CZ/nonogram/-/g/276577");
    }

    private void Draw()
    {
      canvas.Children.Clear();
      _Solver?.Draw(canvas);
    }

    private void OnSolve_Click(object sender, RoutedEventArgs e)
    {
      Int32[][] rows = _Solver!.Rows!;
      Int32[][] cols = _Solver!.Cols!;

      ProgressWindow progressWindow = new();
      progressWindow.Show();

      Task.Run(() =>
      {
          Nonogram nonogram = new Nonogram(rows, cols, progressWindow);
          _Solver.Result = nonogram.Solve();
      });

      Draw();
    }

    private void OnButtondDownload_Click(object sender, RoutedEventArgs e)
    {
      _Solver = new Solver();
      String url = _Solver.Url = comboBoxUrl.Text;

      Int32 pos = url.LastIndexOf('/') + 1;
      String id = url.Substring(pos);
      url = url.Substring(0, pos);

      // https://www.griddlers.net/cs_CZ/nonogram/-/g/t1685312821998/i01?p_p_lifecycle=2&p_p_resource_id=griddlerPuzzle&p_p_cacheability=cacheLevelPage&_gpuzzles_WAR_puzzles_id=183521&_gpuzzles_WAR_puzzles_lite=false
      url += $"t1679057429974/i01?p_p_lifecycle=2&p_p_resource_id=griddlerPuzzle&p_p_cacheability=cacheLevelPage&_gpuzzles_WAR_puzzles_id={id}&_gpuzzles_WAR_puzzles_lite=false";

      String js = new HttpClient().GetStringAsync(url).Result;
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

      Action<Boolean, List<List<List<Int32>>>> parseJsonInput = (isRow, listItems) =>
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

        if (isRow)
        {
          _Solver.HintsRow = hints;
        }
        else
        {
          _Solver.HintsColumn = hints;
        }
      };

      parseJsonInput(true, jsonPuzzle.leftHeader);
      parseJsonInput(false, jsonPuzzle.topHeader);

      _Solver.ListColors = jsonPuzzle.GetListSolidColorBrush();

      Draw();
    }
    private void OnButtonSave_Click(object sender, RoutedEventArgs e)
    {
      SaveFileDialog fileDialog = new();
      fileDialog.Filter = _FileDialogFilter;
      fileDialog.FileName = "Nonogram.json";

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
        _Solver = JsonSerializer.Deserialize<Solver>(json);

        Draw();
      }
    }

    private void OnCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
      Draw();
    }
  }
}
