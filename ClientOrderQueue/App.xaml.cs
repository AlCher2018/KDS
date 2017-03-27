using ClientOrderQueue.Lib;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ClientOrderQueue
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {

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

            // splash
            getAppLayout();
            string fileName = (AppLib.IsAppVerticalLayout ? "Images/bg 3ver 1080x1920 splash.png" : "Images/bg 3hor 1920x1080 splash.png");
            SplashScreen splashScreen = new SplashScreen(fileName);
            splashScreen.Show(true);

            app.InitializeComponent();          // определенные в app.xaml

            MainWindow mWindow = new MainWindow();
            app.Run(mWindow);

            AppLib.WriteLogInfoMessage("************  End application  ************");
        }

        private static void getAppLayout()
        {
            AppLib.SetAppGlobalValue("screenWidth", SystemParameters.PrimaryScreenWidth);
            AppLib.SetAppGlobalValue("screenHeight", SystemParameters.PrimaryScreenHeight);
        }

    }  // class App
}
