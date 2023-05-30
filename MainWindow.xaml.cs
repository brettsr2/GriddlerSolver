using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.IO;

using System.Net.Http;
using System.Collections.Generic;
using Windows.System;

namespace Griddler_Solver
{
  public partial class MainWindow : Window
  {
    private Solver _Solver
    { get; set; } = new Solver();

    public MainWindow()
    {
      InitializeComponent();
    }

    private void Draw()
    {
      canvas.Children.Clear();
      _Solver.Draw(canvas);
    }

    private void OnSolve_Click(object sender, RoutedEventArgs e)
    {
      /*_Solver = new Solver();

      Int32 black = 1;

      // rows
      ListSingleDefinition list = new ListSingleDefinition();
      list.AddDefinition(black, 3);
      _Solver.AddSingleDefinitionRow(list);

      list = new ListSingleDefinition();
      list.AddDefinition(black, 1);
      list.AddDefinition(black, 1);
      _Solver.AddSingleDefinitionRow(list);

      list = new ListSingleDefinition();
      list.AddDefinition(black, 2);
      list.AddDefinition(black, 2);
      _Solver.AddSingleDefinitionRow(list);

      list = new ListSingleDefinition();
      _Solver.AddSingleDefinitionRow(list);

      list = new ListSingleDefinition();
      list.AddDefinition(black, 5);
      _Solver.AddSingleDefinitionRow(list);

      // columns
      list = new ListSingleDefinition();
      list.AddDefinition(black, 1);
      list.AddDefinition(black, 1);
      _Solver.AddSingleDefinitionCol(list);

      list = new ListSingleDefinition();
      list.AddDefinition(black, 3);
      list.AddDefinition(black, 1);
      _Solver.AddSingleDefinitionCol(list);

      list = new ListSingleDefinition();
      list.AddDefinition(black, 1);
      list.AddDefinition(black, 1);
      _Solver.AddSingleDefinitionCol(list);

      list = new ListSingleDefinition();
      list.AddDefinition(black, 3);
      list.AddDefinition(black, 1);
      _Solver.AddSingleDefinitionCol(list);

      list = new ListSingleDefinition();
      list.AddDefinition(black, 1);
      list.AddDefinition(black, 1);
      _Solver.AddSingleDefinitionCol(list);

      _Solver.Solve();
      Draw();*/
    }

    private async void OnButtonGo_Click(object sender, RoutedEventArgs e)
    {
      _Solver = new Solver();

      // https://www.griddlers.net/cs_CZ/nonogram/-/g/183521
      String url = textBlockUrl.Text;

      Int32 pos = url.LastIndexOf('/') + 1;
      String id = url.Substring(pos);
      url = url.Substring(0, pos);

      // https://www.griddlers.net/cs_CZ/nonogram/-/g/t1685312821998/i01?p_p_lifecycle=2&p_p_resource_id=griddlerPuzzle&p_p_cacheability=cacheLevelPage&_gpuzzles_WAR_puzzles_id=183521&_gpuzzles_WAR_puzzles_lite=false
      url += $"t1679057429974/i01?p_p_lifecycle=2&p_p_resource_id=griddlerPuzzle&p_p_cacheability=cacheLevelPage&_gpuzzles_WAR_puzzles_id={id}&_gpuzzles_WAR_puzzles_lite=false";

      HttpClient webClient = new HttpClient();

      using HttpResponseMessage response = await webClient.GetAsync(url);
      response.EnsureSuccessStatusCode();
      String js = await response.Content.ReadAsStringAsync();

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
        foreach (var items in listItems)
        {
          ListSingleDefinition list = new ListSingleDefinition();
          foreach (var item in items)
          {
            list.AddDefinition(item[0], item[1]);
          }
          if (isRow)
          {
            _Solver.AddSingleDefinitionRow(list);
          }
          else
          {
            _Solver.AddSingleDefinitionCol(list);
          }
        }
      };

      parseJsonInput(true, jsonPuzzle.leftHeader);
      parseJsonInput(false, jsonPuzzle.topHeader);

      _Solver.ListSolidColorBrush = jsonPuzzle.GetListSolidColorBrush();
      _Solver.Solve();

      Draw();
    }
  }
}
