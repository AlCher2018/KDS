using KDSWPFClient.ServiceReference1;
using KDSWPFClient.Lib;
using System;
using System.Globalization;
using System.Windows;


namespace KDSWPFClient
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
            AppLib.WriteLogInfoMessage("************  Start KDS Client (WPF) *************");
            AppLib.WriteLogInfoMessage(AppLib.GetEnvironmentString());

            KDSWPFClient.App app = new KDSWPFClient.App();

            getAppLayout();
            // splash
            //string fileName = (AppLib.IsAppVerticalLayout ? "Images/bg 3ver 1080x1920 splash.png" : "Images/bg 3hor 1920x1080 splash.png");
            //SplashScreen splashScreen = new SplashScreen(fileName);
            //splashScreen.Show(true);

            // настройка приложения
            app.InitializeComponent();  // определенные в app.xaml
            setAppGlobalValues();  // для хранения в свойствах приложения (из config-файла или др.)
            AppLib.WriteLogInfoMessage("App settings from config file: " + AppLib.GetAppSettingsFromConfigFile());

            // создать каналы
            AppLib.WriteLogTraceMessage("Создаю клиента для работы со службой KDSService...");
            AppDataProvider dataProvider = new AppDataProvider();
            if (dataProvider.ErrorMessage != null)
            {
                AppLib.WriteLogErrorMessage("Data provider error: " + dataProvider.ErrorMessage);
                //splashScreen.Close(TimeSpan.FromMinutes(10));
                MessageBox.Show("Ошибка создания каналов к службе KDSService:" + Environment.NewLine + dataProvider.ErrorMessage, "АВАРИЙНОЕ ЗАВЕРШЕНИЕ ПРИЛОЖЕНИЯ", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                Environment.Exit(1);
            }
            AppLib.WriteLogTraceMessage("Создаю клиента для работы со службой KDSService... Ok");
            // и получить словари
            //AppLib.WriteLogTraceMessage("Получаю словари от службы KDSService...");
            //if (dataProvider.SetDictDataFromService() == false)
            //{
            //    AppLib.WriteLogErrorMessage("Data provider error: " + dataProvider.ErrorMessage);
            //    splashScreen.Close(TimeSpan.FromMinutes(10));
            //    MessageBox.Show("Ошибка получения словарей от службы KDSService:" + Environment.NewLine + dataProvider.ErrorMessage, "АВАРИЙНОЕ ЗАВЕРШЕНИЕ ПРИЛОЖЕНИЯ", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
            //    Environment.Exit(2);
            //}
            //AppLib.WriteLogTraceMessage("Получаю словари от службы KDSService... Ok");

            // основное окно приложения
            MainWindow mWindow = new MainWindow();
            app.Run(mWindow);

            if (dataProvider != null) { dataProvider.Dispose(); dataProvider = null; }
            AppLib.WriteLogInfoMessage("************  End KDS Client (WPF)  *************");
        }  // Main()


        private static void getAppLayout()
        {
            AppLib.SetAppGlobalValue("screenWidth", SystemParameters.PrimaryScreenWidth);
            AppLib.SetAppGlobalValue("screenHeight", SystemParameters.PrimaryScreenHeight);
        }

        private static void setAppGlobalValues()
        {
            string cfgValue;
            if ((cfgValue = AppLib.GetAppSetting("IsWriteTraceMessages")) != null)
                AppLib.SetAppGlobalValue("IsWriteTraceMessages", cfgValue.ToBool());
            if ((cfgValue = AppLib.GetAppSetting("IsLogUserAction")) != null)
                AppLib.SetAppGlobalValue("IsLogUserAction", cfgValue.ToBool());

            if ((cfgValue = AppLib.GetAppSetting("AppMainScale")) != null)
                AppLib.SetAppGlobalValue("AppMainScale", cfgValue.ToDouble());
        }

    }  // class App
}
