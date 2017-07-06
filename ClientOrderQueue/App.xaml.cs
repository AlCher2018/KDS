using ClientOrderQueue.Lib;
using ClientOrderQueue.Model;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Windows;
using System.Windows.Media;

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
            AppLib.WriteLogInfoMessage("****  Start application  ****");
            AppLib.WriteLogInfoMessage("Системное окружение: " + AppLib.GetEnvironmentString());
            AppLib.WriteLogInfoMessage("Версия файла {0}: {1}", AppLib.GetAppFileName(), AppLib.GetAppVersion());
            AppLib.WriteLogInfoMessage("Настройки из config-файла: " + AppLib.GetAppSettingsFromConfigFile());

            App app = new App();

            // splash
            getAppLayout();
            string fileName = (AppLib.IsAppVerticalLayout ? "Images/bg 3ver 1080x1920 splash.png" : "Images/bg 3hor 1920x1080 splash.png");
            SplashScreen splashScreen = new SplashScreen(fileName);
            splashScreen.Show(true);

            // проверка доступа к БД
            if (AppLib.CheckDBConnection(typeof(KDSContext)) == false)
            {
                MessageBox.Show("Ошибка доступа к базе данных. См. журнал в папке Logs.", "Аварийное завершение программы", MessageBoxButton.OK, MessageBoxImage.Stop);
                App.Current.Shutdown(1);
            }

            // настройка приложения
            app.InitializeComponent();  // определенные в app.xaml
            setAppGlobalValues();  // для хранения в свойствах приложения (из config-файла или др.)

            MainWindow mWindow = new MainWindow();
            app.Run(mWindow);

            AppLib.WriteLogInfoMessage("****  End application  ****");
        }


        private static void getAppLayout()
        {
            AppLib.SetAppGlobalValue("screenWidth", SystemParameters.PrimaryScreenWidth);
            AppLib.SetAppGlobalValue("screenHeight", SystemParameters.PrimaryScreenHeight);
        }

        // сохранить в свойствах приложения часто используемые значения, чтобы не дергать config-файл
        private static void setAppGlobalValues()
        {
            string cfgValue;

            // файл изображения состояния
            string sPath = AppLib.GetAppSetting("ImagesPath"), sFile = AppLib.GetAppSetting("StatusReadyImage");
            string fileName = AppLib.GetFullFileName(sPath, sFile);
            if ((fileName != null) && (System.IO.File.Exists(fileName))) AppLib.SetAppGlobalValue("StatusReadyImageFile", fileName);

            // размер шрифта номера заказа
            cfgValue = AppLib.GetAppSetting("OrderNumberFontSize");
            AppLib.SetAppGlobalValue("OrderNumberFontSize", cfgValue.ToDouble());
            // время приготовления заказа - отображается на панели, если != 0
            cfgValue = AppLib.GetAppSetting("OrderReadyTime");
            AppLib.SetAppGlobalValue("OrderReadyTime", cfgValue.ToDouble());
            // имя клиента - отображается на панели, если есть
            cfgValue = AppLib.GetAppSetting("IsShowClientName");
            AppLib.SetAppGlobalValue("IsShowClientName", cfgValue.ToBool());

            cfgValue = AppLib.GetAppSetting("IsWriteTraceMessages");
            AppLib.SetAppGlobalValue("IsWriteTraceMessages", (cfgValue == null) ? false : cfgValue.ToBool());

            // массивы строк для различных языков
            cfgValue = AppLib.GetAppSetting("StatusTitle");
            if (cfgValue == null) cfgValue = "Заказ|Замовлення|Order";
            AppLib.SetAppGlobalValue("StatusTitle", cfgValue.Split('|'));

            cfgValue = AppLib.GetAppSetting("PanelWaitText");
            if (cfgValue != null) cfgValue = "Ожидать|Чекати|Wait";
            AppLib.SetAppGlobalValue("PanelWaitText", cfgValue.Split('|'));

            string sBuf0 = AppLib.GetAppSetting("StatusLang0");
            if (sBuf0 == null) sBuf0 = "Готовится|Готується|In process";
            string sBuf1 = AppLib.GetAppSetting("StatusLang1");
            if (sBuf1 == null) sBuf1 = "Готов|Готово|Done";
            string sBuf2 = AppLib.GetAppSetting("StatusLang2");
            if (sBuf2 == null) sBuf2 = "Забрали|Забрали|Taken";
            AppLib.SetAppGlobalValue("StatusLang", new string[][] { sBuf0.Split('|'), sBuf1.Split('|'), sBuf2.Split('|') });
        }


    }  // class App
}
