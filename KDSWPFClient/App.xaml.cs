using KDSWPFClient.ServiceReference1;
using KDSWPFClient.Lib;
using System;
using System.Globalization;
using System.Windows;
using System.Collections.Generic;
using KDSWPFClient.ViewModel;

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
                AppLib.WriteLogErrorMessage("Data provider error: " + dataProvider.ErrorMessage);
                if (splashScreen != null) splashScreen.Close(TimeSpan.FromMinutes(10));
                MessageBox.Show("Ошибка создания каналов к службе KDSService:" + Environment.NewLine + dataProvider.ErrorMessage, "АВАРИЙНОЕ ЗАВЕРШЕНИЕ ПРИЛОЖЕНИЯ", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                Environment.Exit(1);
            }
            AppLib.WriteLogTraceMessage("Создаю клиента для работы со службой KDSService... Ok");
            // и получить словари
            AppLib.WriteLogTraceMessage("Получаю словари от службы KDSService...");
            if (dataProvider.SetDictDataFromService() == false)
            {
                AppLib.WriteLogErrorMessage("Data provider error: " + dataProvider.ErrorMessage);
                if (splashScreen != null) splashScreen.Close(TimeSpan.FromMinutes(10));
                MessageBox.Show("Ошибка получения словарей от службы KDSService:" + Environment.NewLine + dataProvider.ErrorMessage, "АВАРИЙНОЕ ЗАВЕРШЕНИЕ ПРИЛОЖЕНИЯ", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                Environment.Exit(2);
            }
            AppLib.WriteLogTraceMessage("Получаю словари от службы KDSService... Ok");


            // основное окно приложения
            MainWindow mainWindow = new MainWindow();
            mainWindow.DataProvider = dataProvider;
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
            // заполнить словарь разрешенных переходов между состояниями
            setStatesAllowedForMoveInAppProps();

            cfgValue = AppLib.GetAppSetting("IsWriteTraceMessages");
            AppLib.SetAppGlobalValue("IsWriteTraceMessages", (cfgValue == null)?false:cfgValue.ToBool());
            cfgValue = AppLib.GetAppSetting("IsLogUserAction");
            AppLib.SetAppGlobalValue("IsLogUserAction", (cfgValue == null)?false:cfgValue.ToBool());

            cfgValue = AppLib.GetAppSetting("AppFontScale");
            AppLib.SetAppGlobalValue("AppFontScale", (cfgValue == null)?1d:cfgValue.ToDouble());

            // размеры элементов панели заказа
            //   кол-во столбцов заказов
            cfgValue = AppLib.GetAppSetting("OrdersColumnsCount");
            int cntCols = (cfgValue == null) ? 4 : cfgValue.ToInt();  // по умолчанию - 4
            AppLib.SetAppGlobalValue("OrdersColumnsCount", cntCols);
            //   ширина столбцов заказов и расстояния между столбцами
            double screenWidth = (double)AppLib.GetAppGlobalValue("screenWidth");
            // wScr = wCol*cntCols + koef*wCol*(cntCols+1) ==> wCol = wScr / (cntCols + koef*(cntCols+1))
            // где, koef = доля поля от ширины колонки
            double koef = 0.2;
            double colWidth = screenWidth / (cntCols + koef*(cntCols+1));
            double colMargin = koef * colWidth;
            AppLib.SetAppGlobalValue("OrdersColumnWidth", colWidth);
            AppLib.SetAppGlobalValue("OrdersColumnMargin", colMargin);  // поле между заказами по горизонтали

            //   отступ сверху/снизу для панели заказов
            AppLib.SetAppGlobalValue("dishesPanelTopBotMargin", 20d);
            //   отступ между заказами по вертикали
            AppLib.SetAppGlobalValue("ordPnlTopMargin", 50d);
            // шрифты для панели заказа
            //    заголовок заказа
            AppLib.SetAppGlobalValue("ordPnlHdrLabelFontSize", 14d);
            AppLib.SetAppGlobalValue("ordPnlHdrTableNameFontSize", 20d);
            AppLib.SetAppGlobalValue("ordPnlHdrOrderNumberFontSize", 22d);
            AppLib.SetAppGlobalValue("ordPnlHdrWaiterNameFontSize", 14d);
            AppLib.SetAppGlobalValue("ordPnlHdrOrderTimerFontSize", 20d);
            //    шрифт заголовка таблицы блюд
            AppLib.SetAppGlobalValue("ordPnlDishTblHeaderFontSize", 10d);
            //    шрифт строки блюда
            AppLib.SetAppGlobalValue("ordPnlDishLineFontSize", 24d);
            // кнопки прокрутки страниц
            AppLib.SetAppGlobalValue("dishesPanelScrollButtonSize", 100d);
        }


        // заполнить словарь разрешенных переходов между состояниями
        // словарь хранится в свойствах приложения (key = "StatesAllowedForMove")
        // значения читаются из config-файла
        private static void setStatesAllowedForMoveInAppProps()
        {
            List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>> statesAllowedForMove;
            var oProp = AppLib.GetAppGlobalValue("StatesAllowedForMove");
            // создать или очистить словарь
            if (oProp == null) statesAllowedForMove = new List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>>();
            else
            {
                statesAllowedForMove = (List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>>)oProp;
                statesAllowedForMove.Clear();
            }

            // настройки взять из config-файла
            bool isSpecial = false;
            string cfgValue = AppLib.GetAppSetting("KDSMode");
            if (cfgValue.IsNull() == false)
            {
                KDSModeEnum mode;
                if (Enum.TryParse<KDSModeEnum>(cfgValue, out mode))
                {
                    switch (mode)
                    {
                        case KDSModeEnum.Special:
                            isSpecial = true;
                            break;

                        case KDSModeEnum.Cook:
                            // повар
                            statesAllowedForMove.Add(new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.WaitingCook, OrderStatusEnum.Cooking));
                            statesAllowedForMove.Add(new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.Cooking,  OrderStatusEnum.Ready));
                            statesAllowedForMove.Add(new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.Cancelled,  OrderStatusEnum.CancelConfirmed));
                            break;

                        case KDSModeEnum.Waiter:
                            // официант
                            statesAllowedForMove.Add(new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.Ready,  OrderStatusEnum.Took));
                            break;

                        case KDSModeEnum.Manager:
                            // менеджер на фронте
                            statesAllowedForMove.Add(new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.WaitingCook, OrderStatusEnum.Cooking));
                            statesAllowedForMove.Add(new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.WaitingCook, OrderStatusEnum.Cancelled));
                            statesAllowedForMove.Add(new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.Cooking,  OrderStatusEnum.Cancelled));
                            statesAllowedForMove.Add(new KeyValuePair<OrderStatusEnum, OrderStatusEnum>(OrderStatusEnum.Ready, OrderStatusEnum.Cancelled));
                            break;

                        default:
                            break;
                    }
                }
            }
            else
            {
                isSpecial = true;
            }

            if (isSpecial)
            {
                cfgValue = AppLib.GetAppSetting("StatesAllowedForMove");
                if (cfgValue.IsNull() == false)
                {
                    statesAllowedForMove = AppLib.StringToStatusCords(cfgValue);
                }
            }

            AppLib.SetAppGlobalValue("StatesAllowedForMove", statesAllowedForMove);
        }


    }  // class App
}
