using ClientOrderQueue.Lib;
using ClientOrderQueue.Model;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace ClientOrderQueue
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public CellBrushes[] cellBrushes;
        public string[] statusTitleLang;
        public string[][] statusLang;

        [STAThread]
        public static void Main()
        {
            // номер устройства - не число!
            //if (AppLib.GetAppSetting("ssdID").IsNumber() == false)
            //{
            //    AppLib.WriteLogErrorMessage("** Номер устройства - НЕ ЧИСЛО !! **");
            //    AppLib.WriteLogInfoMessage("************  End application  ************");
            //    MessageBox.Show("Номер устройства - НЕ ЧИСЛО!!");
            //    Environment.Exit(3);
            //}

            AppLib.WriteLogInfoMessage("************  Start application  **************");
            App app = new App();
            app.cellBrushes = new CellBrushes[2] { new CellBrushes(), new CellBrushes()};

            // splash
            getAppLayout();
            string fileName = (AppLib.IsAppVerticalLayout ? "Images/bg 3ver 1080x1920 splash.png" : "Images/bg 3hor 1920x1080 splash.png");
            SplashScreen splashScreen = new SplashScreen(fileName);
            splashScreen.Show(true);

            app.InitializeComponent();          // определенные в app.xaml
            setCellBrushes(app);
            setCellLangTexts(app);

            MainWindow mWindow = new MainWindow();
            app.Run(mWindow);

            AppLib.WriteLogInfoMessage("************  End application  ************");
        }

        private static void setCellBrushes(App app)
        {
            // cooking
            app.cellBrushes[0].Background = (Brush)app.Resources["statusCookingBrush"];
            if (app.cellBrushes[0].Background == null) app.cellBrushes[0].Background = Brushes.Orange;
            app.cellBrushes[0].DelimLine = new SolidColorBrush(Color.FromRgb(218, 151, 88));

            // cooked
            app.cellBrushes[1].Background = (Brush)app.Resources["statusCookedBrush"];
            if (app.cellBrushes[1].Background == null) app.cellBrushes[1].Background = Brushes.Lime;
            app.cellBrushes[1].DelimLine = new SolidColorBrush(Color.FromRgb(97, 210, 67));
        }
        private static void setCellLangTexts(App app)
        {
            string sBuf = AppLib.GetAppSetting("StatusTitle");
            if (sBuf == null) sBuf = "Заказ|Замовлення|Order";
            app.statusTitleLang = sBuf.Split('|');

            string sBuf0 = AppLib.GetAppSetting("StatusLang0");
            if (sBuf0 == null) sBuf0 = "Готовится|Готується|In process";
            string sBuf1 = AppLib.GetAppSetting("StatusLang1");
            if (sBuf1 == null) sBuf1 = "Готов|Готово|Done";
            string sBuf2 = AppLib.GetAppSetting("StatusLang2");
            if (sBuf2 == null) sBuf2 = "Забрали|Забрали|Taken";
            app.statusLang = new string[][] { sBuf0.Split('|'), sBuf1.Split('|'), sBuf2.Split('|') };
        }


        private static void getAppLayout()
        {
            AppLib.SetAppGlobalValue("screenWidth", SystemParameters.PrimaryScreenWidth);
            AppLib.SetAppGlobalValue("screenHeight", SystemParameters.PrimaryScreenHeight);
        }

    }  // class App
}
