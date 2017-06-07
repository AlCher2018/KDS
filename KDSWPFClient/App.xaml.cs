using KDSWPFClient.ServiceReference1;
using KDSWPFClient.Lib;
using System;
using System.Globalization;
using System.Windows;
using System.Collections.Generic;
using KDSWPFClient.ViewModel;
using KDSWPFClient.Model;
using KDSWPFClient.View;
using System.Windows.Media;

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
            string fileName = (AppLib.IsAppVerticalLayout ? "Images/bg 3ver 1080x1920 splash.png" : "Images/bg 3hor 1920x1080 splash.png");
            SplashScreen splashScreen = null;
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
                // КДСы могут быть уже запущены, а служба еще нет!
                AppLib.WriteLogErrorMessage("Data provider error: " + dataProvider.ErrorMessage);
                //if (splashScreen != null) splashScreen.Close(TimeSpan.FromMinutes(10));
                //MessageBox.Show("Ошибка создания каналов к службе KDSService:" + Environment.NewLine + dataProvider.ErrorMessage, "АВАРИЙНОЕ ЗАВЕРШЕНИЕ ПРИЛОЖЕНИЯ", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                //Environment.Exit(1);
            }
            else
                AppLib.WriteLogTraceMessage("Создаю клиента для работы со службой KDSService... Ok");
            
            // и получить словари и настройки от службы
            AppLib.WriteLogTraceMessage("Получаю словари и настройки от службы KDSService...");
            if (dataProvider.SetDictDataFromService() == false)
            {
                // КДСы могут быть уже запущены, а служба еще нет!
                AppLib.WriteLogErrorMessage("Data provider error: " + dataProvider.ErrorMessage);
                //if (splashScreen != null) splashScreen.Close(TimeSpan.FromMinutes(10));
                //MessageBox.Show("Ошибка получения словарей от службы KDSService:" + Environment.NewLine + dataProvider.ErrorMessage, "АВАРИЙНОЕ ЗАВЕРШЕНИЕ ПРИЛОЖЕНИЯ", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                //Environment.Exit(2);
            }
            else
                AppLib.WriteLogTraceMessage("Получаю словари и настройки от службы KDSService... Ok");

            AppLib.SetAppGlobalValue("AppDataProvider", dataProvider);

            // прочитать из config-а и сохранить в свойствах приложения режим КДС
            // после открытия канала к сервису, т.к. здесь используются значения, полученные от службы и сохраненные в св-вах приложения
            KDSModeHelper.PutCfgKDSModeToAppProps();

            // основное окно приложения
            MainWindow mainWindow = new MainWindow();
            app.Run(mainWindow);

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

            // отделы на КДСе
            cfgValue = AppLib.GetAppSetting("depUIDs");
            AppLib.SetAppGlobalValue("depUIDs", cfgValue);

            cfgValue = AppLib.GetAppSetting("IsWriteTraceMessages");
            AppLib.SetAppGlobalValue("IsWriteTraceMessages", (cfgValue == null) ? false : cfgValue.ToBool());
            cfgValue = AppLib.GetAppSetting("IsLogUserAction");
            AppLib.SetAppGlobalValue("IsLogUserAction", (cfgValue == null) ? false : cfgValue.ToBool());

            // ****  РАСЧЕТ РАЗМЕЩЕНИЯ ПАНЕЛЕЙ ЗАКАЗОВ
            AppLib.RecalcOrderPanelsLayot();

            // ** ЗАГОЛОВОК ЗАКАЗА
            // шрифты для панели заказа
            AppLib.SetAppGlobalValue("ordPnlHdrLabelFontSize", 14d);
            AppLib.SetAppGlobalValue("ordPnlHdrTableNameFontSize", 20d);
            AppLib.SetAppGlobalValue("ordPnlHdrOrderNumberFontSize", 22d);
            AppLib.SetAppGlobalValue("ordPnlHdrWaiterNameFontSize", 14d);
            AppLib.SetAppGlobalValue("ordPnlHdrOrderTimerFontSize", 24d);
            //    шрифт заголовка таблицы блюд
            AppLib.SetAppGlobalValue("ordPnlDishTblHeaderFontSize", 10d);
            // ** СТРОКА БЛЮДА
            // шрифт строки блюда
            AppLib.SetAppGlobalValue("ordPnlDishLineFontSize", 20d);
            // минимальная высота строки блюда
            double dishLineMinHeight = (double)AppLib.GetAppGlobalValue("screenHeight") / 20d;
            AppLib.SetAppGlobalValue("ordPnlDishLineMinHeight", dishLineMinHeight);
            // шрифт разделителя блюд (напр. Подача **)
            AppLib.SetAppGlobalValue("ordPnlDishDelimiterFontSize", 16d);

            // кнопки прокрутки страниц
            AppLib.SetAppGlobalValue("dishesPanelScrollButtonSize", 100d);

            cfgValue = AppLib.GetAppSetting("NewOrderAudioAttention");
            if (cfgValue != null) AppLib.SetAppGlobalValue("NewOrderAudioAttention", cfgValue);

            // режим ингредиента: 
            // - подчиненный блюду, т.е. переходит из состояния в состояние только вместе с блюдом, отображается только вместе с блюдом, 
            //         имеет такие же значения в runTimeRecords
            // - самостоятельный(независимый), т.е. ведет себя как блюдо (может отображаться на разных КДС-ах, 
            //         иметь собственные таймеры, переходить из состояния в состояние), кроме перехода в состояние ВЫДАНО, 
            //         в это состояние незав.ингредиент может быть переведен только вместе с блюдом, т.е. как зависимый ингредиент
            // Режим находится в службе, а клиентом только читается оттуда: AppDataProvider.SetDictDataFromService()
            bool isIngrIndepend = (bool)AppLib.GetAppGlobalValue("IsIngredientsIndependent", false); // for Copy/Paste

        }


    }  // class App
}
