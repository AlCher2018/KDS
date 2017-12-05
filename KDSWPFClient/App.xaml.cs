using KDSWPFClient.ServiceReference1;
using KDSWPFClient.Lib;
using IntegraLib;
using System;
using System.Globalization;
using System.Windows;
using System.Collections.Generic;
using KDSWPFClient.ViewModel;
using KDSWPFClient.Model;
using KDSWPFClient.View;
using System.Windows.Media;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Compression;
using SplashScreen;

namespace KDSWPFClient
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static string ClientName;


        /// <summary>
        /// Application Entry Point.
        /// </summary>
        [System.STAThreadAttribute()]
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "4.0.0.0")]
        public static void Main(string[] args)
        {
            // splash
            Splasher.Splash = new SplashScreen.SplashScreen();
            Splasher.ShowSplash();
            //for (int i = 0; i < 5000; i += 1)
            //{
            //    MessageListener.Instance.ReceiveMessage(string.Format("Load module {0}", i));
            //    Thread.Sleep(1);
            //}
            //string fileName = (AppLib.IsAppVerticalLayout ? "Images/bg 3ver 1080x1920 splash.png" : "Images/bg 3hor 1920x1080 splash.png");
            //SplashScreen splashScreen = null;
            //SplashScreen splashScreen = new SplashScreen(fileName);
            //splashScreen.Show(true);

            // текст в MessageListener.Instance прибинден к текстовому полю на сплэше
            MessageListener.Instance.ReceiveMessage("Инициализация журнала событий...");
            AppLib.InitAppLogger();

            AppLib.WriteLogInfoMessage("************  Start KDS Client (WPF) *************");

            // защита PSW-файлом
            pswLib.CheckProtectedResult checkProtectedResult;
            if (pswLib.Hardware.IsCurrentAppProtected("KDSWPFClient", out checkProtectedResult) == false)
            {
                appExit(2, checkProtectedResult.CustomMessage);
            }

            MessageListener.Instance.ReceiveMessage("Получение версии приложения...");
            AppLib.WriteLogInfoMessage("Инициализация KDS-клиента...");

            // проверка наличия уникального имени клиента в конфиг-файле
            string cfgValue = CfgFileHelper.GetAppSetting("KDSClientName");
            if (cfgValue.IsNull() == true)
            {
                cfgValue = "Не указано имя КДС-клиента в файле AppSettings.config.";
                appExit(3, cfgValue);
            }
            if (cfgValue.Equals("uniqClientName", StringComparison.OrdinalIgnoreCase))
            {
#if DEBUG==false
                cfgValue = "Измените имя КДС-клиента в файле AppSettings.config";
                appExit(3, cfgValue);
#endif
            }
            KDSWPFClient.App app = new KDSWPFClient.App();
            WpfHelper.SetAppGlobalValue("KDSClientName", cfgValue);
            App.ClientName = System.Convert.ToString(WpfHelper.GetAppGlobalValue("KDSClientName"));
            AppLib.WriteLogInfoMessage(" - имя КДС-клиента: {0}", App.ClientName);

            // информация о файлах и сборках
            AppLib.WriteLogInfoMessage(" - файл: {0}, Version {1}", AppEnvironment.GetAppFullFile(), AppEnvironment.GetAppVersion());
            ITSAssemmblyInfo asmInfo = new ITSAssemmblyInfo("IntegraLib");
            AppLib.WriteLogInfoMessage(" - Integra lib: '{0}', Version {1}", asmInfo.FullFileName, asmInfo.Version);

            MessageListener.Instance.ReceiveMessage("Получение параметров окружения...");
            AppLib.WriteLogInfoMessage(AppEnvironment.GetEnvironmentString());

            // установить текущий каталог на папку с приложением
            string curDir = System.IO.Directory.GetCurrentDirectory();
            if (curDir.Last() != '\\') curDir += "\\";
            string appDir = AppEnvironment.GetAppDirectory();
            if (curDir != appDir)
            {
                AppLib.WriteLogInfoMessage("Текущий каталог изменен на папку приложения: " + appDir);
                System.IO.Directory.SetCurrentDirectory(appDir);
            }

            getAppLayout();

            // настройка приложения
            MessageListener.Instance.ReceiveMessage("Получение параметров приложения...");
            System.Threading.Thread.Sleep(500);
            app.InitializeComponent();  // определенные в app.xaml

            setAppGlobalValues();  // для хранения в свойствах приложения (из config-файла или др.)
            AppLib.WriteLogInfoMessage("App settings from config file: " + CfgFileHelper.GetAppSettingsFromConfigFile());

            // создать каналы
            AppLib.WriteLogInfoMessage("Создаю клиента для работы со службой KDSService - START");
            AppDataProvider dataProvider = new AppDataProvider();
            try
            {
                MessageListener.Instance.ReceiveMessage("Создание канала получения данных...");
                System.Threading.Thread.Sleep(1000);
                dataProvider.CreateGetChannel();

                MessageListener.Instance.ReceiveMessage("Создание канала установки данных...");
                System.Threading.Thread.Sleep(1000);
                dataProvider.CreateSetChannel();

                AppLib.WriteLogInfoMessage("Создаю клиента для работы со службой KDSService - FINISH");
            }
            catch (Exception)
            {
                AppLib.WriteLogErrorMessage("Data provider error: " + dataProvider.ErrorMessage);
            }

            // и получить словари и настройки от службы
            MessageListener.Instance.ReceiveMessage("Получаю словари и настройки от службы KDSService...");
            System.Threading.Thread.Sleep(500);
            AppLib.WriteLogInfoMessage("Получаю словари и настройки от службы KDSService - START");
            if (dataProvider.SetDictDataFromService() == false)
            {
                // КДСы могут быть уже запущены, а служба еще нет!
                AppLib.WriteLogErrorMessage("Data provider error: " + dataProvider.ErrorMessage);
                //if (splashScreen != null) splashScreen.Close(TimeSpan.FromMinutes(10));
                //MessageBox.Show("Ошибка получения словарей от службы KDSService:" + Environment.NewLine + dataProvider.ErrorMessage, "АВАРИЙНОЕ ЗАВЕРШЕНИЕ ПРИЛОЖЕНИЯ", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                //Environment.Exit(2);
            }
            else
                AppLib.WriteLogInfoMessage("Получаю словари и настройки от службы KDSService - FINISH");

            WpfHelper.SetAppGlobalValue("AppDataProvider", dataProvider);

            // прочитать из config-а и сохранить в свойствах приложения режим КДС
            MessageListener.Instance.ReceiveMessage("Получаю из config-файла режим работы КДС...");
            System.Threading.Thread.Sleep(500);
            KDSModeHelper.Init();

            // создать и сохранить в свойствах приложения служебные окна (ColorLegend, StateChange)
            MessageListener.Instance.ReceiveMessage("Создаю служебные окна...");
            System.Threading.Thread.Sleep(500);
            WpfHelper.SetAppGlobalValue("ColorLegendWindow", new ColorLegend());  // окно легенды
            // окно изменения статуса
            WpfHelper.SetAppGlobalValue("StateChangeWindow", new StateChange());

            // основное окно приложения
            MessageListener.Instance.ReceiveMessage("Инициализация окна приложения...");
            System.Threading.Thread.Sleep(1000);
            MainWindow mainWindow = new MainWindow(args);
            app.MainWindow = mainWindow;
            app.Run(mainWindow);

            if (dataProvider != null) { dataProvider.Dispose(); dataProvider = null; }
            AppLib.WriteLogInfoMessage("************  End KDS Client (WPF)  *************");
        }  // Main()

        private static void appExit(int exitCode, string errMsg)
        {
            AppLib.WriteLogErrorMessage("Аварийное завершение программы: " + errMsg);

            MessageBox.Show(errMsg, "Аварийное завершение программы", MessageBoxButton.OK, MessageBoxImage.Stop);

            Environment.Exit(exitCode);
        }

        private static void getAppLayout()
        {
            WpfHelper.SetAppGlobalValue("screenWidth", SystemParameters.PrimaryScreenWidth);
            WpfHelper.SetAppGlobalValue("screenHeight", SystemParameters.PrimaryScreenHeight);
        }

        
        private static void setAppGlobalValues()
        {
            string cfgValue;

            cfgValue = CfgFileHelper.GetAppSetting("KDSServiceHostName");
            WpfHelper.SetAppGlobalValue("KDSServiceHostName", cfgValue);

            cfgValue = CfgFileHelper.GetAppSetting("IsWriteTraceMessages");
            WpfHelper.SetAppGlobalValue("IsWriteTraceMessages", (cfgValue == null) ? false : cfgValue.ToBool());
            cfgValue = CfgFileHelper.GetAppSetting("TraceOrdersDetails");
            WpfHelper.SetAppGlobalValue("TraceOrdersDetails", (cfgValue == null) ? false : cfgValue.ToBool());
            cfgValue = CfgFileHelper.GetAppSetting("IsLogClientAction");
            WpfHelper.SetAppGlobalValue("IsLogClientAction", (cfgValue == null) ? false : cfgValue.ToBool());

            // **** РАЗМЕЩЕНИЕ ПАНЕЛЕЙ ЗАКАЗОВ
            //   кол-во столбцов заказов, если нет в config-е, то сохранить значение по умолчанию
            cfgValue = CfgFileHelper.GetAppSetting("OrdersColumnsCount");
            WpfHelper.SetAppGlobalValue("OrdersColumnsCount", (cfgValue == null) ? 4 : cfgValue.ToInt());
            //   отступ сверху/снизу для панели заказов
            cfgValue = CfgFileHelper.GetAppSetting("OrdersPanelTopBotMargin");
            WpfHelper.SetAppGlobalValue("OrdersPanelTopBotMargin", (cfgValue == null) ? 30 : cfgValue.ToInt());
            //   отступ между заказами по вертикали
            cfgValue = CfgFileHelper.GetAppSetting("OrderPanelTopMargin");
            WpfHelper.SetAppGlobalValue("OrderPanelTopMargin", (cfgValue == null) ? 30 : cfgValue.ToInt());
            //   отступ между заказами по горизонтали
            cfgValue = CfgFileHelper.GetAppSetting("OrderPanelLeftMargin");
            WpfHelper.SetAppGlobalValue("OrderPanelLeftMargin", (cfgValue == null) ? 0.15 : cfgValue.ToDouble());
            // кнопки прокрутки страниц
            cfgValue = CfgFileHelper.GetAppSetting("OrdersPanelScrollButtonSize");
            WpfHelper.SetAppGlobalValue("OrdersPanelScrollButtonSize", (cfgValue == null) ? 100 : cfgValue.ToInt());

            cfgValue = CfgFileHelper.GetAppSetting("AppFontScale");
            WpfHelper.SetAppGlobalValue("AppFontScale", (cfgValue == null) ? 1.0d : cfgValue.ToDouble());

            // различные текстовые строки
            cfgValue = CfgFileHelper.GetAppSetting("DishesSupplyName");
            WpfHelper.SetAppGlobalValue("DishesSupplyName", (cfgValue == null) ? "Подача" : cfgValue);
            cfgValue = CfgFileHelper.GetAppSetting("ContinueOrderNextPage");
            WpfHelper.SetAppGlobalValue("ContinueOrderNextPage", (cfgValue == null) ? "Продолж. см.на СЛЕДУЮЩЕЙ стр." : cfgValue);
            cfgValue = CfgFileHelper.GetAppSetting("ContinueOrderPrevPage");
            WpfHelper.SetAppGlobalValue("ContinueOrderPrevPage", (cfgValue == null) ? "Начало см.на ПРЕДЫДУЩЕЙ стр." : cfgValue);

            // ** ЗАГОЛОВОК ЗАКАЗА
            // шрифты для панели заказа
            WpfHelper.SetAppGlobalValue("ordPnlHdrLabelFontSize", 14d);
            WpfHelper.SetAppGlobalValue("ordPnlHdrTableNameFontSize", 20d);
            WpfHelper.SetAppGlobalValue("ordPnlHdrOrderNumberFontSize", 22d);
            WpfHelper.SetAppGlobalValue("ordPnlHdrWaiterNameFontSize", 14d);
            WpfHelper.SetAppGlobalValue("ordPnlHdrOrderTimerFontSize", 24d);
            //    шрифт заголовка таблицы блюд
            WpfHelper.SetAppGlobalValue("ordPnlDishTblHeaderFontSize", 10d);
            // ** СТРОКА БЛЮДА
            // шрифт строки блюда
            WpfHelper.SetAppGlobalValue("ordPnlDishLineFontSize", 20d);
            // минимальная высота строки блюда
            double dishLineMinHeight = (double)WpfHelper.GetAppGlobalValue("screenHeight") / 20d;
            WpfHelper.SetAppGlobalValue("ordPnlDishLineMinHeight", dishLineMinHeight);
            
            // шрифт разделителя блюд (напр. Подача **)
            cfgValue = CfgFileHelper.GetAppSetting("OrderPanelItemsDelimiterFontSize");
            WpfHelper.SetAppGlobalValue("ordPnlDishDelimiterFontSize", (cfgValue == null)? 16 : cfgValue.ToInt());

            cfgValue = CfgFileHelper.GetAppSetting("NewOrderAudioAttention");
            if (cfgValue != null) WpfHelper.SetAppGlobalValue("NewOrderAudioAttention", cfgValue);

            cfgValue = CfgFileHelper.GetAppSetting("OrderHeaderClickable");
            WpfHelper.SetAppGlobalValue("OrderHeaderClickable", cfgValue.ToBool());

            cfgValue = CfgFileHelper.GetAppSetting("IsIngredientsIndependent");
            WpfHelper.SetAppGlobalValue("IsIngredientsIndependent", cfgValue.ToBool());
            cfgValue = CfgFileHelper.GetAppSetting("ShowTimerOnDependIngr");
            WpfHelper.SetAppGlobalValue("ShowTimerOnDependIngr", cfgValue.ToBool());

            cfgValue = CfgFileHelper.GetAppSetting("IsShowOrderStatusByAllShownDishes");
            WpfHelper.SetAppGlobalValue("IsShowOrderStatusByAllShownDishes", cfgValue.ToBool());

            // таймаут открытия канала
            WpfHelper.SetAppGlobalValue("OpenTimeoutSeconds", 3);
        }

        // открыть/закрыть легенду цветов таймеров
        internal static void OpenColorLegendWindow()
        {
            ColorLegend colorLegendWin = (ColorLegend)WpfHelper.GetAppGlobalValue("ColorLegendWindow");
            if ((colorLegendWin != null) && !WpfHelper.IsOpenWindow("ColorLegend")) colorLegendWin.Show();
        }


        // из обработчика MouseUp объектов DishPanel и OrderPanelHeader
        internal static void OpenStateChangeWindow(OrderViewModel orderModel, OrderDishViewModel dishModel)
        {
            if ((orderModel == null) && (dishModel == null)) return;

            // из РАЗРЕШЕННЫХ переходов выбрать переходы, ДОСТУПНЫЕ для текущего состояния
            OrderStatusEnum currentState = (OrderStatusEnum)((dishModel == null) ? orderModel.OrderStatusId : dishModel.DishStatusId);
            KDSModeEnum kdsMode = KDSModeHelper.CurrentKDSMode;  // текущий режим КДС

            List<KeyValuePair<OrderStatusEnum, OrderStatusEnum>> allowedActions = KDSModeHelper.DefinedKDSModes[kdsMode].AllowedActions;
            if (allowedActions != null)
            {
                List<OrderStatusEnum> allowedStates = allowedActions.Where(p => (p.Key == currentState)).Select(p => p.Value).ToList();
                // при клике по ЗАКАЗУ проверить статус отображаемых на данном КДСе позиций
                if (dishModel == null)
                {
                    OrderStatusEnum statAllDishes = (OrderStatusEnum)(int)orderModel.StatusAllowedDishes;  //AppLib.GetStatusAllDishes(orderModel.Dishes);
                    if (orderModel.StatusAllowedDishes != StatusEnum.None)
                    {
                        currentState = statAllDishes;  // текущее состояние - по блюдам!
                        // и, если в разрешенных переходах есть пары с таким ключем, т.е. ВСЕ блюда находятся в состоянии, которое есть в разрешенных переходах
                        var tmpList = allowedActions.Where(s => s.Key == statAllDishes).ToList();
                        // то отображаем эти переходы, а не переходы из состояния заказа!
                        if (tmpList != null)
                        {
                            allowedStates = tmpList.Select(p => p.Value).ToList();
                        }
                    }
                }

                StringBuilder sb = new StringBuilder();
                allowedStates.ForEach(status => sb.Append(status.ToString()));
                AppLib.WriteLogClientAction("Open StateChange win, allowedStates: " + sb.ToString());

                // открываем окно изменения статуса
                if (allowedStates.Count != 0)
                {
                    StateChange win = (StateChange)WpfHelper.GetAppGlobalValue("StateChangeWindow");
                    win.CurrentState = currentState;
                    win.Order = orderModel;
                    win.Dish = dishModel;
                    win.AllowedStates = allowedStates;
                    WpfHelper.SetWinSizeToMainWinSize(win);

                    win.ShowDialog();
                    AppLib.WriteLogClientAction("Close StateChange win, result: {0}", win.CurrentState.ToString());

                    // изменить статус
                    AppDataProvider dataProvider = (AppDataProvider)WpfHelper.GetAppGlobalValue("AppDataProvider");
                    OrderStatusEnum newState = win.CurrentState;
                    if ((newState != OrderStatusEnum.None) && (newState != currentState) && (dataProvider != null))
                    {
                        string sLogMsg;
                        try
                        {
                            // проверить set-канал
                            if (!dataProvider.EnableSetChannel) dataProvider.CreateSetChannel();
                            if (!dataProvider.EnableSetChannel) dataProvider.CreateSetChannel();

                            // изменение состояния БЛЮДА и разрешенных ингредиентов (2017-07-26)
                            if (dishModel != null)
                            {
                                sLogMsg = string.Format("orderId {0} (num {1}) for change status dishId {2} ({3}) to {4}", orderModel.Id, orderModel.Number, dishModel.Id, dishModel.DishName, newState.ToString());
                                DateTime dtTmr = DateTime.Now;
                                AppLib.WriteLogClientAction("lock " + sLogMsg);
                                dataProvider.LockOrder(orderModel.Id);

                                // изменить статус блюда с ингредиентами
                                changeStatusDishWithIngrs(dataProvider, orderModel, dishModel, newState);

                                AppLib.WriteLogClientAction("delock " + sLogMsg + " - " + (DateTime.Now-dtTmr).ToString());
                                dataProvider.DelockOrder(orderModel.Id);
                            }

                            // изменение состояния ЗАКАЗА, то изменяем все равно поблюдно
                            else if (dishModel == null)
                            {
                                sLogMsg = string.Format("orderId {0} (num {1}) for change order status to {2}", orderModel.Id, orderModel.Number, newState.ToString());
                                DateTime dtTmr = DateTime.Now;
                                AppLib.WriteLogClientAction("lock " + sLogMsg);
                                dataProvider.LockOrder(orderModel.Id);

                                // меняем статус БЛЮД в заказе, если блюдо разрешено для данного КДСа
                                foreach (OrderDishViewModel item in orderModel.Dishes.Where(d => d.ParentUID.IsNull()))
                                {
                                    if (DishesFilter.Instance.Checked(item))
                                    {
                                        // изменить статус блюда с ингредиентами
                                        changeStatusDishWithIngrs(dataProvider, orderModel, item, newState);
                                    }
                                }  // foreach

                                AppLib.WriteLogClientAction("delock orderId {0} (num {1}) - {2}", orderModel.Id, orderModel.Number, (DateTime.Now - dtTmr).ToString());
                                dataProvider.DelockOrder(orderModel.Id);

                            }  // order status
                        }
                        catch (Exception ex)
                        {
                            AppLib.WriteLogErrorMessage(ex.ToString());
                            MessageBox.Show("Ошибка изменения состояния. Попробуйте еще раз.", "Ошибка записи нового состояния",MessageBoxButton.OK);
                        }

                    } // if
                }  // if (allowedStates.Count != 0)

            } // if (allowedActions != null)

        }  // method

        // изменение статуса блюда с ингредиентами
        private static void changeStatusDishWithIngrs(AppDataProvider dataProvider, OrderViewModel orderModel, OrderDishViewModel dishModel, OrderStatusEnum newState)
        {
            // эта настройка от КДС-сервиса
            bool isConfirmedReadyState = (bool)WpfHelper.GetAppGlobalValue("UseReadyConfirmedState", false);

            // изменить статус блюда
            dataProvider.SetNewDishStatus(orderModel.Id, dishModel.Id, newState);

            // если блюдо, то изменить статус ингредиентов
            if (dishModel.ParentUID.IsNull())
            {
                // изменить статус ингредиентов при условиях: 
                // - разрешен на данном КДСе 
                // - блюдо переходит в статус Готово, Выдан или ПодтвОтмены
                OrderDishViewModel[] ingrs = orderModel.Dishes.Where(d => (d.ParentUID != null) && (d.ParentUID == dishModel.UID)).ToArray();
                if (ingrs.Length > 0)
                {
                    foreach (OrderDishViewModel ingr in ingrs)
                    {
                        if (DishesFilter.Instance.Checked(ingr)
                            || (!isConfirmedReadyState && (newState == OrderStatusEnum.Ready))
                            || (isConfirmedReadyState && (newState == OrderStatusEnum.ReadyConfirmed))
                            || (newState == OrderStatusEnum.Took)
                            || (newState == OrderStatusEnum.CancelConfirmed))
                            dataProvider.SetNewDishStatus(orderModel.Id, ingr.Id, newState);
                    }
                }
            }
        } // method


    }  // class App
}
