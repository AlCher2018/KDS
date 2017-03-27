using ClientOrderQueue.Lib;
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

namespace ClientOrderQueue
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            string s1 = AppLib.GetAppSetting("ReadyAudioFile");

            setAppLayout();
        }

        private void setAppLayout()
        {
            string bgImageFile;

            if (AppLib.IsAppVerticalLayout)
            {
                bgImageFile = AppLib.GetFullFileName("bg 3ver 1080x1920 background.png");
            }
            else
            {
                bgImageFile = AppLib.GetFullFileName("bg 3hor 1920x1080 background.png");
            }

            // фон
            backgroundImage.Source = ImageHelper.GetBitmapImage(bgImageFile);
            // яркость фона
            string opacity = AppLib.GetAppSetting("MenuBackgroundBrightness");
            if (opacity != null)
            {
                backgroundImage.Opacity = opacity.ToDouble();
            }
        }
    }
}
