using KDSWPFClient.Lib;
using IntegraLib;
using IntegraWPFLib;
using SplashScreenLib;
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
            Splasher.Splash = new SplashScreen();
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
            MessageListener.Instance.ReceiveMessage("Проверка лицензии...");
            //bool isLoyalClient = false;
            //isLoyalClient = ((args != null) && args.Contains("-autoGenLicence"));
            // ключ реестра HKLM\Software\Integra\autoGenLicence = 01 (binary)
            //if (isLoyalClient == false) isLoyalClient = RegistryHelper.IsExistsAutoGenLicenceKey();
            pswLib.CheckProtectedResult checkProtectedResult;
            if (pswLib.Hardware.IsCurrentAppProtected("KDSWPFClient", out checkProtectedResult, null, false) == false)
            {
                string errMsg = string.Format("{0}{1}{1}{2}", checkProtectedResult.LogMessage, Environment.NewLine, checkProtectedResult.CustomMessage);
                appExit(2, errMsg);
            }

            MessageListener.Instance.ReceiveMessage("Получение версии приложения...");
            AppLib.WriteLogInfoMessage("Инициализация KDS-клиента...");

            // проверка наличия уникального имени клиента в конфиг-файле
            cfgValue = CfgFileHelper.GetAppSetting("KDSClientName");
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
            ITSAssemblyInfo asmInfo = new ITSAssemblyInfo("IntegraLib");
            AppLib.WriteLogInfoMessage(" - Integra lib: '{0}', Version {1}", asmInfo.FullFileName, asmInfo.Version);
            asmInfo = new ITSAssemblyInfo("IntegraWPFLib");
            AppLib.WriteLogInfoMessage(" - Integra WPF lib: '{0}', Version {1}", asmInfo.FullFileName, asmInfo.Version);

            MessageListener.Instance.ReceiveMessage("Получение параметров окружения...");
            AppLib.WriteLogInfoMessage(AppEnvironment.GetEnvironmentString());

            getAppLayout();

            // настройка приложения
            MessageListener.Instance.ReceiveMessage("Получение параметров приложения...");
#if !DEBUG
            System.Threading.Thread.Sleep(500);
#endif
            app.InitializeComponent();  // определенные в app.xaml

            setAppGlobalValues();  // для хранения в свойствах приложения (из config-файла или др.)
            AppLib.WriteLogInfoMessage("App settings from config file: " + CfgFileHelper.GetAppSettingsFromConfigFile());

            // создать каналы
            AppLib.WriteLogInfoMessage("Создаю клиента для работы со службой KDSService - START");
            AppDataProvider dataProvider = new AppDataProvider();
            try
            {
                MessageListener.Instance.ReceiveMessage("Создание канала получения данных...");
#if !DEBUG
                System.Threading.Thread.Sleep(1000);
#endif
                dataProvider.CreateGetChannel();

                MessageListener.Instance.ReceiveMessage("Создание канала установки данных...");
#if !DEBUG
                System.Threading.Thread.Sleep(1000);
#endif
                dataProvider.CreateSetChannel();

                AppLib.WriteLogInfoMessage("Создаю клиента для работы со службой KDSService - FINISH");
            }
            catch (Exception)
            {
                AppLib.WriteLogErrorMessage("Data provider error: " + dataProvider.ErrorMessage);
            }

            // и получить словари и настройки от службы
            MessageListener.Instance.ReceiveMessage("Получаю словари и настройки от службы KDSService...");
#if !DEBUG
            System.Threading.Thread.Sleep(500);
#endif
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
#if !DEBUG
            System.Threading.Thread.Sleep(500);
#endif
            KDSModeHelper.Init();

            // создать и сохранить в свойствах приложения служебные окна (ColorLegend, StateChange)
            MessageListener.Instance.ReceiveMessage("Создаю служебные окна...");
#if !DEBUG
            System.Threading.Thread.Sleep(500);
#endif
            WpfHelper.SetAppGlobalValue("ColorLegendWindow", new ColorLegend());  // окно легенды
            // окно изменения статуса
            WpfHelper.SetAppGlobalValue("StateChangeWindow", new StateChange());

            // основное окно приложения
            MessageListener.Instance.ReceiveMessage("Инициализация окна приложения...");
#if !DEBUG
            System.Threading.Thread.Sleep(1000);
#endif
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
            int icfgValue;

            // Имя или ip-адрес компьютера, на котором запущена КДС-служба
            setGlobStringValueFromCfg("KDSServiceHostName", "localhost");
            // Стандартный интервал опроса КДС-службы, в мсек. Допустимые значения от 500 до 2000. По умолчанию - 1000.
            setGlobIntValueFromCfg("KDSServiceIntervalStd", 1000);
            icfgValue = (int)WpfHelper.GetAppGlobalValue("KDSServiceIntervalStd");
            if ((icfgValue < 500) || (icfgValue > 2000)) WpfHelper.SetAppGlobalValue("KDSServiceIntervalStd", 1000);
            // Уменьшенный интервал опроса КДС-службы, в мсек. Используется, когда клиент не смог получить данные от службы по причине того, что служба была занята чтением данных из БД. Допустимые значения от 50 до 450. По умолчанию - 100. Если не указано, то стандартный интервал таймера НЕ изменяется на альтернативный.
            setGlobIntValueFromCfg("KDSServiceIntervalAlt", 100);
            icfgValue = (int)WpfHelper.GetAppGlobalValue("KDSServiceIntervalAlt");
            if ((icfgValue < 50) || (icfgValue > 450)) WpfHelper.SetAppGlobalValue("KDSServiceIntervalAlt", 100);

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

            // боковая панель
            // Ширина кнопочной панели в процентах от ширины экрана.
            setGlobIntValueFromCfg("ControlPanelPercentWidth", 5);
            icfgValue = (int)WpfHelper.GetAppGlobalValue("ControlPanelPercentWidth");
            if (icfgValue <= 0) WpfHelper.SetAppGlobalValue("ControlPanelPercentWidth", 5);
            // флажок отрисовки вкладок фильтра статусов по-отдельности
            setGlobBoolValueFromCfg("IsMultipleStatusTabs", false);
            // флажок группировки блюд по наименованию и суммирования количество порций
            setGlobBoolValueFromCfg("IsDishGroupAndSumQuantity", false);
           

            // **** РАЗМЕЩЕНИЕ ПАНЕЛЕЙ ЗАКАЗОВ
            setGlobIntValueFromCfg("OrdersColumnsCount", 4);        // кол-во столбцов заказов
            icfgValue = (int)WpfHelper.GetAppGlobalValue("OrdersColumnsCount");
            if (icfgValue <= 0) WpfHelper.SetAppGlobalValue("OrdersColumnsCount", 4);
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
            setGlobBoolValueFromCfg("TraceScreenDrawDetails", true);

            // таймаут открытия канала
            setGlobIntValueFromCfg("OpenTimeoutSeconds", 3);

            // кисти читаются в служ.классе BrushHelper
            BrushHelper.FillAppBrushes();
        }


#region get value from config and put it to global vars set
        private static void setGlobStringValueFromCfg(string cfgElementName, string defaultValue = null, string globVarName = null)
        {
            string sCfgValue = CfgFileHelper.GetAppSetting(cfgElementName);

            string key = ((globVarName == null) ? cfgElementName : globVarName);
            string value = (string.IsNullOrEmpty(sCfgValue) ? defaultValue : sCfgValue);

            WpfHelper.SetAppGlobalValue(key, value);
        }

        private static void setGlobBoolValueFromCfg(string cfgElementName, bool defaultValue = false, string globVarName = null)
        {
            string sCfgValue = CfgFileHelper.GetAppSetting(cfgElementName);

            string key = ((globVarName == null) ? cfgElementName : globVarName);
            bool value = (string.IsNullOrEmpty(sCfgValue) ? defaultValue : sCfgValue.ToBool());

            WpfHelper.SetAppGlobalValue(key, value);
        }

        private static void setGlobIntValueFromCfg(string cfgElementName, int defaultValue = 0, string globVarName = null)
        {
            string sCfgValue = CfgFileHelper.GetAppSetting(cfgElementName);

            string key = ((globVarName == null) ? cfgElementName : globVarName);
            int value = (string.IsNullOrEmpty(sCfgValue) ? defaultValue : sCfgValue.ToInt());

            WpfHelper.SetAppGlobalValue(key, value);
        }

        private static void setGlobDoubleValueFromCfg(string cfgElementName, double defaultValue = 0d, string globVarName = null)
        {
            string sCfgValue = CfgFileHelper.GetAppSetting(cfgElementName);

            string key = ((globVarName == null) ? cfgElementName : globVarName);
            double value = (string.IsNullOrEmpty(sCfgValue) ? defaultValue : sCfgValue.ToDouble());

            WpfHelper.SetAppGlobalValue(key, value);
        }
#endregion


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
                        bool result = false;
                        bool finReady = getFinReadyValue(newState); // флаг терминального окончания готовки

                        #region изменение статуса
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
                                if (dataProvider.LockOrder(orderModel.Id))
                                {
                                    AppLib.WriteLogClientAction("lock " + sLogMsg + ": success");

                                    OrderStatusEnum preState = dishModel.Status;
                                    // изменить статус блюда с ингредиентами
                                    result = changeStatusDishWithIngrs(dataProvider, orderModel, dishModel, newState);

                                    string sBuf = "delock " + sLogMsg + " - " + (DateTime.Now - dtTmr).ToString();
                                    if (dataProvider.DelockOrder(orderModel.Id))
                                        AppLib.WriteLogClientAction(sBuf + ": success");
                                    else
                                        AppLib.WriteLogClientAction(sBuf + ": NOT success");
                                }
                                else
                                {
                                    AppLib.WriteLogClientAction("lock " + sLogMsg + ": NOT success");
                                }
                            }

                            // изменение состояния ЗАКАЗА, то изменяем все равно поблюдно
                            else if (dishModel == null)
                            {
                                sLogMsg = string.Format("orderId {0} (num {1}) for change order status to {2}", orderModel.Id, orderModel.Number, newState.ToString());
                                DateTime dtTmr = DateTime.Now;

                                if (dataProvider.LockOrder(orderModel.Id))
                                {
                                    AppLib.WriteLogClientAction("lock " + sLogMsg + ": success");
                                    List<int> dishIds = new List<int>(); // dishes id has changed
                                    // меняем статус БЛЮД в заказе, если блюдо разрешено для данного КДСа
                                    foreach (OrderDishViewModel item in orderModel.Dishes.Where(d => d.ParentUID.IsNull()))
                                    {
                                        if (DishesFilter.Instance.Checked(item))
                                        {
                                            // изменить статус блюда с ингредиентами
                                            result = changeStatusDishWithIngrs(dataProvider, orderModel, item, newState);
                                            if (result) { dishIds.Add(item.Id); }
                                        }
                                    }  // foreach

                                    string sBuf = "delock " + sLogMsg + " - " + (DateTime.Now - dtTmr).ToString();
                                    if (dataProvider.DelockOrder(orderModel.Id))
                                        AppLib.WriteLogClientAction(sBuf + ": success");
                                    else
                                        AppLib.WriteLogClientAction(sBuf + ": NOT success");
                                }
                                else
                                {
                                    AppLib.WriteLogClientAction("lock " + sLogMsg + ": NOT success");
                                }
                            }  // order status
                        }
                        catch (Exception ex)
                        {
                            AppLib.WriteLogErrorMessage(ex.ToString());
                            MessageBox.Show("Ошибка изменения состояния. Попробуйте еще раз.", "Ошибка записи нового состояния",MessageBoxButton.OK);
                        }
                        #endregion
                    }
                }  // if (allowedStates.Count != 0)

            } // if (allowedActions != null)

        }  // method

        private static bool getFinReadyValue(OrderStatusEnum state)
        {
            bool confReady = WpfHelper.GetAppGlobalBool("UseReadyConfirmedState");
            return (confReady && (state == OrderStatusEnum.ReadyConfirmed))
                || (!confReady && (state == OrderStatusEnum.Ready));
        }

        // изменение статуса блюда с ингредиентами
        private static bool changeStatusDishWithIngrs(AppDataProvider dataProvider, OrderViewModel orderModel, OrderDishViewModel dishModel, OrderStatusEnum newState)
        {
            // для отмененного блюда статус не менять
            //if (dishModel.Quantity < 0) return false;
            // эта настройка от КДС-сервиса
            bool isConfirmedReadyState = (bool)WpfHelper.GetAppGlobalValue("UseReadyConfirmedState", false);

            bool result = false;

            // изменить статус блюда
            result = dataProvider.SetNewDishStatus(orderModel.Id, dishModel.Id, newState);
            if (result == false) return false;

            // есть ли сгруппированные блюда
            if (dishModel.GroupedDishIds.IsNull() == false)
            {
                int[] ids = dishModel.GroupedDishIds.Split(';').Select(sId => sId.ToInt()).ToArray();
                for (int i = 0; i < ids.Length; i++)
                {
                    result = dataProvider.SetNewDishStatus(orderModel.Id, ids[i], newState);
                    if (result == false) return false;
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
                            || (newState == OrderStatusEnum.Cooking)
                            || (newState == OrderStatusEnum.Took)
                            || (newState == OrderStatusEnum.CancelConfirmed))
                        {
                            result = dataProvider.SetNewDishStatus(orderModel.Id, ingr.Id, newState);
                            if (result == false) return false;

                            // если есть сгруппированные ингредиенты
                            if (ingr.GroupedDishIds.IsNull() == false)
                            {
                                int[] ids = ingr.GroupedDishIds.Split(';').Select(sId => sId.ToInt()).ToArray();
                                for (int i = 0; i < ids.Length; i++)
                                {
                                    result = dataProvider.SetNewDishStatus(orderModel.Id, ids[i], newState);
                                    if (result == false) return false;
                                }
                            }
                        }
                    }
                }
            }

            return true;
        } // method


    }  // class App
}
