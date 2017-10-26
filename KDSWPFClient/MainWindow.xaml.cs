using KDSWPFClient.Lib;
using KDSWPFClient.ServiceReference1;
using KDSWPFClient.View;
using KDSWPFClient.ViewModel;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using KDSWPFClient.Model;
using System.ComponentModel;
using System.Runtime.InteropServices;
using IntegraLib;

namespace KDSWPFClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int GWL_STYLE = -16;
        private const int WS_SYSMENU = 0x80000;
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private double _screenWidth, _screenHeight;

        private Timer _timer;
        private short _canInvokeUpdateOrders;
        private Timer _timerBackToOrderGroupByTime;  //  таймер возврата группировки заказов по времени
        private Timer _timerBackToFirstPage;        // таймер возврата на первую страницу
        private double _autoBackTimersInterval;     // интервал для таймеров возврата

        private AppDataProvider _dataProvider;
        private bool _traceOrderDetails;
        private DishesFilter _dishesFilter = DishesFilter.Instance;

        // классы для циклического перебора клиентских условий отображения блюд
        private ListLooper<OrderGroupEnum> _orderGroupLooper;
        // набор фильтров состояний/вкладок (имя, кисти фона и текста, список состояний)
        private ListLooper<KDSUserStatesSet> _orderStatesLooper;

        // страницы заказов
        private OrdersPages _pages;

        // служебные коллекции
        private List<OrderModel> _svcOrders;
        private List<int> _preOrdersId;
        private List<OrderViewModel> _viewOrders;  // для отображения на экране

        // временные списки для удаления неразрешенных блюд/заказов, т.к. от службы получаем ВСЕ блюда и ВСЕ заказы в нетерминальных состояниях
        private List<OrderModel> _delOrderIds;  // для удаления заказов
        private List<OrderViewModel> _delOrderViewIds;  // для удаления заказов
        private List<int> _delDishIds;  // для удаления блюд
        private List<string> _dishUIDs;  // UID-ы блюд для поиска "висячих" ингредиентов

        // переменные для опеределения условий отображения окна настройки
        private int _adminBitMask;
        private Timer _adminTimer;
        private DateTime _adminDate;

        private bool _mayGetData;

        // звуки
        System.Media.SoundPlayer _wavPlayer;

        // CONSTRUCTOR
        public MainWindow(string[] args)
        {
            InitializeComponent();

            this.Loaded += MainWindow_Loaded;

            _screenWidth = (double)AppPropsHelper.GetAppGlobalValue("screenWidth");
            _screenHeight = (double)AppPropsHelper.GetAppGlobalValue("screenHeight");

            this.Top = 0; this.Left = 0;
            this.Width = _screenWidth; this.Height = _screenHeight;

            // админ-кнопка для открытия окна конфигурации
            btnCFG.Visibility = (CfgFileHelper.GetAppSetting("IsShowCFGButton").ToBool() || args.Contains("-adm")) ? Visibility.Visible : Visibility.Hidden;

            _traceOrderDetails = (bool)AppPropsHelper.GetAppGlobalValue("TraceOrdersDetails");

            _dataProvider = (AppDataProvider)AppPropsHelper.GetAppGlobalValue("AppDataProvider");
            setWindowsTitle();

            // класс для циклического перебора группировки заказов
            // в коде используется ТЕКУЩИЙ объект, но на вкладках отображается СЛЕДУЮЩИЙ !!!
            _orderGroupLooper = new ListLooper<OrderGroupEnum>(new[] { OrderGroupEnum.ByCreateTime, OrderGroupEnum.ByOrderNumber });
            setOrderGroupTab();
            setOrderStatusFilterTab();

            // отступы панели заказов (ViewBox) внутри родительской панели
            double verMargin = Convert.ToDouble(AppPropsHelper.GetAppGlobalValue("OrdersPanelTopBotMargin"));
            this.vbxOrders.Margin = new Thickness(0, verMargin, 0, verMargin);

            _preOrdersId = new List<int>();
            _viewOrders = new List<OrderViewModel>();

            setAutoBackTimers();
            // основной таймер опроса сервиса
            _timer = new Timer(1000) { AutoReset = false };
            _timer.Elapsed += _timer_Elapsed;
            _timer.Start(); _canInvokeUpdateOrders = -1;

            // кнопки переключения страниц
            btnSetPagePrevious.Height = btnSetPagePrevious.Width = btnSetPageNext.Width = btnSetPageNext.Height = Convert.ToDouble(AppPropsHelper.GetAppGlobalValue("OrdersPanelScrollButtonSize"));

            // временные коллекции
            _delOrderIds = new List<OrderModel>();
            _delOrderViewIds = new List<OrderViewModel>();
            _delDishIds = new List<int>();
            _dishUIDs = new List<string>();

            _adminTimer = new Timer() { Interval = 3000d, AutoReset = false };
            _adminTimer.Elapsed += _adminTimer_Elapsed;

            // звук предупреждения о появлении нового заказа
            _wavPlayer = new System.Media.SoundPlayer();
            var wavFile = AppPropsHelper.GetAppGlobalValue("NewOrderAudioAttention");
            if (wavFile != null)
            {
                _wavPlayer.SoundLocation = AppEnvironment.GetAppDirectory("Audio") + wavFile;
                _wavPlayer.LoadAsync();
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // hide Close button
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~WS_SYSMENU);

            // размер канвы для панелей заказов
            recalcOrderPanelsLayot();
            _pages = new OrdersPages(vbxOrders);

            // настройки кнопок пользов.группировки и фильтрации
            double hRow = grdUserConfig.RowDefinitions[1].ActualHeight;
            double wRow = grdUserConfig.ActualWidth;
            double rad = 0.2 * wRow;
            CornerRadius crnRad = new CornerRadius(rad, 0d, 0d, rad);
            Thickness leftBtnMargin = new Thickness(rad, 0d, 0d, 0d);
            Thickness leftTbMargin = new Thickness(0.1d * wRow, 0d, -hRow, -wRow);

            btnOrderGroup.Margin = leftBtnMargin;
            btnOrderGroup.CornerRadius = crnRad;
            tbOrderGroup.Width = hRow; tbOrderGroup.Height = wRow;
            tbOrderGroup.FontSize = 0.35d * wRow;
            tbOrderGroup.Margin = leftTbMargin;

            btnDishStatusFilter.Margin = leftBtnMargin;
            btnDishStatusFilter.CornerRadius = crnRad;
            tbDishStatusFilter.Width = hRow; tbDishStatusFilter.Height = wRow;
            tbDishStatusFilter.FontSize = 0.35d * wRow;
            tbDishStatusFilter.Margin = leftTbMargin;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if ((_timer != null) && _timer.Enabled)
            {
                _timer.Stop(); _timer.Dispose();
            }
            AppLib.CloseChildWindows();
            base.OnClosing(e);
        }

        // основной таймер отображения панелей заказов
        // запускается каждые 100 мсек
        private void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            //DateTime dt = DateTime.Now;
            //short seconds = (short)dt.Second;  // от 0 до 59
            //if ((dt.Millisecond <= 200) && (_canInvokeUpdateOrders != seconds))
            //{
                _timer.Stop();
                //_canInvokeUpdateOrders = seconds;
                _mayGetData = false;
                try
                {
                    // потеря связи со службой
                    if (_dataProvider.EnableGetChannel == false)
                    {
                        AppLib.WriteLogTraceMessage("потеря связи со службой получения данных, пересоздаю get-канал...");
                        _mayGetData = _dataProvider.CreateGetChannel();
                    }
                    else
                        _mayGetData = true;
                }
                catch (Exception ex)
                {
                    AppLib.WriteLogErrorMessage("** Ошибка обновления заказов: {0}", AppLib.GetShortErrMessage(ex));
                }

                this.Dispatcher.Invoke(new Action(updateOrders));

            //}

            _timer.Start();  // т.к. AutoReset = false !!!
        }  // method


        // *** Основной метод получения заказов от службы ***
        private void updateOrders()
        {
            //int maxLines = _pages.GetMaxDishesCountOnPage();

            if (_mayGetData)
            {
                if (tblChannelErrorMessage.Visibility == Visibility.Visible)
                    tblChannelErrorMessage.Visibility = Visibility.Hidden;
            }

            // очистить канву от заказов и отобразить сообщение об ошибке связи
            else
            {
                if (tblChannelErrorMessage.Visibility != Visibility.Visible) tblChannelErrorMessage.Visibility = Visibility.Visible;
                _pages.ClearPages();
                if (_viewOrders.Count > 0) _viewOrders.Clear();
                AppLib.WriteLogOrderDetails("can't get orders due to service status Falted");
                return;
            }

            DateTime dtTmr1 = DateTime.Now;

            // получить заказы от сервиса
            List<int> statusesIDs = _orderStatesLooper.Current.States.Select(e => (int)e).ToList();
            List<int> clientDeps = (from d in _dataProvider.Departments.Values where d.IsViewOnKDS select d.Id).ToList();
            AppLib.WriteLogOrderDetails("svc.GetOrders('{0}', '{1}', '{2}', '{3}') - START", Environment.MachineName, string.Join(",", statusesIDs), string.Join(",", clientDeps), _orderGroupLooper.Current.ToString());
            try
            {
                _svcOrders = _dataProvider.GetOrders(statusesIDs, clientDeps, _orderGroupLooper.Current);
            }
            catch (Exception ex)
            {
                AppLib.WriteLogErrorMessage("Ошибка получения данных от КДС-службы: {0}", AppLib.GetShortErrMessage(ex));
                return;
            }

            if (_traceOrderDetails)
            {
                if (_svcOrders == null)
                    AppLib.WriteLogOrderDetails(" - от службы получено 0 заказов");
                else
                {
                    AppLib.WriteLogOrderDetails(" - от службы получено заказов: {0}, {1}", _svcOrders.Count, _logOrderInfo(_svcOrders));
                }
            }

            if (_svcOrders == null) return;
            // клиент не смог получить заказы, т.к. служба еще читала данные из БД - 
            // уменьшить интервал таймера до 100 мсек
            else if (_svcOrders.Count == 0)
            {
                _timer.Interval = 90;
                AppLib.WriteLogTraceMessage(" - set timer.Interval = 0,1 sec");
                return;
            }
            // иначе вернуться на стандартный интервал в 1 сек
            else if (_timer.Interval != 1000)
            {
                _timer.Interval = 1000;
                AppLib.WriteLogTraceMessage(" - set timer.Interval = 1 sec");
            }

            // условие проигрывания мелодии при появлении нового заказа
            // появились ли в svcOrders (УЖЕ ОТФИЛЬТРОВАННОМ ПО ОТДЕЛАМ И СТАТУСАМ) заказы, 
            // которых нет в preOrdersId, т.е. новые? (поиск по Id)
            int[] curOrdersId = _svcOrders.Select(o => o.Id).Distinct().ToArray();  // собрать уникальные Id
            if ((_preOrdersId.Count > 0) 
                || ((_preOrdersId.Count==0) && (_svcOrders.Count != 0)))
            {
                foreach (int curId in curOrdersId)
                    if (!_preOrdersId.Contains(curId)) {
                        _wavPlayer.Play() ; break;
                    }
                _preOrdersId.Clear();
            }
            _preOrdersId.AddRange(curOrdersId);

            // *** ОБНОВИТЬ _viewOrdes (для отображения на экране) ДАННЫМИ ИЗ svcOrders (получено из БД)
            // обновить внутреннюю коллекцию заказов данными, полученными от сервиса
            // в случае с группировкой по времени и разбивкой заказов на несколько панелей AppLib.JoinSortedLists() работает НЕПРАВИЛЬНО!!!
            //bool isViewRepaint = AppLib.JoinSortedLists<OrderViewModel, OrderModel>(_viewOrders, svcOrders);
            // поэтому сделано уникальной процедурой
            if (_traceOrderDetails) AppLib.WriteLogOrderDetails("   обновление служебной коллекции заказов (для отображения на экране)...");
            bool isViewRepaint2 = false;
            try
            {
                isViewRepaint2 = updateViewOrdersList(_svcOrders);
            }
            catch (Exception ex)
            {
                AppLib.WriteLogErrorMessage("Ошибка обновления служебной коллекции заказов для отображения на экране: {0}", ex.ToString());
            }

            // 2017-07-24 по заявке Ридченко
            // удалить заказы, у которых StatusAllowedDishes не входит в отображаемые состояния
            _delOrderViewIds.Clear();
            StatusEnum allDishesStatus = StatusEnum.None;
            foreach (OrderViewModel item in _viewOrders)
            {
                allDishesStatus = AppLib.GetStatusAllDishesOwnDeps(item.Dishes);
                if (item.StatusAllowedDishes != allDishesStatus)
                {
                    item.StatusAllowedDishes = allDishesStatus;
                    isViewRepaint2 = true;
                }

                if ((item.StatusAllowedDishes != StatusEnum.None) 
                    && (_dishesFilter.IsStatusAllowed((int)item.StatusAllowedDishes) == false))
                {
                    _delOrderViewIds.Add(item);
                    isViewRepaint2 = true;
                }
            }
            _delOrderViewIds.ForEach(o => _viewOrders.Remove(o));

            if (_traceOrderDetails)
            {
                AppLib.WriteLogOrderDetails("   для отображения на экране заказов: {0}; {1} - {2}", _viewOrders.Count, _logOrderInfo(_viewOrders), (isViewRepaint2 ? "ПЕРЕРИСОВКА всех заказов" : "только счетчики"));
            }

            // перерисовать, если на экране было пусто, а во _viewOrders появились заказы
            if (!isViewRepaint2) isViewRepaint2 = ((_pages.CurrentPage.Children.Count == 0) && (_viewOrders.Count != 0));
            // перерисовать полностью
            if (isViewRepaint2 == true) repaintOrders("update Orders from KDS service");

            AppLib.WriteLogOrderDetails("svc.GetOrders(...) - FINISH - " + (DateTime.Now - dtTmr1).ToString());

        }  // method

        private string _logOrderInfo(List<OrderModel> orders)
        {
            string retVal = null;
            if (orders.Count > 100)
                retVal = "> 100 !!";
            else
                retVal = "id/Num/dishes: " + string.Join(", ", orders.Select(o => string.Format("{0}/{1}/{2}", o.Id.ToString(), o.Number, o.Dishes.Count.ToString())));

            return retVal;
        }
        private string _logOrderInfo(List<OrderViewModel> orders)
        {
            string retVal = "id/Num/dishes: ";
            if (orders.Count > 0) retVal += string.Join(", ", orders.Select(o => string.Format("{0}/{1}/{2}", o.Id.ToString(), o.Number, o.Dishes.Count.ToString())));

            return retVal;
        }


        #region updateViewOrdersList()
        // обновить _viewOrders данными из svcOrders
        private bool updateViewOrdersList(List<OrderModel> svcOrders)
        {
            bool isViewRepaint = false;
            OrderViewModel curViewOrder;
            int index = -1;  // порядковый номер
            if (_viewOrders.Count > svcOrders.Count)
            {
                int delIndexFrom = (svcOrders.Count == 0) ? 0 : svcOrders.Count - 1;
                _viewOrders.RemoveRange(delIndexFrom, _viewOrders.Count - svcOrders.Count);
                isViewRepaint = true;
            }
            foreach (OrderModel om in svcOrders)
            {
                index++;
                // добавить
                if (index == _viewOrders.Count)
                {
                    OrderViewModel newOM = new OrderViewModel(om, index + 1);
                    _viewOrders.Add(newOM);
                    isViewRepaint = true;
                }
                else
                {
                    curViewOrder = _viewOrders[index];
                    if ((curViewOrder.Number == om.Number) && (curViewOrder.CreateDate == om.CreateDate)
                        && compareOrderDishes(om, curViewOrder))
                    {
                        curViewOrder.UpdateFromSvc(om);
                        if ((curViewOrder is IContainInnerCollection)
                            && ((curViewOrder as IContainInnerCollection).IsInnerListUpdated)
                            && !isViewRepaint) isViewRepaint = true;
                    }
                    else
                    {
                        // удалить в целевом списке все от текущей позиции включительно и до конца
                        _viewOrders.RemoveRange(index, _viewOrders.Count - index);
                        // и вставить новый объект
                        OrderViewModel newOM = new OrderViewModel(om, index + 1);
                        _viewOrders.Add(newOM);
                        isViewRepaint = true;
                    }
                }
            }
            return isViewRepaint;
        }

        // возвращает true, если количество элементов в коллекциях блюд одинаково и элементы упорядочены по Id
        private bool compareOrderDishes(OrderModel srcOrder, OrderViewModel tgtOrder)
        {
            if (srcOrder.Dishes.Count != tgtOrder.Dishes.Count) return false;

            OrderDishModel[] srcDishes = new OrderDishModel[srcOrder.Dishes.Count];
            srcOrder.Dishes.Values.CopyTo(srcDishes, 0);

            bool retVal = true;
            for (int i=0; i < srcDishes.Length; i++)
            {
                if (srcDishes[i].Id != tgtOrder.Dishes[i].Id) { retVal = false; break; }
            }

            return retVal;
        }
        #endregion


        private void repaintOrders(string reason)
        {
            if (_pages == null) return;

            DateTime dtTmr = DateTime.Now;
            string sLogMsg = string.Format(" - redraw reason: {0}", reason);
            AppLib.WriteLogOrderDetails(sLogMsg + " - START");
            Cursor = System.Windows.Input.Cursors.Wait;

            // добавить заказы
            // TODO тестирование прохождения по коллекции заказов/блюд
            OrderPageHelper pageHelper = new OrderPageHelper(bufferOrderPanels);
            pageHelper.ResetOrderPanelSize();
            pageHelper.DrawOrderPanelsOnPage(_viewOrders, -1, -1, true);

            //Debug.Print("** создано {0} панелей", (orderPanelList == null) ? 0 : orderPanelList.Count);

            _pages.AddOrdersPanels(_viewOrders);

            setCurrentPage();
            Cursor = null; //System.Windows.Input.Cursors.Arrow;

            AppLib.WriteLogOrderDetails(sLogMsg + " - FINISH - " + (DateTime.Now - dtTmr).ToString());
        }

        // размеры элементов панели заказа рассчитываются из размеров vbxOrders (Viewbox)
        private void recalcOrderPanelsLayot()
        {
            //   кол-во столбцов заказов
            int cntCols = Convert.ToInt32(AppPropsHelper.GetAppGlobalValue("OrdersColumnsCount"));

            //   ширина столбцов заказов и расстояния между столбцами
            double pnlWidth = vbxOrders.ActualWidth;
            // wScr = wCol*cntCols + koef*wCol*(cntCols+1) ==> wCol = wScr / (cntCols + koef*(cntCols+1))
            // где, koef = доля поля от ширины колонки
            double koef = Convert.ToDouble(AppPropsHelper.GetAppGlobalValue("OrderPanelLeftMargin"));
            double colWidth = Math.Floor(pnlWidth / (cntCols + koef * (cntCols + 1)));
            double colMargin = Math.Floor(koef * colWidth);  // поле между заказами по горизонтали
            AppPropsHelper.SetAppGlobalValue("OrdersColumnWidth", colWidth);
            AppPropsHelper.SetAppGlobalValue("OrdersColumnMargin", colMargin);
        }


        #region change page
        // *** кнопки листания страниц ***
        private void btnSetPageNext_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_pages.SetNextPage()) setCurrentPage();
        }

        private void btnSetPagePrevious_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_pages.SetPreviousPage()) setCurrentPage();
        }

        // отображение кнопок с номерами страниц и вкл/выкл таймера возврата на первую страницу
        private void setCurrentPage()
        {
            btnSetPagePrevious.Visibility = Visibility.Hidden;
            btnSetPageNext.Visibility = Visibility.Hidden;
            if (_pages.Count == 0) return;

            // состояние кнопки перехода на предыдущюю страницу
            if ((_pages.CurrentPageIndex - 1) > 0)
            {
                tbPagePreviousNum.Text = "Стр. " + (_pages.CurrentPageIndex - 1).ToString();
                btnSetPagePrevious.Visibility = Visibility.Visible;
            }

            // и на следующую страницу
            if (_pages.CurrentPageIndex < _pages.Count)
            {
                tbPageNextNum.Text = "Стр. " + (_pages.CurrentPageIndex + 1).ToString();
                btnSetPageNext.Visibility = Visibility.Visible;
            }

            // таймер возврата на первую страницу
            if ((_timerBackToFirstPage != null) && (_autoBackTimersInterval > 0d))
            {
                if (_timerBackToFirstPage.Enabled) _timerBackToFirstPage.Stop();
                if (_pages.CurrentPageIndex > 1) _timerBackToFirstPage.Start();
            }
        }

        #endregion

        #region настройка приложения через ConfigEdit

        // ******
        // ФОРМА НАСТРОЕК И ОБНОВЛЕНИЕ ПОЛЕЙ ПОСЛЕ НАСТРОЙКИ ПАРАМЕТРОВ ПРИЛОЖЕНИЯ
        // ******
        private void openConfigPanel()
        {
            _timer.Stop();

            AppLib.WriteLogClientAction("Open ConfigEdit window...");

            ConfigEdit cfgEdit = new ConfigEdit() { DepartmentsDict = _dataProvider.Departments };
            cfgEdit.ShowDialog();

            string sLogMsg = string.Join("; ", cfgEdit.AppNewSettings.Select(s => s.Key + ":" + s.Value));
            AppLib.WriteLogClientAction("   changed: " + sLogMsg);

            //  ОБНОВЛЕНИЕ ПАРАМЕТРОВ ПРИЛОЖЕНИЯ
            if (cfgEdit.AppNewSettings.Count > 0)
            {
                if (cfgEdit.AppNewSettings.ContainsKey("depUIDs"))
                {
                }

                // обновить фильтр блюд
                if (cfgEdit.AppNewSettings.ContainsKey("KDSMode"))
                {
                    setWindowsTitle();
                    _orderStatesLooper = null;
                    setOrderStatusFilterTab();
                }
                else if (cfgEdit.AppNewSettings.ContainsKey("KDSModeSpecialStates") 
                    && (cfgEdit.AppNewSettings["KDSModeSpecialStates"].IsNull() == false))
                {
                    if (KDSModeHelper.CurrentKDSMode != KDSModeEnum.Special) KDSModeHelper.CurrentKDSMode = KDSModeEnum.Special;

                    setWindowsTitle();
                    _orderStatesLooper = null;
                    setOrderStatusFilterTab();
                }

                if (cfgEdit.AppNewSettings.ContainsKey("IsShowOrderStatusByAllShownDishes"))
                {
                    repaintOrders("change config parameter IsShowOrderStatusByAllShownDishes");
                }

                // масштаб шрифта
                // перерисовать полностью, т.к. по таймеру может все не обновиться
                if (cfgEdit.AppNewSettings.ContainsKey("AppFontScale"))
                {
                    AppPropsHelper.SetAppGlobalValue("AppFontScale", cfgEdit.AppNewSettings["AppFontScale"].ToDouble());
                    repaintOrders("change config parameter AppFontScale");  
                }

                // кол-во колонок заказов
                if (cfgEdit.AppNewSettings.ContainsKey("OrdersColumnsCount"))
                {
                    recalcOrderPanelsLayot();
                    _pages.ResetOrderPanelSize();

                    repaintOrders("change config parameter OrdersColumnsCount");  // перерисовать заказы
                }

                // интервал таймера сброса группировки заказов по номерам
                if (cfgEdit.AppNewSettings.ContainsKey("AutoReturnOrdersGroupByTime"))
                {
                    setBackTimersInterval();
                }

                // плановое время выноса блюда (ExpectedTake)
                if (cfgEdit.AppNewSettings.ContainsKey("ExpectedTake"))
                {
                    string newValue = cfgEdit.AppNewSettings["ExpectedTake"];
                    // сохраняем в config-файле сервиса
                    _dataProvider.SetExpectedTakeValue(newValue.ToInt());
                }

                // звуковой файл предупреждения о появлении нового заказа
                if (cfgEdit.AppNewSettings.ContainsKey("NewOrderAudioAttention"))
                {
                    string wavFile = cfgEdit.AppNewSettings["NewOrderAudioAttention"];
                    // сохранить в свойствах приложения 
                    AppPropsHelper.SetAppGlobalValue("NewOrderAudioAttention", wavFile);
                    // в config-файле
                    string errMsg;
                    CfgFileHelper.SaveAppSettings("NewOrderAudioAttention", wavFile,out errMsg);
                    // и загрузить в проигрыватель
                    _wavPlayer.SoundLocation = AppEnvironment.GetAppDirectory("Audio") + wavFile;
                    _wavPlayer.LoadAsync();
                }

            }
            cfgEdit = null;

            _timer.Start();
        }

        private void setWindowsTitle()
        {
            // отобразить в заголовке роль КДСа
            string title = "KDS - " + KDSModeHelper.CurrentKDSMode.ToString().ToUpper();
            // и отображаемые цеха
            if (_dataProvider != null)
            {
                string depNames = _dataProvider.GetDepNames();
                if (!depNames.IsNull()) title += " (" + depNames + ")";
            }

            this.Title = title;
        }

        #endregion

        #region Настройка приложения боковыми кнопками

        private void setAutoBackTimers()
        {
            // таймер автоматического перехода группировки заказов из "По номерам" в "По времени"
            _timerBackToOrderGroupByTime = new Timer() { AutoReset = false };
            _timerBackToOrderGroupByTime.Elapsed += setInitPageAndOrdersGroup;

            // таймер возврата на первую страницу
            _timerBackToFirstPage = new Timer() { AutoReset = false };
            _timerBackToFirstPage.Elapsed += setInitPageAndOrdersGroup;

            setBackTimersInterval();
        }

        private void setInitPageAndOrdersGroup(object sender, ElapsedEventArgs e)
        {
            if (_orderGroupLooper.Current != OrderGroupEnum.ByCreateTime)
            {
                if (_timerBackToFirstPage.Enabled) _timerBackToFirstPage.Stop();
                _orderGroupLooper.Current = OrderGroupEnum.ByCreateTime;
                this.Dispatcher.Invoke(setOrderGroupTab);
            }
            if (_pages.CurrentPageIndex > 1)
            {
                if (_timerBackToOrderGroupByTime.Enabled) _timerBackToOrderGroupByTime.Stop();
                _pages.SetFirstPage();
                this.Dispatcher.Invoke(setCurrentPage);
            }
        }

        private void setBackTimersInterval()
        {
            // интервал таймера взять из config-файла
            string cfgStr = CfgFileHelper.GetAppSetting("AutoReturnOrdersGroupByTime");
            // и перевести в мсек
            _autoBackTimersInterval = 1000d * ((cfgStr.IsNull()) ? 0d : cfgStr.ToDouble());
            if (_autoBackTimersInterval > 0d)
            {
                _timerBackToOrderGroupByTime.Interval = _autoBackTimersInterval;
                _timerBackToFirstPage.Interval = _autoBackTimersInterval;
            }
            else
            {
                _timerBackToOrderGroupByTime.Enabled = false;
                _timerBackToFirstPage.Enabled = false;
            }
        }

        // обновить элементы фильтра состояний блюд
        private void setOrderStatusFilterTab()
        {
            // если нет пользовательского фильтра состояний,
            if ((KDSModeHelper.CurrentKDSStates.StateSets == null) || (KDSModeHelper.CurrentKDSStates.StateSets.Count == 0))
            {
                // то уничтожить объект перебора значений
                if (_orderStatesLooper != null) _orderStatesLooper = null;
                // скрыть вкладку перебора фильтра состояний
                btnDishStatusFilter.Visibility = Visibility.Hidden;
            }

            // применить пользовательский фильтр состояний
            else
            {
                // попытаться создать объект перебора значений
                if (_orderStatesLooper == null)
                {
                    _orderStatesLooper = new ListLooper<KDSUserStatesSet>(KDSModeHelper.CurrentKDSStates.StateSets);

                    KDSUserStatesSet cookingSet = null;
                    // ПО УМОЛЧАНИЮ  набор состояний - "В Процессе"
                    //cookingSet = _userStatesLooper.InnerList.FirstOrDefault(s => s.Name == "В процессе");
                    // ПО УМОЛЧАНИЮ  набор состояний - первый: "Все статусы"
                    if ((_orderStatesLooper.InnerList != null) && (_orderStatesLooper.InnerList.Count > 0))
                        cookingSet = _orderStatesLooper.InnerList[0];

                    if (cookingSet != null) _orderStatesLooper.Current = cookingSet;
                }

                // текущий набор состояний
                KDSUserStatesSet statesSet = _orderStatesLooper.Current;
                _dishesFilter.SetAllowedStatuses(statesSet);

                // вкладка перебора фильтров состояний
                if (btnDishStatusFilter.Visibility == Visibility.Hidden) btnDishStatusFilter.Visibility = Visibility.Visible;

                btnDishStatusFilter.Background = statesSet.BackBrush;
                tbDishStatusFilter.Text = statesSet.Name;
                tbDishStatusFilter.Foreground = statesSet.FontBrush;
            }
        }

        // *************************
        // *** группировка заказов
        private void tbOrderGroup_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // сдвинуть текущий элемент
            _orderGroupLooper.SetNextIndex();

            // и отобразить следующий
            AppLib.WriteLogClientAction("Change Order group to " + _orderGroupLooper.Current.ToString());
            setOrderGroupTab();
        }

        // отобразить на вкладке СЛЕДУЮЩИЙ элемент!!
        private void setOrderGroupTab()
        {
            //OrderGroupEnum eOrderGroup = _orderGroupLooper.GetNextObject();
            OrderGroupEnum eOrderGroup = _orderGroupLooper.Current; // отображать текущий объект!!

            switch (eOrderGroup)
            {
                case OrderGroupEnum.ByCreateTime:
                    tbOrderGroup.Text = "По времени";
                    break;

                case OrderGroupEnum.ByOrderNumber:
                    tbOrderGroup.Text = "По заказам";
                    if ((_timerBackToOrderGroupByTime != null) && (_autoBackTimersInterval > 0d)) _timerBackToOrderGroupByTime.Start();
                    break;

                default:
                    break;
            }
        }

        // ****************************
        // **  фильтр по состояниям
        // перебор фильтров состояний по клику на вкладке
        private void tbDishStatusFilter_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _timer.Stop();

            // сдвинуть фильтр
            _orderStatesLooper.SetNextIndex();

            AppLib.WriteLogClientAction("Change Status Filter to " + _orderStatesLooper.Current.Name);

            // обновить пользовательский фильтр текущим набором
            setOrderStatusFilterTab();

            _preOrdersId.Clear();

            _timer.Start();
        }

        #endregion

        #region inner classes

        // класс для циклического получения значений из списка
        private class ListLooper<T>
        {
            private List<T> _list;
            private int _currentIndex;

            public List<T> InnerList { get { return _list; } }
            public int CurrentIndex
            {
                get { return _currentIndex; }
                set { _currentIndex = value; }
            }

            public T Current
            {
                get { return _list[_currentIndex]; }
                set
                {
                    if (_list.Contains(value)) _currentIndex = _list.IndexOf(value);
                }
            }

            // CONSTRUCTOR
            //  объект инициализируется коллекцией для перебора
            //  если ничего не передано или кол-во меньше 2, то объект не создаем
            public ListLooper(IEnumerable<T> collection)
            {
                if ((collection == null) || (collection.Count() < 2)) return;

                _list = new List<T>(collection);
                _currentIndex = 0;
            }

            // сдвинуть текущий индекс для получения следующего значения
            public void SetNextIndex()
            {
                _currentIndex++;
                if (_currentIndex == _list.Count) _currentIndex = 0;

            }

            // получить следующий объект БЕЗ смещения текущего индекса
            // нужен для отображения следующего элемента в UI
            public T GetNextObject()
            {
                int i = _currentIndex + 1; if (i == _list.Count) i = 0;
                return _list[i];
            }

        }

        private class DateNumberOrder
        {
            public DateTime DishDate { get; set; }
            public int Number { get; set; }
            public OrderModel Order { get; set; }
        }

        #endregion

        #region Event Handlers

        private void _adminTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _adminTimer.Stop();
            _adminBitMask = 0;
        }

        // админ жест
        private void brdAdmin_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Point p = e.GetPosition(brdAdmin);
            //Debug.Print("-- down " + p.ToString());

            if ((p.X <= brdAdmin.ActualWidth) && (p.Y <= 30d))  // верхний левый угол
            {
                _adminBitMask = 0;
                _adminBitMask = _adminBitMask.SetBit(0);
                _adminTimer.Start();
            }
            else if ((p.X <= brdAdmin.ActualWidth) && (p.Y >= (brdAdmin.ActualHeight - 30d))) // нижний левый угол
                _adminBitMask = _adminBitMask.SetBit(2);
            else
                _adminBitMask = 0;

            //Debug.Print("_adminMask = {0}", _adminBitMask.ToString());
        }

        private void brdAdmin_PreviewMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Point p = e.GetPosition(brdAdmin);
//            int iSec = (DateTime.Now - _adminDate).Seconds;
            //Debug.Print("-- up {0}", p.ToString());

            if ((p.X <= brdAdmin.ActualWidth) && (p.Y > 30d) && (p.Y <= 0.25d *_screenHeight))
                _adminBitMask = _adminBitMask.SetBit(1); // верхний левый со смещением вниз
            else if ((p.X <= brdAdmin.ActualWidth) && (p.Y >= (0.75d * brdAdmin.ActualHeight)) && (p.Y <= (brdAdmin.ActualHeight - 30d)))  // нижний левый со смещением вверх
            {
                _adminBitMask = _adminBitMask.SetBit(3);
                if (_adminBitMask == 15) openConfigPanel();
            }
            else
                _adminBitMask = 0;
//            Debug.Print("_adminMask = {0}", _adminBitMask.ToString());
        }

        private void btnColorsLegend_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _adminDate = DateTime.Now;
            e.Handled = true;
        }

        private void Window_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (AppLib.IsOpenWindow("ColorLegend"))
            {
                ColorLegend colorLegendWin = (ColorLegend)AppPropsHelper.GetAppGlobalValue("ColorLegendWindow");
                if (colorLegendWin != null) colorLegendWin.Hide();
            }
        }

        private void btnColorsLegend_PreviewMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            double admSecs = (DateTime.Now - _adminDate).TotalSeconds;
            if ((admSecs > 3d) && (admSecs < 10d))
            {
                openConfigPanel();
            }
            // открыть/закрыть легенду цветов таймеров
            else
            {
                App.OpenColorLegendWindow();
            }

            _adminDate = DateTime.MaxValue;
            e.Handled = true;
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            openConfigPanel();
        }

        #endregion

    }  // class MainWindow
}
