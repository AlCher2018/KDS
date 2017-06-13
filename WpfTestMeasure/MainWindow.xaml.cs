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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WpfTestMeasure
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            TimeSpan ts;
            TimeSpan.TryParse("-05:33:20", out ts);
            TimeSpan ts1 = ts.Negate();
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            Window1 win1 = new Window1();
            textBox.Text += Environment.NewLine +"after create: " +  win1.textBlock.DesiredSize;
            win1.textBlock.Measure(new Size(1000d, 1000d));
            textBox.Text += Environment.NewLine + "after measured: " + win1.textBlock.DesiredSize;
            win1.Show();
            win1.textBlock.Measure(new Size(1000d, 1000d));
            textBox.Text += Environment.NewLine + "after show win: " +  win1.textBlock.DesiredSize;

            win1.textBlock.Text = LoremIpsum(2, 7, 1, 1, 1);
            win1.textBlock.FontSize *= 2d;
            win1.textBlock.Measure(new Size(1000d, 1000d));
            textBox.Text += Environment.NewLine + "after incr font size: " + win1.textBlock.DesiredSize;

            double h1 = win1.textBlock.DesiredSize.Height + 10d;
            win1.grdMain.Children.Add(new Line() { X1=0, X2=20, Y1=10,Y2=10, Stroke=Brushes.Red, SnapsToDevicePixels=true});
            win1.grdMain.Children.Add(new Line() { X1=0, X2=20, Y1=h1,Y2=h1, Stroke=Brushes.Red, SnapsToDevicePixels = true });

        }


        private string LoremIpsum(int minWords, int maxWords, int minSentences, int maxSentences, int numParagraphs)
        {
            var words = new[]{"lorem", "ipsum", "dolor", "sit", "amet", "consectetuer",
        "adipiscing", "elit", "sed", "diam", "nonummy", "nibh", "euismod",
        "tincidunt", "ut", "laoreet", "dolore", "magna", "aliquam", "erat"};

            var rand = new Random();
            int numSentences = rand.Next(maxSentences - minSentences)
                + minSentences + 1;
            int numWords = rand.Next(maxWords - minWords) + minWords + 1;

            StringBuilder result = new StringBuilder();

            for (int p = 0; p < numParagraphs; p++)
            {
                result.Append("<p>");
                for (int s = 0; s < numSentences; s++)
                {
                    for (int w = 0; w < numWords; w++)
                    {
                        if (w > 0) { result.Append(" "); }
                        result.Append(words[rand.Next(words.Length)]);
                    }
                    result.Append(". ");
                }
                result.Append("</p>");
            }

            return result.ToString();
        }

    }  // class
}
