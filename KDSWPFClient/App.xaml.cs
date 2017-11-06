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

namespace KDSWPFClient
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // тестовая задержка
        private static Random rnd = new Random();

        /// <summary>
        /// Application Entry Point.
        /// </summary>
        [System.STAThreadAttribute()]
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "4.0.0.0")]
        public static void Main(string[] args)
        {
            AppLib.InitAppLogger();

            AppLib.WriteLogInfoMessage("************  Start KDS Client (WPF) *************");
            AppLib.WriteLogInfoMessage("Версия файла {0}: {1}", GetAppFileName(), GetAppVersion());
            AppLib.WriteLogInfoMessage(GetEnvironmentString());

            // установить текущий каталог на папку с приложением
            string appDir = AppEnvironment.GetAppDirectory();
            if (System.IO.Directory.GetCurrentDirectory() != appDir)
            {
                AppLib.WriteLogInfoMessage("Текущий каталог изменен на папку приложения: " + appDir);
                System.IO.Directory.SetCurrentDirectory(appDir);
            }

            // check registration
            if (ProtectedProgramm() == false) Environment.Exit(1);

            KDSWPFClient.App app = new KDSWPFClient.App();

            getAppLayout();
            // splash
            //string fileName = (AppLib.IsAppVerticalLayout ? "Images/bg 3ver 1080x1920 splash.png" : "Images/bg 3hor 1920x1080 splash.png");
            //SplashScreen splashScreen = null;
            //SplashScreen splashScreen = new SplashScreen(fileName);
            //splashScreen.Show(true);

            // настройка приложения
            app.InitializeComponent();  // определенные в app.xaml

            setAppGlobalValues();  // для хранения в свойствах приложения (из config-файла или др.)
            AppLib.WriteLogInfoMessage("App settings from config file: " + CfgFileHelper.GetAppSettingsFromConfigFile());

            // создать каналы
            AppLib.WriteLogInfoMessage("Создаю клиента для работы со службой KDSService - START");
            AppDataProvider dataProvider = new AppDataProvider();
            try
            {
                dataProvider.CreateGetChannel();
                dataProvider.CreateSetChannel();
                AppLib.WriteLogInfoMessage("Создаю клиента для работы со службой KDSService - FINISH");
            }
            catch (Exception)
            {
                // КДСы могут быть уже запущены, а служба еще нет!
                AppLib.WriteLogErrorMessage("Data provider error: " + dataProvider.ErrorMessage);

                //if (splashScreen != null) splashScreen.Close(TimeSpan.FromMinutes(10));
                //string msg = string.Format("Ошибка создания каналов к службе KDSService: {0}\n{1}", dataProvider.ErrorMessage, ex.ToString());
                //MessageBox.Show(msg, "АВАРИЙНОЕ ЗАВЕРШЕНИЕ ПРИЛОЖЕНИЯ", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                //Environment.Exit(1);
            }
            
            // и получить словари и настройки от службы
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

            AppPropsHelper.SetAppGlobalValue("AppDataProvider", dataProvider);

            // прочитать из config-а и сохранить в свойствах приложения режим КДС
            KDSModeHelper.Init();

            // основное окно приложения
            MainWindow mainWindow = new MainWindow(args);
            // создать и сохранить в свойствах приложения служебные окна (ColorLegend, StateChange)
            AppPropsHelper.SetAppGlobalValue("ColorLegendWindow", new ColorLegend());  // окно легенды
            // окно изменения статуса
            AppPropsHelper.SetAppGlobalValue("StateChangeWindow", new StateChange());

            app.Run(mainWindow);

            if (dataProvider != null) { dataProvider.Dispose(); dataProvider = null; }
            AppLib.WriteLogInfoMessage("************  End KDS Client (WPF)  *************");
        }  // Main()

        private static string GetAppFileName()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            return assembly.ManifestModule.Name;
        }

        private static string GetAppVersion()
        {
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
            return fvi.FileVersion;
        }

        private static string GetEnvironmentString()
        {
            return string.Format("Environment: machine={0}, user={1}, current directory={2}, OS version={3}, isOS64bit={4}, processor count={5}, free RAM={6} Mb",
                Environment.MachineName, Environment.UserName, Environment.CurrentDirectory, Environment.OSVersion, Environment.Is64BitOperatingSystem, Environment.ProcessorCount, Hardware.getAvailableRAM());
        }


        private static bool ProtectedProgramm()
        {
            // файл E_init.PSW должен находиться в папке приложения
            string fileName = AppEnvironment.GetAppDirectory() + "E_init.PSW";
            string cpuid = Hardware.getCPUID();
            string msg = string.Format("Ваш продукт не зарегистрирован.\nСообщите этот код службе поддержки\nтел: +380 (44)384-3213 (050)447-4476\n\n\t{0}\n\n(the number has been copied to the clipboard)", cpuid);

            if (File.Exists(fileName) == false)
            {
                // 2017-10-04 создать psw-файл для клиента
                Password.CreatePSWFile(fileName, cpuid);

                //AppLib.WriteLogErrorMessage(string.Format("Не найден файл: {0}, key {1}", fileName, cpuid));
                //Clipboard.Clear();
                //Clipboard.SetText(cpuid, TextDataFormat.Text);
                //MessageBox.Show(msg, "Проверка регистрации", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                //return false;
            }

            if (AppLib.SeeHardware(fileName, cpuid))
            {
                return true;
            }
            else
            {
                AppLib.WriteLogErrorMessage(string.Format("Софт не прошел проверку в ProtectedProgramm(), key {0}", cpuid));
                Clipboard.Clear();
                Clipboard.SetText(cpuid, TextDataFormat.Text);
                MessageBox.Show(msg, "Проверка регистрации", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return false;
            }
        }


        private static void getAppLayout()
        {
            AppPropsHelper.SetAppGlobalValue("screenWidth", SystemParameters.PrimaryScreenWidth);
            AppPropsHelper.SetAppGlobalValue("screenHeight", SystemParameters.PrimaryScreenHeight);
        }

        
        private static void setAppGlobalValues()
        {
            string cfgValue;

            cfgValue = CfgFileHelper.GetAppSetting("KDSServiceHostName");
            AppPropsHelper.SetAppGlobalValue("KDSServiceHostName", cfgValue);

            cfgValue = CfgFileHelper.GetAppSetting("IsWriteTraceMessages");
            AppPropsHelper.SetAppGlobalValue("IsWriteTraceMessages", (cfgValue == null) ? false : cfgValue.ToBool());
            cfgValue = CfgFileHelper.GetAppSetting("TraceOrdersDetails");
            AppPropsHelper.SetAppGlobalValue("TraceOrdersDetails", (cfgValue == null) ? false : cfgValue.ToBool());
            cfgValue = CfgFileHelper.GetAppSetting("IsLogClientAction");
            AppPropsHelper.SetAppGlobalValue("IsLogClientAction", (cfgValue == null) ? false : cfgValue.ToBool());

            // **** РАЗМЕЩЕНИЕ ПАНЕЛЕЙ ЗАКАЗОВ
            //   кол-во столбцов заказов, если нет в config-е, то сохранить значение по умолчанию
            cfgValue = CfgFileHelper.GetAppSetting("OrdersColumnsCount");
            AppPropsHelper.SetAppGlobalValue("OrdersColumnsCount", (cfgValue == null) ? 4 : cfgValue.ToInt());
            //   отступ сверху/снизу для панели заказов
            cfgValue = CfgFileHelper.GetAppSetting("OrdersPanelTopBotMargin");
            AppPropsHelper.SetAppGlobalValue("OrdersPanelTopBotMargin", (cfgValue == null) ? 30 : cfgValue.ToInt());
            //   отступ между заказами по вертикали
            cfgValue = CfgFileHelper.GetAppSetting("OrderPanelTopMargin");
            AppPropsHelper.SetAppGlobalValue("OrderPanelTopMargin", (cfgValue == null) ? 30 : cfgValue.ToInt());
            //   отступ между заказами по горизонтали
            cfgValue = CfgFileHelper.GetAppSetting("OrderPanelLeftMargin");
            AppPropsHelper.SetAppGlobalValue("OrderPanelLeftMargin", (cfgValue == null) ? 0.15 : cfgValue.ToDouble());
            // кнопки прокрутки страниц
            cfgValue = CfgFileHelper.GetAppSetting("OrdersPanelScrollButtonSize");
            AppPropsHelper.SetAppGlobalValue("OrdersPanelScrollButtonSize", (cfgValue == null) ? 100 : cfgValue.ToInt());

            cfgValue = CfgFileHelper.GetAppSetting("AppFontScale");
            AppPropsHelper.SetAppGlobalValue("AppFontScale", (cfgValue == null) ? 1.0d : cfgValue.ToDouble());

            // различные текстовые строки
            cfgValue = CfgFileHelper.GetAppSetting("DishesSupplyName");
            AppPropsHelper.SetAppGlobalValue("DishesSupplyName", (cfgValue == null) ? "Подача" : cfgValue);
            cfgValue = CfgFileHelper.GetAppSetting("ContinueOrderNextPage");
            AppPropsHelper.SetAppGlobalValue("ContinueOrderNextPage", (cfgValue == null) ? "Продолж. см.на СЛЕДУЮЩЕЙ стр." : cfgValue);
            cfgValue = CfgFileHelper.GetAppSetting("ContinueOrderPrevPage");
            AppPropsHelper.SetAppGlobalValue("ContinueOrderPrevPage", (cfgValue == null) ? "Начало см.на ПРЕДЫДУЩЕЙ стр." : cfgValue);

            // ** ЗАГОЛОВОК ЗАКАЗА
            // шрифты для панели заказа
            AppPropsHelper.SetAppGlobalValue("ordPnlHdrLabelFontSize", 14d);
            AppPropsHelper.SetAppGlobalValue("ordPnlHdrTableNameFontSize", 20d);
            AppPropsHelper.SetAppGlobalValue("ordPnlHdrOrderNumberFontSize", 22d);
            AppPropsHelper.SetAppGlobalValue("ordPnlHdrWaiterNameFontSize", 14d);
            AppPropsHelper.SetAppGlobalValue("ordPnlHdrOrderTimerFontSize", 24d);
            //    шрифт заголовка таблицы блюд
            AppPropsHelper.SetAppGlobalValue("ordPnlDishTblHeaderFontSize", 10d);
            // ** СТРОКА БЛЮДА
            // шрифт строки блюда
            AppPropsHelper.SetAppGlobalValue("ordPnlDishLineFontSize", 20d);
            // минимальная высота строки блюда
            double dishLineMinHeight = (double)AppPropsHelper.GetAppGlobalValue("screenHeight") / 20d;
            AppPropsHelper.SetAppGlobalValue("ordPnlDishLineMinHeight", dishLineMinHeight);
            
            // шрифт разделителя блюд (напр. Подача **)
            cfgValue = CfgFileHelper.GetAppSetting("OrderPanelItemsDelimiterFontSize");
            AppPropsHelper.SetAppGlobalValue("ordPnlDishDelimiterFontSize", (cfgValue == null)? 16 : cfgValue.ToInt());

            cfgValue = CfgFileHelper.GetAppSetting("NewOrderAudioAttention");
            if (cfgValue != null) AppPropsHelper.SetAppGlobalValue("NewOrderAudioAttention", cfgValue);

            cfgValue = CfgFileHelper.GetAppSetting("OrderHeaderClickable");
            AppPropsHelper.SetAppGlobalValue("OrderHeaderClickable", cfgValue.ToBool());
            cfgValue = CfgFileHelper.GetAppSetting("IsIngredientsIndependent");
            AppPropsHelper.SetAppGlobalValue("IsIngredientsIndependent", cfgValue.ToBool());

            cfgValue = CfgFileHelper.GetAppSetting("IsShowOrderStatusByAllShownDishes");
            AppPropsHelper.SetAppGlobalValue("IsShowOrderStatusByAllShownDishes", cfgValue.ToBool());
        }

        // открыть/закрыть легенду цветов таймеров
        internal static void OpenColorLegendWindow()
        {
            ColorLegend colorLegendWin = (ColorLegend)AppPropsHelper.GetAppGlobalValue("ColorLegendWindow");
            if ((colorLegendWin != null) && !AppLib.IsOpenWindow("ColorLegend")) colorLegendWin.Show();
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
                    StateChange win = (StateChange)AppPropsHelper.GetAppGlobalValue("StateChangeWindow");
                    win.CurrentState = currentState;
                    win.Order = orderModel;
                    win.Dish = dishModel;
                    win.AllowedStates = allowedStates;
                    AppLib.SetWinSizeToMainWinSize(win);

                    win.ShowDialog();
                    AppLib.WriteLogClientAction("Close StateChange win, result: {0}", win.CurrentState.ToString());

                    // изменить статус
                    AppDataProvider dataProvider = (AppDataProvider)AppPropsHelper.GetAppGlobalValue("AppDataProvider");
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
            bool isConfirmedReadyState = (bool)AppPropsHelper.GetAppGlobalValue("UseReadyConfirmedState", false);

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
