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
            // установить текущий каталог на папку с приложением
            string curDir = System.IO.Directory.GetCurrentDirectory();
            if (curDir.Last() != '\\') curDir += "\\";
            string appDir = AppEnvironment.GetAppDirectory();
            if (curDir != appDir)
            {
                AppLib.WriteLogInfoMessage("Текущий каталог изменен на папку приложения: " + appDir);
                System.IO.Directory.SetCurrentDirectory(appDir);
            }

            // защита PSW-файлом
            //args = new string[] { "-autoGenLicence" };
            bool isLoyalClient = ((args != null) && args.Contains("-autoGenLicence"));
            pswLib.CheckProtectedResult checkProtectedResult;
            if (pswLib.Hardware.IsCurrentAppProtected("KDSWPFClient", out checkProtectedResult, null, isLoyalClient) == false)
            {
                string errMsg = string.Format("{0}{1}{1}{2}", checkProtectedResult.LogMessage, Environment.NewLine, checkProtectedResult.CustomMessage);
                appExit(2, errMsg);
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
#if (Release==false) && (DEBUG == false)
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
            // Имя или ip-адрес компьютера, на котором запущена КДС-служба
            setGlobStringValueFromCfg("KDSServiceHostName", "localhost");
            setGlobStringValueFromCfg("KDSClientName", "uniqClientName");  // УНИКАЛЬНОЕ ИМЯ КДС-КЛИЕНТА

            // звуковой файл, проигрываемый при появлении нового заказа
            setGlobStringValueFromCfg("NewOrderAudioAttention");
            // кликабельность заголовка заказа
            setGlobBoolValueFromCfg("OrderHeaderClickable");
            // кликабельность ингредиента НЕЗАВИСИМО от родительского блюда
            setGlobBoolValueFromCfg("IsIngredientsIndependent");
            // отображать ли на ЗАВИСИМЫХ ингредиентах таймеры
            setGlobBoolValueFromCfg("ShowTimerOnDependIngr");
            // отображать ли заголовок ЗАКАЗА тем же статусом, что и ВСЕ, ОТОБРАЖАЕМЫЕ НА ДАННОМ КДС-е, блюда/ингредиенты
            setGlobBoolValueFromCfg("IsShowOrderStatusByAllShownDishes");

            // флажок группировки блюд по наименованию и суммирования количество порций
            setGlobBoolValueFromCfg("IsDishGroupAndSumQuantity", false);
            // флажок отрисовки вкладок фильтра статусов по-отдельности
            setGlobBoolValueFromCfg("IsMultipleStatusTabs", false);
           

            // **** РАЗМЕЩЕНИЕ ПАНЕЛЕЙ ЗАКАЗОВ
            setGlobIntValueFromCfg("OrdersColumnsCount", 4);        // кол-во столбцов заказов
            // масштабный коэффициент размера шрифтов панели заказа
            setGlobDoubleValueFromCfg("AppFontScale", 1.0d);
            setGlobIntValueFromCfg("OrdersPanelTopBotMargin", 40);  // отступ сверху/снизу для панели заказов, в пикселях
            setGlobIntValueFromCfg("OrderPanelTopMargin", 50);      // отступ между заказами по вертикали, в пикселях
            // отступ между заказами по горизонтали, в доли от ширины панели заказа
            setGlobDoubleValueFromCfg("OrderPanelLeftMargin", 0.15d);
            // кнопки прокрутки страниц, в пикселях
            setGlobDoubleValueFromCfg("OrdersPanelScrollButtonSize", 100d);

            // ** ЗАГОЛОВОК ЗАКАЗА
            // шрифты для панели заголовка заказа
            setGlobDoubleValueFromCfg("OrderPanelHdrLabelFontSize", 14d, "ordPnlHdrLabelFontSize"); // метки полей
            setGlobDoubleValueFromCfg("OrderPanelHdrTableNameFontSize", 20d, "ordPnlHdrTableNameFontSize"); // имя стола
            setGlobDoubleValueFromCfg("OrderPanelHdrOrderNumberFontSize", 22d, "ordPnlHdrOrderNumberFontSize"); // номер заказа
            setGlobDoubleValueFromCfg("OrderPanelHdrWaiterNameFontSize", 14d, "ordPnlHdrWaiterNameFontSize"); // имя официанта
            setGlobDoubleValueFromCfg("OrderPanelHdrOrderCreateDateFontSize", 20d, "ordPnlHdrOrderCreateDateFontSize"); // дата создания заказа
            setGlobDoubleValueFromCfg("OrderPanelHdrOrderTimerFontSize", 24d, "ordPnlHdrOrderTimerFontSize"); // таймер заказа
            // шрифт шапки таблицы блюд
            setGlobDoubleValueFromCfg("OrderPanelDishTblHeaderFontSize", 10d, "ordPnlDishTblHeaderFontSize");
            // ** СТРОКА БЛЮДА
            // шрифт строки блюда
            setGlobDoubleValueFromCfg("OrderPanelDishIndexFontSize", 16d, "ordPnlDishIndexFontSize");
            setGlobDoubleValueFromCfg("OrderPanelDishNameFontSize", 20d, "ordPnlDishNameFontSize");
            setGlobDoubleValueFromCfg("OrderPanelIngrNameFontSize", 20d, "ordPnlIngrNameFontSize");
            setGlobDoubleValueFromCfg("OrderPanelDishCommentFontSize", 18d, "ordPnlDishCommentFontSize");
            setGlobDoubleValueFromCfg("OrderPanelDishQuantityFontSize", 22d, "ordPnlDishQuantityFontSize");
            setGlobDoubleValueFromCfg("OrderPanelDishTimerFontSize", 20d, "ordPnlDishTimerFontSize");

            // шрифт разделителя блюд (напр. Подача ** или Продол.см.на след.стр.)
            setGlobDoubleValueFromCfg("OrderPanelItemsDelimiterFontSize", 16d, "ordPnlDishDelimiterFontSize");

            // различные текстовые строки
            setGlobStringValueFromCfg("DishesSupplyName", "Подача");
            setGlobStringValueFromCfg("ContinueOrderNextPage", "Продолж. см.на СЛЕДУЮЩЕЙ стр.");
            setGlobStringValueFromCfg("ContinueOrderPrevPage", "Начало см.на ПРЕДЫДУЩЕЙ стр.");

            // Максимальное количество архивных файлов журнала. По умолчанию, равно 0 (нет ограничения).
            setGlobIntValueFromCfg("MaxLogFiles", 0);

            // флаги типов записей журнала приложения
            setGlobBoolValueFromCfg("IsWriteTraceMessages", true);
            setGlobBoolValueFromCfg("TraceOrdersDetails", true);
            setGlobBoolValueFromCfg("IsLogClientAction", true);

            // таймаут открытия канала
            WpfHelper.SetAppGlobalValue("OpenTimeoutSeconds", 3);

            // кисти читаются в служ.классе BrushHelper
            BrushHelper.FillAppBrushes();
        }


        #region get value from config and put it to global vars set
        private static void setGlobStringValueFromCfg(string cfgElementName, string defaultValue = null, string globVarName = null)
        {
            string sCfgValue = CfgFileHelper.GetAppSetting(cfgElementName);

            WpfHelper.SetAppGlobalValue(((globVarName == null) ? cfgElementName : globVarName),
               (string.IsNullOrEmpty(sCfgValue) ? defaultValue : sCfgValue));
        }

        private static void setGlobBoolValueFromCfg(string cfgElementName, bool defaultValue = false, string globVarName = null)
        {
            string sCfgValue = CfgFileHelper.GetAppSetting(cfgElementName);

            WpfHelper.SetAppGlobalValue(((globVarName == null) ? cfgElementName : globVarName),
               (string.IsNullOrEmpty(sCfgValue) ? defaultValue : sCfgValue.ToBool()));
        }

        private static void setGlobIntValueFromCfg(string cfgElementName, int defaultValue = 0, string globVarName = null)
        {
            string sCfgValue = CfgFileHelper.GetAppSetting(cfgElementName);

            WpfHelper.SetAppGlobalValue( ((globVarName == null) ? cfgElementName : globVarName), 
               (string.IsNullOrEmpty(sCfgValue) ? defaultValue : sCfgValue.ToInt()) );
        }

        private static void setGlobDoubleValueFromCfg(string cfgElementName, double defaultValue = 0d, string globVarName = null)
        {
            string sCfgValue = CfgFileHelper.GetAppSetting(cfgElementName);

            WpfHelper.SetAppGlobalValue(((globVarName == null) ? cfgElementName : globVarName),
               (string.IsNullOrEmpty(sCfgValue) ? defaultValue : sCfgValue.ToDouble()));
        }
        #endregion


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
            // есть ли сгруппированные блюда
            if (dishModel.GroupedDishIds.IsNull() == false)
            {
                int[] ids = dishModel.GroupedDishIds.Split(';').Select(sId => sId.ToInt()).ToArray();
                for (int i = 0; i < ids.Length; i++)
                {
                    dataProvider.SetNewDishStatus(orderModel.Id, ids[i], newState);
                }
            }

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
                        {
                            dataProvider.SetNewDishStatus(orderModel.Id, ingr.Id, newState);

                            // если есть сгруппированные ингредиенты
                            if (ingr.GroupedDishIds.IsNull() == false)
                            {
                                int[] ids = ingr.GroupedDishIds.Split(';').Select(sId => sId.ToInt()).ToArray();
                                for (int i = 0; i < ids.Length; i++)
                                {
                                    dataProvider.SetNewDishStatus(orderModel.Id, ids[i], newState);
                                }
                            }
                        }
                    }
                }
            }
        } // method


    }  // class App
}
