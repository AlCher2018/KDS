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
            string sPath = AppLib.GetAppSetting("ImagesPath");
            AppLib.SetAppGlobalValue("ImagesPath", sPath);
            string sFile = AppLib.GetAppSetting("StatusReadyImage");
            string fileName = AppLib.GetFullFileName(sPath, sFile);
            if ((fileName != null) && (System.IO.File.Exists(fileName))) AppLib.SetAppGlobalValue("StatusReadyImageFile", fileName);

            // неиспользуемые цеха
            HashSet<int> unUsed = new HashSet<int>();
            cfgValue = AppLib.GetAppSetting("UnusedDepartments");
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
            AppLib.SetAppGlobalValue("UnusedDepartments", unUsed);

            // кисти фона и текста заголовка окна
            createWinTitleBrushes();
            // кисти фона панели заказа (CellBrushes - кисть фона и разделительной полосы)
            createPanelBackBrushes();

            // размер шрифта номера заказа
            cfgValue = AppLib.GetAppSetting("OrderNumberFontSize");
            AppLib.SetAppGlobalValue("OrderNumberFontSize", cfgValue.ToDouble());

            // показывать ли ожидаемое время приготовления заказа
            cfgValue = AppLib.GetAppSetting("IsShowOrderEstimateTime");
            AppLib.SetAppGlobalValue("IsShowOrderEstimateTime", cfgValue.ToBool());
            // ожидаемое время приготовления заказа
            cfgValue = AppLib.GetAppSetting("OrderEstimateTime");
            AppLib.SetAppGlobalValue("OrderEstimateTime", cfgValue.ToDouble());
            // имя клиента - отображается на панели, если есть
            cfgValue = AppLib.GetAppSetting("IsShowClientName");
            AppLib.SetAppGlobalValue("IsShowClientName", cfgValue.ToBool());

            cfgValue = AppLib.GetAppSetting("IsWriteTraceMessages");
            AppLib.SetAppGlobalValue("IsWriteTraceMessages", (cfgValue == null) ? false : cfgValue.ToBool());

            // массивы строк для различных языков
            cfgValue = AppLib.GetAppSetting("StatusTitle");
            if (cfgValue == null) cfgValue = "Заказ|Замовлення|Order";
            AppLib.SetAppGlobalValue("StatusTitle", cfgValue);

            cfgValue = AppLib.GetAppSetting("PanelWaitText");
            if (cfgValue != null) cfgValue = "Ожидать|Чекати|Wait";
            AppLib.SetAppGlobalValue("PanelWaitText", cfgValue);

            cfgValue = AppLib.GetAppSetting("StatusLang0");
            if (cfgValue == null) cfgValue = "Готовится|Готується|In process";
            AppLib.SetAppGlobalValue("Status1Langs", cfgValue);

            cfgValue = AppLib.GetAppSetting("StatusLang1");
            if (cfgValue == null) cfgValue = "Готов|Готово|Done";
            AppLib.SetAppGlobalValue("Status2Langs", cfgValue);

            cfgValue = AppLib.GetAppSetting("StatusLang2");
            if (cfgValue == null) cfgValue = "Забрали|Забрали|Taken";
            AppLib.SetAppGlobalValue("Status3Langs", cfgValue);
        }

        private static void createWinTitleBrushes()
        {
            string cfgValue;
            cfgValue = AppLib.GetAppSetting("WinTitleBackground");
            if (cfgValue == null) cfgValue = "122;34;104";   // по умолчанию - т.фиолетовый
            AppLib.SetAppGlobalValue("WinTitleBackground", getBrushByName(cfgValue, "122;34;104"));

            cfgValue = AppLib.GetAppSetting("WinTitleForeground");
            if (cfgValue == null) cfgValue = "255;200;62";   // по умолчанию - т.желтый
            AppLib.SetAppGlobalValue("WinTitleForeground", getBrushByName(cfgValue, "255;200;62"));
        }

        private static void createPanelBackBrushes()
        {
            Brush[] cellBrushes = new Brush[2];

            string cfgValue;
            cfgValue = AppLib.GetAppSetting("StatusCookingPanelBackground");
            if (cfgValue == null) cfgValue = "Gold";
            cellBrushes[0] = getBrushByName(cfgValue, "Gold");

            cfgValue = AppLib.GetAppSetting("StatusReadyPanelBackground");
            if (cfgValue == null) cfgValue = "LimeGreen";
            cellBrushes[1] = getBrushByName(cfgValue, "LimeGreen");

            // сохранить в свойствах
            AppLib.SetAppGlobalValue("PanelBackgroundBrushes", cellBrushes);
        }

        // brushName может быть как наименованием цвета, так и RGB
        private static SolidColorBrush getBrushByName(string brushName, string defaultBrushName = null)
        {
            SolidColorBrush retVal = null;

            // кисть по RGB
            if (brushName.Contains(";"))
            {
                string[] rgb = brushName.Split(';');
                if (rgb.Length == 3)
                {
                    Color c = Color.FromRgb(Convert.ToByte(rgb[0]), Convert.ToByte(rgb[1]), Convert.ToByte(rgb[2]));
                    retVal = new SolidColorBrush(c);
                }
            }
            else
            {
                Type t = typeof(Brushes);
                System.Reflection.PropertyInfo[] bProps = t.GetProperties();
                foreach (System.Reflection.PropertyInfo item in bProps)
                {
                    if (item.Name == brushName)
                    {
                        retVal = (SolidColorBrush)item.GetValue(null, null);
                        break;
                    }
                }
            }

            if ((retVal == null) && (defaultBrushName != null)) retVal = getBrushByName(defaultBrushName);

            return retVal;
        }

    }  // class App
}
