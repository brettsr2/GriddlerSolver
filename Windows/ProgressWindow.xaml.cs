using System.Windows;

namespace Griddler_Solver.Windows
{
  partial class ProgressWindow : Window
  {
    IProgress? _Progress = null;

    internal ProgressWindow(IProgress progress)
    {
      _Progress = progress;
      InitializeComponent();
    }

    private void Window_Closed(object sender, System.EventArgs e)
    {
      _Progress?.ProgressWindowClosed();
    }

    private void OnButtonCancel_Click(object sender, RoutedEventArgs e)
    {
      _Progress?.ProgressWindowClosed();
    }
  }
}
