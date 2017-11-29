using ClientOrderQueue.Lib;
using ClientOrderQueue.Model;
using IntegraLib;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
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

            // информация о файлах и сборках
            AppLib.WriteLogInfoMessage(" - файл: {0}, Version {1}", AppEnvironment.GetAppFullFile(), AppEnvironment.GetAppVersion());
            ITSAssemmblyInfo asmInfo = new ITSAssemmblyInfo("IntegraLib");
            AppLib.WriteLogInfoMessage(" - Integra lib: '{0}', Version {1}", asmInfo.FullFileName, asmInfo.Version);

            AppLib.WriteLogInfoMessage("Системное окружение: " + AppEnvironment.GetEnvironmentString());
            AppLib.WriteLogInfoMessage("Настройки из config-файла: " + CfgFileHelper.GetAppSettingsFromConfigFile());

            App app = new App();
            // флажки для логов
            string cfgValue = CfgFileHelper.GetAppSetting("IsWriteTraceMessages");
            WpfHelper.SetAppGlobalValue("IsWriteTraceMessages", cfgValue.ToBool());

            // splash
            getAppLayout();
            string fileName = (WpfHelper.IsAppVerticalLayout ? "Images/bg 3ver 1080x1920 splash.png" : "Images/bg 3hor 1920x1080 splash.png");
            SplashScreen splashScreen = new SplashScreen(fileName);
            splashScreen.Show(true);

            // проверить доступность БД
            if (AppLib.CheckDBConnection(typeof(KDSContext)) == false)
            {
                bool result = false;
                AppStartWait winWait = new AppStartWait();
                winWait.Show();

                // сделать цикл проверки подключения: 20 раз через 2 сек
                for (int i = 1; i <= 20; i++)
                {
                    winWait.Dispatcher.Invoke(() =>
                    {
                        int iVal = winWait.txtNumAttempt.Text.ToInt();
                        iVal++;
                        winWait.txtNumAttempt.Text = iVal.ToString();
                        winWait.InvalidateProperty(TextBlock.TextProperty);
                        winWait.InvalidateVisual();
                        winWait.Refresh();
                    });
                    Thread.Sleep(2000);

                    result = AppLib.CheckDBConnection(typeof(KDSContext));
                    if (result) break;
                }
                winWait.Close();

                if (!result)
                {
                    MessageBox.Show("Ошибка подключения к базе данных. См. журнал в папке Logs.\nПриложение будет закрыто", "Аварийное завершение", MessageBoxButton.OK, MessageBoxImage.Stop);
                    Environment.Exit(3);
                }
                // перезапусить приложение
                else
                {
                    AppEnvironment.RestartApplication();
                }
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
            WpfHelper.SetAppGlobalValue("screenWidth", SystemParameters.PrimaryScreenWidth);
            WpfHelper.SetAppGlobalValue("screenHeight", SystemParameters.PrimaryScreenHeight);
        }

        // сохранить в свойствах приложения часто используемые значения, чтобы не дергать config-файл
        private static void setAppGlobalValues()
        {
            string cfgValue;

            // файл изображения состояния
            string sPath = CfgFileHelper.GetAppSetting("ImagesPath");
            WpfHelper.SetAppGlobalValue("ImagesPath", sPath);
            string sFile = CfgFileHelper.GetAppSetting("StatusReadyImage");
            string fileName = AppEnvironment.GetFullFileName(sPath, sFile);
            if ((fileName != null) && (System.IO.File.Exists(fileName))) WpfHelper.SetAppGlobalValue("StatusReadyImageFile", fileName);

            // неиспользуемые цеха
            HashSet<int> unUsed = new HashSet<int>();
            cfgValue = CfgFileHelper.GetAppSetting("UnusedDepartments");
            if (cfgValue != null)
            {
                if (cfgValue.Contains(",")) cfgValue = cfgValue.Replace(',', ';');
                int id;
                foreach (string item in cfgValue.Split(';'))
                {
                    id = item.ToInt();
                    if (!unUsed.Contains(id)) unUsed.Add(id);
                }
                    
            }
            WpfHelper.SetAppGlobalValue("UnusedDepartments", unUsed);

            // кисти фона и текста заголовка окна
            createWinTitleBrushes();
            // кисти фона панели заказа (CellBrushes - кисть фона и разделительной полосы)
            createPanelBackBrushes();

            // показывать ли ожидаемое время приготовления заказа
            cfgValue = CfgFileHelper.GetAppSetting("IsShowOrderEstimateTime");
            WpfHelper.SetAppGlobalValue("IsShowOrderEstimateTime", cfgValue.ToBool());
            // ожидаемое время приготовления заказа
            cfgValue = CfgFileHelper.GetAppSetting("OrderEstimateTime");
            WpfHelper.SetAppGlobalValue("OrderEstimateTime", cfgValue.ToDouble());
            // имя клиента - отображается на панели, если есть
            cfgValue = CfgFileHelper.GetAppSetting("IsShowClientName");
            WpfHelper.SetAppGlobalValue("IsShowClientName", cfgValue.ToBool());

            cfgValue = CfgFileHelper.GetAppSetting("IsWriteTraceMessages");
            WpfHelper.SetAppGlobalValue("IsWriteTraceMessages", (cfgValue == null) ? false : cfgValue.ToBool());

            // массивы строк для различных языков
            cfgValue = CfgFileHelper.GetAppSetting("StatusTitle");
            if (cfgValue == null) cfgValue = "Заказ|Замовлення|Order";
            WpfHelper.SetAppGlobalValue("StatusTitle", cfgValue);

            cfgValue = CfgFileHelper.GetAppSetting("PanelWaitText");
            if (cfgValue != null) cfgValue = "Ожидать|Чекати|Wait";
            WpfHelper.SetAppGlobalValue("PanelWaitText", cfgValue);

            cfgValue = CfgFileHelper.GetAppSetting("StatusLang0");
            if (cfgValue == null) cfgValue = "Готовится|Готується|In process";
            WpfHelper.SetAppGlobalValue("Status1Langs", cfgValue);

            cfgValue = CfgFileHelper.GetAppSetting("StatusLang1");
            if (cfgValue == null) cfgValue = "Готов|Готово|Done";
            WpfHelper.SetAppGlobalValue("Status2Langs", cfgValue);

            cfgValue = CfgFileHelper.GetAppSetting("StatusLang2");
            if (cfgValue == null) cfgValue = "Забрали|Забрали|Taken";
            WpfHelper.SetAppGlobalValue("Status3Langs", cfgValue);
        }

        private static void createWinTitleBrushes()
        {
            string cfgValue;
            cfgValue = CfgFileHelper.GetAppSetting("WinTitleBackground");
            if (cfgValue == null) cfgValue = "122;34;104";   // по умолчанию - т.фиолетовый
            WpfHelper.SetAppGlobalValue("WinTitleBackground", DrawHelper.GetBrushByName(cfgValue, "122;34;104"));

            cfgValue = CfgFileHelper.GetAppSetting("WinTitleForeground");
            if (cfgValue == null) cfgValue = "255;200;62";   // по умолчанию - т.желтый
            WpfHelper.SetAppGlobalValue("WinTitleForeground", DrawHelper.GetBrushByName(cfgValue, "255;200;62"));
        }

        private static void createPanelBackBrushes()
        {
            Brush[] cellBrushes = new Brush[2];

            string cfgValue;
            cfgValue = CfgFileHelper.GetAppSetting("StatusCookingPanelBackground");
            if (cfgValue == null) cfgValue = "Gold";
            cellBrushes[0] = DrawHelper.GetBrushByName(cfgValue, "Gold");

            cfgValue = CfgFileHelper.GetAppSetting("StatusReadyPanelBackground");
            if (cfgValue == null) cfgValue = "LimeGreen";
            cellBrushes[1] = DrawHelper.GetBrushByName(cfgValue, "LimeGreen");

            // сохранить в свойствах
            WpfHelper.SetAppGlobalValue("PanelBackgroundBrushes", cellBrushes);
        }

    }  // class App
}
