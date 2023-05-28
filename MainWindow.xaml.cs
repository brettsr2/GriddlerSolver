using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;

using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

using Microsoft.Win32;
using System.Text;
using Tesseract;
using Windows.UI.Xaml.Media;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;

namespace Griddler_Solver
{
  public partial class MainWindow : Window
  {
    private Solver _Solver
    { get; set; } = new Solver();

    private BitmapImage? InputImage
    { get; set; }

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
      _Solver = new Solver();

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
      _Solver.AddSingleDefinitionColumn(list);

      list = new ListSingleDefinition();
      list.AddDefinition(black, 3);
      list.AddDefinition(black, 1);
      _Solver.AddSingleDefinitionColumn(list);

      list = new ListSingleDefinition();
      list.AddDefinition(black, 1);
      list.AddDefinition(black, 1);
      _Solver.AddSingleDefinitionColumn(list);

      list = new ListSingleDefinition();
      list.AddDefinition(black, 3);
      list.AddDefinition(black, 1);
      _Solver.AddSingleDefinitionColumn(list);

      list = new ListSingleDefinition();
      list.AddDefinition(black, 1);
      list.AddDefinition(black, 1);
      _Solver.AddSingleDefinitionColumn(list);

      _Solver.Solve();
      Draw();
    }

    private void OnLoadInput_Click(object sender, RoutedEventArgs e)
    {
      OpenFileDialog openFileDialog = new OpenFileDialog();
      if (openFileDialog.ShowDialog() == true)
      {
        System.Windows.Controls.Image imageControl = new System.Windows.Controls.Image();
        imageControl.Stretch = System.Windows.Media.Stretch.None;

        var image = new BitmapImage(new Uri(openFileDialog.FileName));
        toCCITT(openFileDialog.FileName);

        imageControl.Source = InputImage = image;

        canvas.Children.Clear();
        canvas.Children.Add(imageControl);
      }
    }

    public void toCCITT(string tifURL)
    {
      byte[] imgBits = File.ReadAllBytes(tifURL);

      using (MemoryStream ms = new MemoryStream(imgBits))
      {
        using (Image i = Image.FromStream(ms))
        {
          EncoderParameters parms = new EncoderParameters(1);
          ImageCodecInfo codec = ImageCodecInfo.GetImageDecoders().FirstOrDefault(decoder => decoder.FormatID == System.Drawing.Imaging.ImageFormat.Tiff.Guid);

          parms.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Compression, (long)EncoderValue.CompressionCCITT4);

          i.Save(@"d:\Downloads\result.tif", codec, parms);
        }
      }
    }

    public void InvertColor(Image image)
    {
      Bitmap pic = new Bitmap(image);
      for (int y = 0; (y <= (pic.Height - 1)); y++)
      {
        for (int x = 0; (x <= (pic.Width - 1)); x++)
        {
          System.Drawing.Color inv = pic.GetPixel(x, y);
          inv = System.Drawing.Color.FromArgb(255, (255 - inv.R), (255 - inv.G), (255 - inv.B));
          pic.SetPixel(x, y, inv);
        }
      }
    }
    public void ToGrayScale(Image image)
    {
      Bitmap bmp = new Bitmap(image);

      int rgb;
      System.Drawing.Color c;

      for (int y = 0; y < bmp.Height; y++)
        for (int x = 0; x < bmp.Width; x++)
        {
          c = bmp.GetPixel(x, y);
          rgb = (int)Math.Round(.299 * c.R + .587 * c.G + .114 * c.B);
          bmp.SetPixel(x, y, System.Drawing.Color.FromArgb(rgb, rgb, rgb));
        }
    }

    // https://www.griddlers.net/cs_CZ/nonogram/-/g/183521
    private async void OnCanvas_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
      if (InputImage == null)
      {
        return;
      }

      String currentDirectory = Directory.GetCurrentDirectory();
      String[] arrFiles = Directory.GetFiles(currentDirectory, "*.bmp");
      foreach (String file in arrFiles)
      {
        File.Delete(file);
      }

      Int32 cellSize = 15;

      System.Windows.Point p = Mouse.GetPosition(canvas);

      Int32 left = (Int32)p.X;
      Int32 top = (Int32)p.Y;

      Int32 mainLeft = left / cellSize;
      for (Int32 counter = 1; counter <= mainLeft; counter++)
      {
        Int32 leftNew = left - (counter * cellSize);
        
        CroppedBitmap croppedBitmap = new CroppedBitmap(InputImage, new Int32Rect(leftNew, top, cellSize, cellSize));
        //ToGrayScale(croppedBitmap);

        BmpBitmapEncoder encoder = new BmpBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(croppedBitmap));

        String fileTitle = $"cell_{leftNew:000}.{top:000}.bmp";
        String fileName = Path.Combine(currentDirectory, fileTitle);
        
        FileStream filestream = new FileStream(fileName, FileMode.Create);
        encoder.Save(filestream);

        filestream.Close();

        var ocrtext = string.Empty;
        using (var engine2 = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default))
        {
          using (var img = Pix.LoadFromFile(fileName))
          {
            for (Int32 step = 0; step < 2; step++)
            {
              using (var page = engine2.Process(img))
              {
                ocrtext = page.GetText();
                Console.Write("aaa");
                Console.WriteLine(ocrtext);
                if (ocrtext != String.Empty)
                {
                  int g = 0;
                }
              }
            }
          }
        }

        var engine = OcrEngine.TryCreateFromUserProfileLanguages();
        //var engine = OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en-US"));
        var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(fileName);
        var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read);
        var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
        var softwareBitmap = await decoder.GetSoftwareBitmapAsync();
        var ocrResult = await engine.RecognizeAsync(softwareBitmap);
        Console.WriteLine(ocrResult.Text);
        if (ocrResult.Text != String.Empty)
        {
          int g = 0;
        }


        //Windows.Graphics.Imaging.BitmapDecoder decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(new MemoryStream(memoryStream.ToArray()));
        //SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();

        //var ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
        //var ocrResult = await ocrEngine.RecognizeAsync(softwareBitmap);
        //foreach (var line in ocrResult.Lines)
        {
          //Console.WriteLine(line.Text);
        }

      }


      /*System.Windows.Controls.Image imageControl = new System.Windows.Controls.Image();
      imageControl.Stretch = Stretch.None;
      imageControl.Source = cropped_bitmap;
      canvas.Children.Add(imageControl);

      JpegBitmapEncoder encoder = new JpegBitmapEncoder();
      MemoryStream memoryStream = new MemoryStream();
      BitmapImage bImg = new BitmapImage();

      encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(cropped_bitmap));
      encoder.Save(memoryStream);

      memoryStream.Position = 0;
      bImg.BeginInit();
      bImg.StreamSource = new MemoryStream(memoryStream.ToArray());
      bImg.EndInit();

      //memoryStream.Close();

      {
        Windows.Graphics.Imaging.BitmapDecoder decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(new MemoryStream(memoryStream.ToArray()));
        SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();

        var ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
        var ocrResult = await ocrEngine.RecognizeAsync(softwareBitmap);
        foreach (var line in ocrResult.Lines)
        {
          Console.WriteLine(line.Text);
        }
      }*/
    }

    private void OnButtonGo_Click(object sender, RoutedEventArgs e)
    {
      // https://www.griddlers.net/en_US/nonogram/-/g/t1679057429974/i01?p_p_lifecycle=2&p_p_resource_id=griddlerPuzzle&p_p_cacheability=cacheLevelPage&_gpuzzles_WAR_puzzles_id={id}&_gpuzzles_WAR_puzzles_lite=false
    }
  }
}
