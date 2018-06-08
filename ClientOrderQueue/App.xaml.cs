﻿using ClientOrderQueue.Lib;
using ClientOrderQueue.Model;
using IntegraLib;
using IntegraWPFLib;
using IntergaLib;
using SplashScreenLib;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
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
        public static void Main(string[] args)
        {
            //System.IO.FileStream writer = new System.IO.FileStream(@"c:\Users\Leschenko.V\Documents\Visual Studio 2015\Projects\Integra KDS1\ClientOrderQueue\bin\Debug\updFiles\ClientOrderQueue.exe", System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None);
            //byte[] ba = new byte[] {50,51,52 };
            //writer.Write(ba,0,ba.Length);
            //writer.Close();
            //writer.Dispose();

            AppArgs.PutArgs(args);
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

            // получить настройки (из config-файла или пр.) для хранения в свойствах приложения
            setAppGlobalValues();

            // информация о файлах, сборках и настройках из конфиг-файлов
            MessageListener.Instance.ReceiveMessage("Получаю информацию о сборках и настройках...");
            string appVersion = AppEnvironment.GetAppVersion();
            AppLib.WriteLogInfoMessage(" - файл: {0}, Version {1}", AppEnvironment.GetAppFullFile(), appVersion);
            ITSAssemblyInfo asmInfo = new ITSAssemblyInfo("IntegraLib");
            AppLib.WriteLogInfoMessage(" - Integra lib: '{0}', Version {1}", asmInfo.FullFileName, asmInfo.Version);
            asmInfo = new ITSAssemblyInfo("IntegraWPFLib");
            AppLib.WriteLogInfoMessage(" - Integra WPF lib: '{0}', Version {1}", asmInfo.FullFileName, asmInfo.Version);

            AppLib.WriteLogInfoMessage("Системное окружение: " + AppEnvironment.GetEnvironmentString());
            AppLib.WriteLogInfoMessage("Настройки из config-файла: " + CfgFileHelper.GetAppSettingsFromConfigFile());

            // проверка обновления софта
            if (AppArgs.IsExists("noUpdate") == false)
            {
                bool autoCreateGRegKey = AppArgs.IsExists("autoCreateUpdateRegKeys");
                updateApplication(autoCreateGRegKey);
            }

            // проверить доступность БД
            MessageListener.Instance.ReceiveMessage("Проверяю доступность к базе данных...");
            AppLib.CheckDBConnection(typeof(KDSContext));

            // настройка приложения
            app.InitializeComponent();  // определенные в app.xaml

            MessageListener.Instance.ReceiveMessage("Работаю...");
            View.MainWindow mWindow = new View.MainWindow();
            try
            {
                app.Run(mWindow);
            }
            catch (Exception ex)
            {
                AppLib.WriteLogErrorMessage(ex.ToString());
                MessageBox.Show(ex.Message, "Error Application", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
            }

            AppLib.WriteLogInfoMessage("****  End application  ****");
        }


        private static void updateApplication(bool autoCreateRegKey)
        {
            AppAutoUpdater updater = new AppAutoUpdater(
                appName: AppEnvironment.GetAppAssemblyName(),
                autoCreateRegKeys: autoCreateRegKey);
            // узнать логгер приложения. проверять существование файла журнала не надо, 
            // т.к. в updater-е файл будет создан автоматически
            updater.LogFile = AppLib.GetFirstFileLogger(false);
            updater.UpdateActionBefore += updater_UpdateActionBefore;
            updater.UpdateActionAfter += updater_UpdateActionAfter;
            // 1. проверка в реестре настроек автообновления
            if (updater.Enable)
            {
                MessageListener.Instance.ReceiveMessage("Проверяю необходимость обновления файлов...");
                AppLib.WriteLogInfoMessage($"Проверка обновлений в хранилище '{updater.StoragePath}'...");
                bool result;
                result = updater.IsNeedUpdate();
                if (result == true)
                {
                    AppLib.WriteLogInfoMessage(" - папка обновления: " + updater.UpdateFTPFolder);
                    string updReason = updater.UpdateReasonString();
                    AppLib.WriteLogInfoMessage(" - причина обновления:" + Environment.NewLine + updReason);
                    updReason = string.Join("\n\t", updater.UpdateItems);
                    AppLib.WriteLogInfoMessage(" - обновляемые файлы:\n\t" + updReason);
                    // обновление файлов
                    MessageListener.Instance.ReceiveMessage("Обновляю файлы...");
                    AppLib.WriteLogInfoMessage("Обновляю файлы...");
                    result = updater.DoUpdate();
                    if (result == true)
                    {
                        AppLib.WriteLogInfoMessage("Файлы обновлены успешно");
                    }
                    else if (updater.LastError != null)
                    {
                        AppLib.WriteLogInfoMessage("Обновление не выполнено из-за ошибки: " + updater.LastError);
                    }
                    else
                    {
                        AppLib.WriteLogInfoMessage("Обновление не требуется");
                    }
                }
                else if (updater.LastError != null)
                {
                    AppLib.WriteLogInfoMessage($" - ошибка проверки обновления: " + updater.LastError);
                }
                else
                {
                    AppLib.WriteLogInfoMessage($"Обновление приложения НЕ требуется");
                }
            }
            else
            {
                AppLib.WriteLogInfoMessage("Автообновления приложения ВЫКЛЮЧЕНО.");
            }
            updater.Dispose();
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

        private static void updater_UpdateActionBefore(object sender, string e)
        {
            AppLib.WriteLogTraceMessage(e);
        }
        private static void updater_UpdateActionAfter(object sender, string e)
        {
            AppLib.WriteLogTraceMessage(e);
        }


    }  // class App
}
