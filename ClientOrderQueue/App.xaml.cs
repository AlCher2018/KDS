using ClientOrderQueue.Lib;
using ClientOrderQueue.Model;
using IntegraLib;
using IntegraWPFLib;
using SplashScreenLib;
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
            App app = new App();

            // splash

            Splasher.Splash = new View.SplashScreen();
            Splasher.ShowSplash();
            //for (int i = 0; i < 5000; i += 1)
            //{
            //    MessageListener.Instance.ReceiveMessage(string.Format("Load module {0}", i));
            //    Thread.Sleep(1);
            //}

            // таймаут запуска приложения
            string cfgValue = CfgFileHelper.GetAppSetting("StartTimeout");
            int startTimeout = 0;
            if (cfgValue != null) startTimeout = cfgValue.ToInt();
            if (startTimeout != 0)
            {
                for (int i = startTimeout; i > 0; i--)
                {
                    MessageListener.Instance.ReceiveMessage($"Таймаут запуска приложения - {i} секунд.");
                    System.Threading.Thread.Sleep(1000);
                }
            }

            MessageListener.Instance.ReceiveMessage("Инициализация журнала событий...");
            cfgValue = AppLib.InitAppLogger();
            if (cfgValue != null)
            {
                appExit(1, "Ошибка инициализации журнала приложения: " + cfgValue);
            }
            System.Threading.Thread.Sleep(500);

            AppLib.WriteLogInfoMessage("****  Start application  ****");

            // защита PSW-файлом
            // текст в MessageListener.Instance прибинден к текстовому полю на сплэше
            MessageListener.Instance.ReceiveMessage("Проверка лицензии...");
            pswLib.CheckProtectedResult checkProtectedResult;
            if (pswLib.Hardware.IsCurrentAppProtected("ClientOrderQueue", out checkProtectedResult) == false)
            {
                AppLib.WriteLogErrorMessage(checkProtectedResult.LogMessage);
                appExit(2, checkProtectedResult.CustomMessage);
                return;
            }
            System.Threading.Thread.Sleep(500);

            // информация о файлах, сборках и настройках из конфиг-файлов
            MessageListener.Instance.ReceiveMessage("Получаю информацию о сборках и настройках...");
            // для хранения в свойствах приложения (из config-файла или др.)
            setAppGlobalValues();  
            AppLib.WriteLogInfoMessage(" - файл: {0}, Version {1}", AppEnvironment.GetAppFullFile(), AppEnvironment.GetAppVersion());
            ITSAssemblyInfo asmInfo = new ITSAssemblyInfo("IntegraLib");
            AppLib.WriteLogInfoMessage(" - Integra lib: '{0}', Version {1}", asmInfo.FullFileName, asmInfo.Version);
            asmInfo = new ITSAssemblyInfo("IntegraWPFLib");
            AppLib.WriteLogInfoMessage(" - Integra WPF lib: '{0}', Version {1}", asmInfo.FullFileName, asmInfo.Version);

            AppLib.WriteLogInfoMessage("Системное окружение: " + AppEnvironment.GetEnvironmentString());
            AppLib.WriteLogInfoMessage("Настройки из config-файла: " + CfgFileHelper.GetAppSettingsFromConfigFile());

            // флажки для логов
            cfgValue = CfgFileHelper.GetAppSetting("IsWriteTraceMessages");
            WpfHelper.SetAppGlobalValue("IsWriteTraceMessages", cfgValue.ToBool());
            System.Threading.Thread.Sleep(300);

            // проверить доступность БД
            MessageListener.Instance.ReceiveMessage("Проверяю доступность к базе данных...");
            if (AppLib.CheckDBConnection(typeof(KDSContext)) == false)
            {
                bool result = false;
                int tryCount = 20;
                // сделать цикл проверки подключения: 20 раз через 2 сек
                for (int i = tryCount; i >= 1; i--)
                {
                    cfgValue = $"Попытка подключения к БД: {i} из {tryCount}";
                    MessageListener.Instance.ReceiveMessage(cfgValue);
                    AppLib.WriteLogInfoMessage(cfgValue);

                    Thread.Sleep(2000);
                    result = AppLib.CheckDBConnection(typeof(KDSContext));
                    if (result) break;
                }

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

            MessageListener.Instance.ReceiveMessage("Работаю...");
            View.MainWindow mWindow = new View.MainWindow();
            app.Run(mWindow);

            AppLib.WriteLogInfoMessage("****  End application  ****");
        }

        private static void appExit(int exitCode, string errMsg)
        {
            Splasher.CloseSplash();

            if ((exitCode != 0) && (errMsg.IsNull() == false))
            {
                MessageBox.Show(errMsg, "Аварийное завершение программы", MessageBoxButton.OK, MessageBoxImage.Stop);
            }

            Environment.Exit(exitCode);
        }


        // сохранить в свойствах приложения часто используемые значения, чтобы не дергать config-файл
        private static void setAppGlobalValues()
        {
            string cfgValue;
            double dCfg;

            cfgValue = CfgFileHelper.GetAppSetting("MidnightShiftShowYesterdayOrders");
            dCfg = (cfgValue == null ? 0 : cfgValue.ToDouble());
            if (dCfg < 0) dCfg = 0d;
            WpfHelper.SetAppGlobalValue("MidnightShiftShowYesterdayOrders", dCfg);

            // файл изображения состояния
            string sPath = CfgFileHelper.GetAppSetting("ImagesPath");
            if (string.IsNullOrEmpty(sPath)) sPath = "Images";
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

            // поля панели заказа в ячейке - два double-значения, разделенные ";"
            cfgValue = checkMarginKoefString();
            WpfHelper.SetAppGlobalValue("MarginKoefStr", cfgValue);

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

        private static string checkMarginKoefString()
        {
            // строки по умолчанию
            string cfgValue = CfgFileHelper.GetAppSetting("MarginHor");
            if (cfgValue.IsNull()) cfgValue = "0.05";
            string s1 = CfgFileHelper.GetAppSetting("MarginVer");
            cfgValue += ";" + (s1.IsNull() ? "0.05" : s1);
            
            // проверить цифровые значения
            string[] a1 = cfgValue.Split(';');
            double d1 = a1[0].ToDouble(), d2 = ((a1.Length > 1) ? a1[1].ToDouble() : d1);
            if (d1 < 0d) d1 = 0.01d; else if (d1 > 0.4d) d1 = 0.4d;
            if (d2 < 0d) d2 = 0.01d; else if (d2 > 0.4d) d2 = 0.4d;

            IFormatProvider dotFormatter = FormatProviderHelper.DotFormatter();
            string retVal = d1.ToString(dotFormatter) + ";" + d2.ToString(dotFormatter);

            return retVal;
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
