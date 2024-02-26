using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using NonogramSolver;

namespace Griddler_Solver.Windows
{
    /// <summary>
    /// Interaction logic for ProgressWindow.xaml
    /// </summary>
    public partial class ProgressWindow : Window, ILogger
    {
        public ProgressWindow()
        {
            InitializeComponent();
        }

        public void LineSolved(int idx, bool isRow, CellValue[] line)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine(isRow ? "Row " : "Col ");

            foreach (CellValue value in  line)
            {
                stringBuilder.Append(value + ", ");
            }

            Dispatcher.Invoke(new Action(() =>
            {
                textBoxOutput.Text += stringBuilder.ToString() + Environment.NewLine;
            }));
        }
    }
}
