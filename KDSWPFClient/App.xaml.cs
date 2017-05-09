using KDSClient.Lib;
using System.Windows;


namespace KDSClient
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {

        /// <summary>
        /// Application Entry Point.
        /// </summary>
        [System.STAThreadAttribute()]
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "4.0.0.0")]
        public static void Main()
        {
            AppLib.WriteLogInfoMessage("************  Start application  *************");

            KDSClient.App app = new KDSClient.App();

            // splash
            getAppLayout();
            string fileName = (AppLib.IsAppVerticalLayout ? "Images/bg 3ver 1080x1920 splash.png" : "Images/bg 3hor 1920x1080 splash.png");
            SplashScreen splashScreen = new SplashScreen(fileName);
            splashScreen.Show(true);

            // настройка приложения
            app.InitializeComponent();  // определенные в app.xaml

            MainWindow mWindow = new MainWindow();
            app.Run(mWindow);

            AppLib.WriteLogInfoMessage("************  End application    *************");
        }

        //        StartupUri="MainWindow.xaml"
        private static void getAppLayout()
        {
            AppLib.SetAppGlobalValue("screenWidth", SystemParameters.PrimaryScreenWidth);
            AppLib.SetAppGlobalValue("screenHeight", SystemParameters.PrimaryScreenHeight);
        }

    }  // class App
}
