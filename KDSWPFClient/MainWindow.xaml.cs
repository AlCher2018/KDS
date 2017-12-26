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
using System.Windows.Media;

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

        private bool _isInit = true;
        private string _clientName;
        private double _screenWidth, _screenHeight;

        private Timer _timer;
        private bool _mayGetData;
        private Timer _timerBackToOrderGroupByTime;  //  таймер возврата группировки заказов по времени
        private Timer _timerBackToFirstPage;        // таймер возврата на первую страницу
        private double _autoBackTimersInterval;     // интервал для таймеров возврата
        private bool _leafing;                      // признак процесса листания

        private AppDataProvider _dataProvider;
        private DishesFilter _dishesFilter = DishesFilter.Instance;

        // классы для циклического перебора клиентских условий отображения блюд
        private ListLooper<OrderGroupEnum> _orderGroupLooper;
        // набор фильтров состояний/вкладок (имя, кисти фона и текста, список состояний)
        private ListLooper<KDSUserStatesSet> _orderStatesLooper;
        // и соответствующие им вкладки
        AppLeftTabControl _tabOrderGroup;
        private bool _isMultipleStatusTabs;

        // клиентский фильтр, передаваемый службе для получения ограниченного объема информации
        ClientDataFilter _clientFilter;
        // буфер для ответа службы
        ServiceResponce _svcResp;

        // страницы заказов
        private OrdersPages _pages;
        private OrderPageHelper _pageHelper;
        private bool _viewPrevPageButton, _viewNextPageButton;
        // флаг режима формирования панелей заказов на канве, если true - то с помощью _pageHelper, иначе - _pages
        private bool _viewByPage = true;

        // служебные коллекции
        private List<OrderModel> _svcOrders;
        private List<OrderViewModel> _viewOrders;  // для отображения на экране
        // признак принудительной отрисовки с первого элемента коллекции заказов
        private bool _forceFromFirstOrder = false;
        // разбивать заказ в последней колонке при движении вперед (или по месту) 
        private bool _keepSplitOrderOnLastColumnByForward = false;

        // временные списки для удаления неразрешенных блюд/заказов, т.к. от службы получаем ВСЕ блюда и ВСЕ заказы в нетерминальных состояниях
        private List<OrderModel> _delOrderIds;  // для удаления заказов
        private List<OrderViewModel> _delOrderViewIds;  // для удаления заказов
        private List<int> _delDishIds;  // для удаления блюд
        private List<string> _dishUIDs;  // UID-ы блюд для поиска "висячих" ингредиентов

        // переменные для опеределения условий отображения окна настройки
        private int _adminBitMask;
        private Timer _adminTimer;
        private DateTime _adminDate;

        // звуки
        private System.Media.SoundPlayer _wavPlayer;
        private bool _isNeedSound = true;

        // делегаты на методы, вызываемые из таймеров
        Action<LeafDirectionEnum> _getOrdersFromServiceDelegate;
        Action _setFirstPageDelegate, _setOrderGroupByTimeDelegate;

        public bool ClickPageButton { get; set; }

        private AppLeftTabControl _tabDishGroup;


        // CONSTRUCTOR
        public MainWindow(string[] args)
        {
            InitializeComponent();

            this.Loaded += MainWindow_Loaded;

            _clientName = Environment.MachineName + "." + App.ClientName;
            _screenWidth = (double)WpfHelper.GetAppGlobalValue("screenWidth");
            _screenHeight = (double)WpfHelper.GetAppGlobalValue("screenHeight");

            this.Top = 0; this.Left = 0;
            this.Width = _screenWidth; this.Height = _screenHeight;
            
            // админ-кнопка для открытия окна конфигурации
            btnCFG.Visibility = (CfgFileHelper.GetAppSetting("IsShowCFGButton").ToBool() || args.Contains("-adm")) ? Visibility.Visible : Visibility.Hidden;

            _dataProvider = (AppDataProvider)WpfHelper.GetAppGlobalValue("AppDataProvider");
            setWindowsTitle();

            // таймер автоматического перехода группировки заказов из "По номерам" в "По времени"
            _timerBackToOrderGroupByTime = new Timer() { AutoReset = false };
            _setOrderGroupByTimeDelegate = new Action(setOrdersGroupByTime);
            _timerBackToOrderGroupByTime.Elapsed += 
                (object sender, ElapsedEventArgs e) => this.Dispatcher.Invoke(_setOrderGroupByTimeDelegate);
            // таймер возврата на первую страницу
            _timerBackToFirstPage = new Timer() { AutoReset = false };
            _setFirstPageDelegate = new Action(setFirstPage);
            _timerBackToFirstPage.Elapsed += 
                (object sender, ElapsedEventArgs e) => this.Dispatcher.Invoke(_setFirstPageDelegate);
            setBackTimersInterval();

            // основной таймер опроса сервиса
            _getOrdersFromServiceDelegate = new Action<LeafDirectionEnum>(getOrdersFromService);
            _timer = new Timer(1000) { AutoReset = false };
            _timer.Elapsed += _timer_Elapsed;
            _timer.Start();

            _clientFilter = new ClientDataFilter();
            _clientFilter.DepIDsList = getClientDepsList();

            // класс для циклического перебора группировки заказов
            // в коде используется ТЕКУЩИЙ объект, но на вкладках отображается СЛЕДУЮЩИЙ !!!
            _orderGroupLooper = new ListLooper<OrderGroupEnum>(new[] { OrderGroupEnum.ByCreateTime, OrderGroupEnum.ByOrderNumber });

            // отступы панели заказов (ViewBox) внутри родительской панели
            double verMargin = Convert.ToDouble(WpfHelper.GetAppGlobalValue("OrdersPanelTopBotMargin"));
            this.vbxOrders.Margin = new Thickness(0, verMargin, 0, verMargin);
            this.bufferOrderPanels.Margin = new Thickness(0, verMargin, 0, verMargin);

            _viewOrders = new List<OrderViewModel>();

            // кнопки переключения страниц
            btnSetPagePrevious.Height = btnSetPagePrevious.Width = btnSetPageNext.Width = btnSetPageNext.Height = Convert.ToDouble(WpfHelper.GetAppGlobalValue("OrdersPanelScrollButtonSize"));

            // временные коллекции
            _delOrderIds = new List<OrderModel>();
            _delOrderViewIds = new List<OrderViewModel>();
            _delDishIds = new List<int>();
            _dishUIDs = new List<string>();

            _adminTimer = new Timer() { Interval = 3000d, AutoReset = false };
            _adminTimer.Elapsed += _adminTimer_Elapsed;

            // звук предупреждения о появлении нового заказа
            _wavPlayer = new System.Media.SoundPlayer();
            var wavFile = WpfHelper.GetAppGlobalValue("NewOrderAudioAttention");
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
            if (_viewByPage)
            {
                // постраничная отрисовка
                _pageHelper = new OrderPageHelper(bufferOrderPanels);
                _pageHelper.ResetOrderPanelSize();
                _pageHelper.ResetMaxDishesCountOnPage();
            }

            // кнопка переключения группировки заказов
            double dTabHeight = WpfHelper.GetRowHeightAbsValue(grdUserConfig, 1);
            _tabOrderGroup = new AppLeftTabControl(grdUserConfig.ActualWidth, dTabHeight, "По времени", 0d);
            _tabOrderGroup.IsEnabled = true;
            _tabOrderGroup.IsForceCallSetHeight = true;
            _tabOrderGroup.PreviewMouseDown += tbOrderGroup_MouseDown;
            _tabOrderGroup.SetValue(Grid.RowProperty, 1);
            grdUserConfig.Children.Add(_tabOrderGroup);
            setOrderGroupTab(false);

            // контрол группировки блюд
            _isMultipleStatusTabs = (bool)WpfHelper.GetAppGlobalValue("IsMultipleStatusTabs");
            bool isVisibleDishGroupControl = (bool)WpfHelper.GetAppGlobalValue("IsDishGroupAndSumQuantity", false);
            //isVisibleDishGroupControl  = Environment.MachineName.Equals("prg01", StringComparison.OrdinalIgnoreCase);
            //cbxDishesGroup.Visibility = (isVisibleDishGroupControl ) ? Visibility.Visible : Visibility.Hidden;
            dTabHeight = WpfHelper.GetRowHeightAbsValue(grdUserConfig, 5);
            _tabDishGroup = new AppLeftTabControl(grdUserConfig.ActualWidth, dTabHeight, null, 0d);
            _tabDishGroup.IsEnabled = true;
            _tabDishGroup.IsForceCallSetHeight = true;
            _tabDishGroup.PreviewMouseDown += _tabDishGroup_PreviewMouseDown;
            _tabDishGroup.SetValue(Grid.RowProperty, 5);
            grdUserConfig.Children.Add(_tabDishGroup);
            _tabDishGroup.Tag = "ungroup";  // текущий режим группировки блюд
            setDishGroupTabProperties();
            if (isVisibleDishGroupControl)
            {
                _tabDishGroup.Visibility = Visibility.Visible;
            }
            else
            {
                _tabDishGroup.Visibility = Visibility.Hidden;
                if (_isMultipleStatusTabs) pnlLeftTabs.SetValue(Grid.RowSpanProperty, 3);
            }

            setStatusTabs(_isMultipleStatusTabs);

            SplashScreen.Splasher.CloseSplash();
            _isInit = false;
        }


        protected override void OnClosing(CancelEventArgs e)
        {
            if ((_timer != null) && _timer.Enabled)
            {
                _timer.Stop(); _timer.Dispose();
            }
            WpfHelper.CloseChildWindows();
            base.OnClosing(e);
        }

        // основной таймер отображения панелей заказов
        private void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            //DateTime dt = DateTime.Now;
            //if (dt.Millisecond <= 200)
            //{
            // обновление по месту
            this.Dispatcher.Invoke(_getOrdersFromServiceDelegate, LeafDirectionEnum.NoLeaf);
            //getOrdersFromService(LeafDirectionEnum.NoLeaf);
            //}
        }


        // получение данных от службы, учитывая условия, задаваемые конкретным клиентом
        private void getOrdersFromService(LeafDirectionEnum leafDirection)
        {
            if (_timer.Enabled) _timer.Stop();

            _mayGetData = false;
            try
            {
                // потеря связи со службой
                if (_dataProvider.EnableGetChannel == false)
                {
                    AppLib.WriteLogTraceMessage("потеря связи со службой получения данных: пересоздаю get-канал...");
                    _mayGetData = _dataProvider.CreateGetChannel();
                }
                else if (_dataProvider.IsGetServiceData == false)
                {
                    AppLib.WriteLogTraceMessage("от службы не получены справочные данные: повторный запрос к службе...");
                    _mayGetData = _dataProvider.SetDictDataFromService();
                }
                else
                    _mayGetData = true;
            }
            catch (Exception ex)
            {
                AppLib.WriteLogErrorMessage("** Ошибка обновления заказов: {0}", ErrorHelper.GetShortErrMessage(ex));
            }

            if (_mayGetData)
            {
                if (tblChannelErrorMessage.Visibility == Visibility.Visible)
                    tblChannelErrorMessage.Visibility = Visibility.Hidden;

                // запросить у службы отделы
                if ((_clientFilter.DepIDsList == null) || (_clientFilter.DepIDsList.Count == 0))
                {
                    AppLib.WriteLogInfoMessage("У клиента нет отображаемых цехов!");
                    if (_dataProvider.Departments.Count == 0)
                    {
                        AppLib.WriteLogInfoMessage(" - от службы не получен список цехов: повторный запрос к службе...");
                        _dataProvider.SetDictDataFromService();
                    }
                    _clientFilter.DepIDsList = getClientDepsList();
                }

                // запрос данных от службы
                // в службу передаются: статусы и отделы, отображаемые на клиенте (фильтр данных); способ группировки заказов;
                // информация для ограничения объема возвращаемых данных: конечные Id заказа и блюда, направление листания (leaf) и приблизительное количество элементов.
                _clientFilter.LeafDirection = leafDirection;
                if (_clientFilter.ApproxMaxDishesCountOnPage != OrderPageHelper.MaxDishesCountOnPage)
                    _clientFilter.ApproxMaxDishesCountOnPage = OrderPageHelper.MaxDishesCountOnPage;

                if (_forceFromFirstOrder)
                {
                    _clientFilter.EndpointOrderID = 0;
                    _clientFilter.EndpointOrderItemID = 0;
                }
                else
                {
                    bool fromFirstPanel = ((leafDirection == LeafDirectionEnum.NoLeaf)
                        || (leafDirection == LeafDirectionEnum.Backward));
                    int orderStartId, dishStartId;
                    getModelIdFromViewContainer(fromFirstPanel, out orderStartId, out dishStartId);
                    _clientFilter.EndpointOrderID = orderStartId;
                    _clientFilter.EndpointOrderItemID = dishStartId;
                }
                AppLib.WriteLogOrderDetails("svc.GetOrders('{0}', '{1}') - START", _clientName, clientDataFilterToString(_clientFilter));

                try
                {

                    _svcResp = _dataProvider.GetOrders(_clientFilter);
                    
                    // клиент не смог получить заказы, т.к. служба еще читала данные из БД - 
                    // уменьшить интервал таймера до 100 мсек
                    if ((_svcResp.OrdersList.Count == 0) && (!_svcResp.ServiceErrorMessage.IsNull()))
                    {
                        string sErr = " - от службы получено 0 заказов: ";
                        // служба еще читает данные
                        if (_svcResp.ServiceErrorMessage.StartsWith("KDS service is reading data"))
                        {
                            if (_timer.Interval != 90)
                            {
                                _timer.Interval = 90;
                                AppLib.WriteLogOrderDetails(" - set timer.Interval = 0,1 sec");
                            }
                            AppLib.WriteLogOrderDetails(sErr + "служба читает данные из БД...");
                        }
                        // сообщение от службы об ошибке чтения данных
                        else
                        {
                            AppLib.WriteLogErrorMessage(sErr + "ошибка службы: {0}", _svcResp.ServiceErrorMessage);
                        }
                    }
                    else
                    {
                        _svcOrders = _svcResp.OrdersList;
                        AppLib.WriteLogOrderDetails(" - от службы получено заказов: {0}, {1}", _svcOrders.Count, _logOrderInfo(_svcOrders));
                        // вернуться на стандартный интервал в 1 сек
                        if (_timer.Interval != 1000)
                        {
                            _timer.Interval = 1000;
                            AppLib.WriteLogTraceMessage(" - set timer.Interval = 1 sec");
                        }

                        // обновить данные во внутренней коллекции и обновить экран/панели
                        updateOrders(leafDirection);

                        // при листании назад:
                        if (leafDirection == LeafDirectionEnum.Backward)
                        {
                            // взять из первого контейнера Ид заказа/блюда
                            int orderStartId, dishStartId;
                            getModelIdFromViewContainer(true, out orderStartId, out dishStartId);
                            _clientFilter.EndpointOrderID = orderStartId;
                            _clientFilter.EndpointOrderItemID = dishStartId;
                            // эмулировать чтение данных по таймеру
                            leafDirection = LeafDirectionEnum.NoLeaf;
                            _clientFilter.LeafDirection = leafDirection;
                            _svcResp = _dataProvider.GetOrders(_clientFilter);
                            while (_svcResp == null)
                            {
                                System.Threading.Thread.Sleep(50);
                                _svcResp = _dataProvider.GetOrders(_clientFilter);
                            }
                            _svcOrders = _svcResp.OrdersList;
                            // обновить внутреннюю коллекцию
                            updateOrders(leafDirection);
                        }

                        // поднять флаг проигрывания мелодии при появлении новых заказов
                        if (!_isNeedSound)
                        {
                            _isNeedSound = true;
                            AppLib.WriteLogTraceMessage("-- _isNeedSound = {0}", _isNeedSound.ToString());
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppLib.WriteLogErrorMessage("Ошибка получения данных от КДС-службы: {0}", ex.ToString()); // ErrorHelper.GetShortErrMessage(ex)
                    _dataProvider.IsGetServiceData = false;
                }

            }  // if (_mayGetData)

            // очистить канву от заказов и отобразить сообщение об ошибке связи
            else
            {
                if (tblChannelErrorMessage.Visibility != Visibility.Visible) tblChannelErrorMessage.Visibility = Visibility.Visible;
                _pages.ClearPages();
                if (_viewOrders.Count > 0) _viewOrders.Clear();
                AppLib.WriteLogOrderDetails("can't get orders due to service status Falted");
            }

            _timer.Start();
        }

        private string clientDataFilterToString(ClientDataFilter source)
        {
            string s1 = (source.StatusesList == null) ? "" : string.Join(",", source.StatusesList);
            string s2 = (source.DepIDsList == null) ? "" : string.Join(",", source.DepIDsList);

            string retVal = string.Format("StatusesList={0}; DepIDsList={1}; GroupBy={2}; EndpointOrderID={3}; EndpointOrderItemID={4}; LeafDirection={5}", s1, s2, source.GroupBy.ToString(), source.EndpointOrderID, source.EndpointOrderItemID, source.LeafDirection.ToString());

            return retVal;
        }


        // обновление данных во внутренней коллекции и на экране
        private void updateOrders(LeafDirectionEnum leafDirection)
        {
            DateTime dtTmr1 = DateTime.Now;
            string repaintReason=null;

            // *** ОБНОВИТЬ _viewOrdes (для отображения на экране) ДАННЫМИ ИЗ svcOrders (получено из БД)
            // обновить внутреннюю коллекцию заказов данными, полученными от сервиса
            // в случае с группировкой по времени и разбивкой заказов на несколько панелей AppLib.JoinSortedLists() работает НЕПРАВИЛЬНО!!!
            //bool isViewRepaint = AppLib.JoinSortedLists<OrderViewModel, OrderModel>(_viewOrders, svcOrders);
            // поэтому сделано уникальной процедурой
            AppLib.WriteLogOrderDetails("   обновление служебной коллекции заказов (для отображения на экране)...");
            bool isViewRepaint2 = false;
            try
            {
                isViewRepaint2 = updateViewOrdersList();
                if (isViewRepaint2) repaintReason = "update Orders from KDS service";
            }
            catch (Exception ex)
            {
                AppLib.WriteLogErrorMessage("Ошибка обновления служебной коллекции заказов для отображения на экране: {0}", ex.ToString());
                return;
            }

            // 2017-07-24 по заявке Ридченко
            // удалить заказы, у которых StatusAllowedDishes не входит в отображаемые состояния
            _delOrderViewIds.Clear();
            StatusEnum allDishesStatus = StatusEnum.None;
            foreach (OrderViewModel item in _viewOrders)
            {
                allDishesStatus = AppLib.GetStatusAllDishesOwnDeps(item.Dishes);
                if ((item.StatusAllowedDishes != allDishesStatus) 
                    && (allDishesStatus != StatusEnum.WaitingCook))
                {
                    item.StatusAllowedDishes = allDishesStatus;
                    isViewRepaint2 = true;
                    AppLib.WriteLogTraceMessage("статус заказа (id {0}) для всех разрешенных блюд ({1}) изменен на статус всех блюд {2}", item.Id, item.StatusAllowedDishes.ToString(), allDishesStatus.ToString());
                }

                if ((item.StatusAllowedDishes != StatusEnum.None) 
                    && (_dishesFilter.IsStatusAllowed((int)item.StatusAllowedDishes) == false))
                {
                    _delOrderViewIds.Add(item);
                    isViewRepaint2 = true;
                    if ((repaintReason != null) && !repaintReason.Equals("delete Orders with not allowed statuses")) repaintReason = "delete Orders with not allowed statuses";
                }
            }
            _delOrderViewIds.ForEach(o => _viewOrders.Remove(o));

            // условия перерисовки
            // перерисовать, если на экране было пусто, а во _viewOrders появились заказы
            if (!isViewRepaint2 && !_viewByPage)
                isViewRepaint2 = ((_pages.CurrentPage.Children.Count == 0) && (_viewOrders.Count != 0));

            // от службы получены новые заказы
            if (_svcResp.NewOrderIds.IsNull() == false)
            {
                List<OrderViewModel> newOrdersList = new List<OrderViewModel>();
                int[] newOrderIdsArray = _svcResp.NewOrderIds.Split(new char[] { ';' }).Select(sId => sId.ToInt()).ToArray();
                foreach (int item in newOrderIdsArray)
                {
                    newOrdersList.AddRange(_viewOrders.Where(ord => ord.Id == item));
                }
                AppLib.WriteLogTraceMessage(" - новые заказы: {0}", _logOrderInfo(newOrdersList));
                newOrdersList.Clear(); newOrdersList = null;

                // не проигрывать мелодию, если только изменились условия выборки/группировки заказов
                // - в элементах управления: кнопки листания страниц, группировка заказов, группировка блюд, фильтр статусов, 
                // - или в окне настроек: отображаемые цеха, отображаемые статусы, 
                AppLib.WriteLogTraceMessage("** before sound play: _isNeedSound={0}, _isInit={1}",_isNeedSound.ToString(), _isInit.ToString());
                if (_isNeedSound || _isInit)
                {
                    _wavPlayer.Play();
                    // принудительно перерисовать с первого полученного заказа
                    _forceFromFirstOrder = true;
                }

                // перерисовать панели заказов на экране
                if (!isViewRepaint2) isViewRepaint2 = true;
            }

            if (!isViewRepaint2 && (leafDirection != LeafDirectionEnum.NoLeaf))
            {
                isViewRepaint2 = true; repaintReason = string.Format("move to {0} page", (leafDirection== LeafDirectionEnum.Backward ? "PREV" : "NEXT"));
            }

            //if (_svcResp.IsExistsNewOrder &&
            //    (((btnSetPageNext.Visibility == Visibility.Visible).Equals(_svcResp.isExistsNextOrders) == false)
            //    || ((btnSetPagePrevious.Visibility == Visibility.Visible).Equals(_svcResp.isExistsPrevOrders) == false))
            //    ) isViewRepaint2 = true;

            AppLib.WriteLogOrderDetails("   для отображения на экране заказов: {0}; {1} - {2}", _viewOrders.Count, _logOrderInfo(_viewOrders), (isViewRepaint2 ? "ПЕРЕРИСОВКА всех заказов" : "только счетчики"));

            // перерисовать полностью
            if (isViewRepaint2)
            {
                if (repaintReason.IsNull()) repaintReason = "update Orders from KDS service";
                repaintOrders(repaintReason, leafDirection);
            }

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
        private bool updateViewOrdersList()
        {
            bool isViewRepaint = false;
            OrderViewModel curViewOrder;
            int index = -1;  // порядковый номер
            if (_viewOrders.Count > _svcOrders.Count)
            {
                int delIndexFrom = (_svcOrders.Count == 0) ? 0 : _svcOrders.Count - 1;
                _viewOrders.RemoveRange(delIndexFrom, _viewOrders.Count - _svcOrders.Count);
                isViewRepaint = true;
            }
            foreach (OrderModel om in _svcOrders)
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
                    if ((curViewOrder.Id == om.Id) && (curViewOrder.CreateDate == om.CreateDate)
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


        // перерисовать заказы
        private void repaintOrders(string reason, LeafDirectionEnum leafDirection)
        {
            if (_pages == null) return;

            DateTime dtTmr = DateTime.Now;
            string sLogMsg = string.Format(" - redraw reason: {0}", reason);
            AppLib.WriteLogOrderDetails(sLogMsg + " - START");
            Cursor = System.Windows.Input.Cursors.Wait;

            if (_viewByPage)
            {
                repaintOrdersNew(leafDirection);
            }
            else
            {
                _pages.AddOrdersPanels(_viewOrders);
            }

            setCurrentPage();
            Cursor = null; //System.Windows.Input.Cursors.Arrow;
            AppLib.WriteLogOrderDetails(sLogMsg + " - FINISH - " + (DateTime.Now - dtTmr).ToString());
        }

        // размеры элементов панели заказа рассчитываются из размеров vbxOrders (Viewbox)
        private void recalcOrderPanelsLayot()
        {
            //   кол-во столбцов заказов
            int cntCols = Convert.ToInt32(WpfHelper.GetAppGlobalValue("OrdersColumnsCount"));

            //   ширина столбцов заказов и расстояния между столбцами
            double pnlWidth = vbxOrders.ActualWidth;
            // wScr = wCol*cntCols + koef*wCol*(cntCols+1) ==> wCol = wScr / (cntCols + koef*(cntCols+1))
            // где, koef = доля поля от ширины колонки
            double koef = Convert.ToDouble(WpfHelper.GetAppGlobalValue("OrderPanelLeftMargin"));
            double colWidth = Math.Floor(pnlWidth / (cntCols + koef * (cntCols + 1)));
            double colMargin = Math.Floor(koef * colWidth);  // поле между заказами по горизонтали
            WpfHelper.SetAppGlobalValue("OrdersColumnWidth", colWidth);
            WpfHelper.SetAppGlobalValue("OrdersColumnMargin", colMargin);
        }

        private void repaintOrdersNew(LeafDirectionEnum shiftDirection)
        {
            #region найти след/предыд индексы заказ/блюдо, с которых начинается создание панелей
            DateTime dtTmr = DateTime.Now;
            // найти след/предыд индексы заказ/блюдо, с которых начинается создание панелей
            int orderStartIndex =-1, dishStartIndex=-1;
            int orderFinishIndex=-1, dishFinishIndex=-1;
            bool bShiftDirForward = true;

            // перерисовка с первого заказа/блюда: еще нет панелей на канве или соотв.флаг
            if ((_pages.CurrentPage.Children.Count == 0) || _forceFromFirstOrder)
            {
                orderStartIndex = 0; dishStartIndex = 0;
                shiftDirection = LeafDirectionEnum.NoLeaf;
                if (_forceFromFirstOrder) _forceFromFirstOrder = false;
            }
            // найти первый/последний элемент
            else
            {
                switch (shiftDirection)
                {
                    case LeafDirectionEnum.NoLeaf:
                        // с первого элемента на канве и вперед
                        getModelIndexesFromViewContainer(true, out orderStartIndex, out dishStartIndex);
                        bShiftDirForward = true;
                        break;
                    case LeafDirectionEnum.Forward:
                        // с последнего элемента на канве и вперед
                        getModelIndexesFromViewContainer(false, out orderStartIndex, out dishStartIndex);
                        bShiftDirForward = true;
                        break;
                    case LeafDirectionEnum.Backward:
                        // с первого элемента на канве и назад
                        getModelIndexesFromViewContainer(true, out orderStartIndex, out dishStartIndex);
                        bShiftDirForward = false;
                        break;
                    default:
                        break;
                }
            }
            // следующий/предыдущий элемент, с которого будет отрисовываться страница
            //    прямое направление
            if (bShiftDirForward)
            {
                if (orderStartIndex < 0)
                {
                    orderStartIndex = 0; dishStartIndex = 0;
                }
                else if ((_viewOrders.Count > 0) && (dishStartIndex > _viewOrders[orderStartIndex].Dishes.Count - 1))
                {
                    if (orderStartIndex < (_viewOrders.Count-1)) orderStartIndex++;
                    dishStartIndex = 0;
                }
                else if (shiftDirection == LeafDirectionEnum.Forward)
                {
                    dishStartIndex++;
                    //OrderViewModel orderModel = _viewOrders[orderStartIndex];
                    // пропустить "назад" ингредиенты - блюда, у которых не пустой ParentUID
                    //while (!orderModel.Dishes[dishStartIndex].ParentUID.IsNull() && (dishStartIndex > 0)) dishStartIndex--;
                }
            }
            //    обратное направлении
            else
            {
                if ((dishStartIndex <= 0) && (_viewOrders.Count > 0))
                {
                    if (orderStartIndex > 0)
                    {
                        orderStartIndex--;
                        dishStartIndex = _viewOrders[orderStartIndex].Dishes.Count - 1;
                    }
                }
                else
                {
                    dishStartIndex--;
                    // пропустить "вперед" ингредиенты - блюда, у которых не пустой ParentUID
                    //OrderViewModel orderModel = _viewOrders[orderStartIndex];
                    //while (!orderModel.Dishes[dishStartIndex].ParentUID.IsNull() && (dishStartIndex < (orderModel.Dishes.Count - 1))) dishStartIndex++;
                }
            }
            AppLib.WriteLogOrderDetails("   - find Id order/dish draw from, dir {0} - {1}", shiftDirection.ToString(), (DateTime.Now - dtTmr).ToString());
            #endregion

            dtTmr = DateTime.Now;
            _pageHelper.DrawOrderPanelsOnPage(_viewOrders, orderStartIndex, dishStartIndex, bShiftDirForward, _keepSplitOrderOnLastColumnByForward);
            AppLib.WriteLogOrderDetails("  - DrawOrderPanelsOnPage(), dir-{0} - {1}", shiftDirection.ToString(), (DateTime.Now - dtTmr).ToString());

            // если при листании назад, первая панель находится НЕ в первой колонке, 
            // или, наоборот, в первой колонке и свободного места более половины,
            // то разместить заново с первой колонки вперед
            // делать ДО переноса панелей из канвы размещения в канву отображения
            if (!bShiftDirForward && _pageHelper.NeedRelayout())
            {
                dtTmr = DateTime.Now;
                getModelIndexesFromViewContainer(true, out orderStartIndex, out dishStartIndex);
                bShiftDirForward = true;
                _pageHelper.DrawOrderPanelsOnPage(_viewOrders, orderStartIndex, dishStartIndex, bShiftDirForward, _keepSplitOrderOnLastColumnByForward);
                AppLib.WriteLogOrderDetails("  - re_DrawOrderPanelsOnPage() forward after backward, {0}", (DateTime.Now - dtTmr).ToString());
            }

            dtTmr = DateTime.Now;
            movePanelsToView(); // перенос панелей в область просмотра
            AppLib.WriteLogOrderDetails("   - move panels to view canvas - {0}", (DateTime.Now - dtTmr).ToString());

            // при листании вперед, получить индексы последних заказ/блюдо на странице
            if (bShiftDirForward) getModelIndexesFromViewContainer(false, out orderFinishIndex, out dishFinishIndex);
            // при листании назад, получить первые индексы
            else
            {
                orderFinishIndex = orderStartIndex; dishFinishIndex = dishStartIndex;
                getModelIndexesFromViewContainer(true, out orderStartIndex, out dishStartIndex);
            }

            // кнопки листания
            _viewPrevPageButton = ((orderStartIndex > 0) || (dishStartIndex > 0));
            if ((orderFinishIndex == -1) || (_viewOrders.Count == 0))
                _viewNextPageButton = false;
            else if (orderFinishIndex < _viewOrders.Count-1)
                _viewNextPageButton = true;
            else
            {
                OrderViewModel lastOrder = _viewOrders.LastOrDefault();
                if (lastOrder != null) _viewNextPageButton = (dishFinishIndex < (lastOrder.Dishes.Count - 1));
            }
            // из объекта, возвращаемого службой
            if (!_viewPrevPageButton) _viewPrevPageButton = _svcResp.isExistsPrevOrders;
            if (!_viewNextPageButton) _viewNextPageButton = _svcResp.isExistsNextOrders;
        }

        // берем первую или последнюю панель на странице, первое или последнее блюдо, в зависимости от флажка fromFirstItem
        // и находим их индексы в наборе _viewOrders
        private void getModelIndexesFromViewContainer(bool fromFirstItem, out int orderIndex, out int dishIndex)
        {
            orderIndex = -1; dishIndex = -1;

            // источник панелей - или bufferOrderPanels, или vbxOrders.Child
            // предпочтение bufferOrderPanels, если там не пусто
            UIElementCollection pnlSource = null;
            try
            {
                pnlSource = ((this.bufferOrderPanels.Children.Count == 0) && (vbxOrders.Child is Canvas))
                    ? ((Canvas)vbxOrders.Child).Children
                    : this.bufferOrderPanels.Children;
            }
            catch (Exception)
            {
                throw;
            }
            if (pnlSource == null) return;
            else if (pnlSource.Count == 0) return;

            // индекс панели заказа
            int i = (fromFirstItem) ? 0 : pnlSource.Count - 1;
            OrderPanel edgeOrderPanel = (OrderPanel)pnlSource[i];
            DishPanel edgeDishPanel = findEdgeDishPanel(edgeOrderPanel, fromFirstItem);
            if (edgeDishPanel == null) return;
            
            // заказ (OrderViewModel) из панели
            OrderViewModel fromPanelOrderModel = edgeOrderPanel.OrderViewModel;
            // блюдо (OrderDishViewModel) из панели
            OrderDishViewModel fromPanelDishModel = edgeDishPanel.DishView;

            // найти заказ в _viewOrders с таким же id заказа и id блюда, как в панели экрана
            foreach (OrderViewModel item in _viewOrders.FindAll(vOrd => vOrd.Id == fromPanelOrderModel.Id))
            {
                dishIndex = item.Dishes.FindIndex(d => d.Id == fromPanelDishModel.Id);
                if (dishIndex != -1)
                {
                    orderIndex = _viewOrders.IndexOf(item);
                    break;
                }
            }
        }

        // id или первого, или последнего элемента заказ/блюдо, в зависимости от флажка fromFirstItem
        private void getModelIdFromViewContainer(bool fromFirstItem, out int orderId, out int dishId)
        {
            orderId = -1; dishId = -1;

            // источник панелей - или bufferOrderPanels, или vbxOrders.Child
            // предпочтение bufferOrderPanels, если там не пусто
            UIElementCollection pnlSource = null;
            try
            {
                pnlSource = ((this.bufferOrderPanels.Children.Count == 0) && (vbxOrders.Child is Canvas))
                    ? ((Canvas)vbxOrders.Child).Children
                    : this.bufferOrderPanels.Children;
            }
            catch (Exception)
            {
                throw;
            }
            if ((pnlSource != null) && (pnlSource.Count == 0)) return;

            int i = (fromFirstItem) ? 0 : pnlSource.Count - 1;
            // граничная панель заказа
            OrderPanel edgeOrderPanel = (OrderPanel)pnlSource[i];
            OrderViewModel curPanelOrderModel = edgeOrderPanel.OrderViewModel;  // OrderViewModel из панели
            orderId = curPanelOrderModel.Id;

            // граничная панель блюда
            DishPanel edgeDishPanel = findEdgeDishPanel(edgeOrderPanel, fromFirstItem);
            OrderDishViewModel curPanelDishModel = edgeDishPanel.DishView;  // OrderDishViewModel из панели
            dishId = curPanelDishModel.Id;
        }


        // поиск граничной панели блюда в панели заказа, с пропуском разделителей
        private DishPanel findEdgeDishPanel(OrderPanel edgeOrderPanel, bool fromFirstItem)
        {
            UIElement item = null;
            for (int i = (fromFirstItem) ? 0 : edgeOrderPanel.DishPanels.Count - 1;
                (fromFirstItem) ? i < edgeOrderPanel.DishPanels.Count : i >= 0;
                i += (fromFirstItem) ? 1 : -1)
            {
                item = edgeOrderPanel.DishPanels[i];
                if (item is DishPanel) return (item as DishPanel);
            }
            return null;
        }

        private int getDishIndex(bool fromFirstItem, OrderDishViewModel curPanelDishModel, List<OrderViewModel> tmpOrderModels, ref OrderViewModel foundDishOrder)
        {
            int dishIndex = -1;

            if (tmpOrderModels != null)
            {
                foreach (OrderViewModel om in tmpOrderModels)
                {
                    // поиск индекса блюда findingDishModel в om
                    dishIndex = om.Dishes.FindIndex(d => d.Id == curPanelDishModel.Id);
                    if (dishIndex != -1)
                    {
                        foundDishOrder = om;
                        return dishIndex;
                    }
                }
            }

            return dishIndex;
        }


        #region change page
        // *** кнопки листания страниц ***
        private void btnSetPagePrevious_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (_leafing == true) return;

            _leafing = true;
            if (_viewByPage)
            {
                //_keepSplitOrderOnLastColumnByForward = true;
                _isNeedSound = false;
                getOrdersFromService(LeafDirectionEnum.Backward);
                setCurrentPage();
            }
            if (_pages.SetPreviousPage())
                setCurrentPage();
            _leafing = false;

            this.ClickPageButton = true;
        }

        private void btnSetPageNext_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (_leafing == true) return;

            _leafing = true;
            if (_viewByPage)
            {
                _keepSplitOrderOnLastColumnByForward = false;
                _isNeedSound = false;
                getOrdersFromService(LeafDirectionEnum.Forward);
                setCurrentPage();
            }
            else if (_pages.SetNextPage())
                setCurrentPage();
            _leafing = false;
        }

        // отображение кнопок с номерами страниц и вкл/выкл таймера возврата на первую страницу
        private void setCurrentPage()
        {
            btnSetPagePrevious.Visibility = Visibility.Hidden;
            btnSetPageNext.Visibility = Visibility.Hidden;
            if (_pages.Count == 0) return;

            // постраничные кнопки, если _pages содержит несколько страниц
            if (!_viewByPage && (_pages.Count > 1))
            {
                // кнопка перехода на предыдущюю страницу
                if ((_pages.CurrentPageIndex - 1) > 0)
                {
                    tbPagePreviousNum.Text = "Стр. " + (_pages.CurrentPageIndex - 1).ToString();
                    btnSetPagePrevious.Visibility = Visibility.Visible;
                }
                // кнопка перехода на следующую страницу
                if (_pages.CurrentPageIndex < _pages.Count)
                {
                    tbPageNextNum.Text = "Стр. " + (_pages.CurrentPageIndex + 1).ToString();
                    btnSetPageNext.Visibility = Visibility.Visible;
                }
            }
            else if (_viewByPage && (_pageHelper != null))
            {
                // кнопка перехода на предыдущюю страницу
                if (_viewPrevPageButton)
                {
                    tbPagePreviousNum.Text = "предыд. стр";
                    btnSetPagePrevious.Visibility = Visibility.Visible;
                }
                // кнопка перехода на следующую страницу
                if (_viewNextPageButton)
                {
                    tbPageNextNum.Text = "след. стр.";
                    btnSetPageNext.Visibility = Visibility.Visible;
                }
            }

            // перезапуск всех автотаймеров
            // таймер возврата в группировку по времени
            if ((_timerBackToOrderGroupByTime != null) && (_autoBackTimersInterval > 0d))
            {
                if (_timerBackToOrderGroupByTime.Enabled) _timerBackToOrderGroupByTime.Stop();
                if (_orderGroupLooper.Current == OrderGroupEnum.ByOrderNumber) _timerBackToOrderGroupByTime.Start();
            }
            // таймер возврата на первую страницу
            if ((_timerBackToFirstPage != null) && (_autoBackTimersInterval > 0d))
            {
                if (_timerBackToFirstPage.Enabled) _timerBackToFirstPage.Stop();
                if (_viewPrevPageButton) _timerBackToFirstPage.Start();
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

            string sLogMsg = string.Join("; ", cfgEdit.AppNewSettings.Select(s => s.Key + "=" + s.Value));
            AppLib.WriteLogClientAction("   changed: " + sLogMsg);

            bool isRepaintScreen=false, isRequestOrders=false;

            //  ОБНОВЛЕНИЕ ПАРАМЕТРОВ ПРИЛОЖЕНИЯ
            if (cfgEdit.AppNewSettings.Count > 0)
            {
                if (cfgEdit.AppNewSettings.ContainsKey("depUIDs"))
                {
                    _clientFilter.DepIDsList = getClientDepsList();
                    isRequestOrders = true;
                }

                // обновить фильтр блюд
                bool isUpdatedTabs = false;
                if (cfgEdit.AppNewSettings.ContainsKey("IsMultipleStatusTabs"))
                {
                    bool newValue = cfgEdit.AppNewSettings["IsMultipleStatusTabs"].ToBool();
                    setStatusTabs(newValue);
                    isUpdatedTabs = true;
                }
                if (cfgEdit.AppNewSettings.ContainsKey("KDSMode"))
                {
                    setWindowsTitle();
                    if (!isUpdatedTabs) setStatusTabs(_isMultipleStatusTabs);
                }
                else if (cfgEdit.AppNewSettings.ContainsKey("KDSModeSpecialStates") 
                    && (cfgEdit.AppNewSettings["KDSModeSpecialStates"].IsNull() == false))
                {
                    if (KDSModeHelper.CurrentKDSMode != KDSModeEnum.Special) KDSModeHelper.CurrentKDSMode = KDSModeEnum.Special;

                    setWindowsTitle();
                    if (!isUpdatedTabs) setStatusTabs(_isMultipleStatusTabs);
                }

                bool resetMaxDishesCountOnPage = false;
                if (cfgEdit.AppNewSettings.ContainsKey("IsShowOrderStatusByAllShownDishes"))
                {
                    isRepaintScreen = true;
                }

                // масштаб шрифта
                // перерисовать полностью, т.к. по таймеру может все не обновиться
                if (cfgEdit.AppNewSettings.ContainsKey("AppFontScale"))
                {
                    double newAppFontScale = cfgEdit.AppNewSettings["AppFontScale"].ToDouble();
                    WpfHelper.SetAppGlobalValue("AppFontScale", newAppFontScale);
                    if (_viewByPage)
                    {
                        resetMaxDishesCountOnPage = true;
                    }
                    isRepaintScreen = true;
                }

                // кол-во колонок заказов
                if (cfgEdit.AppNewSettings.ContainsKey("OrdersColumnsCount"))
                {
                    recalcOrderPanelsLayot();
                    if (_viewByPage)
                    {
                        _pageHelper.ResetOrderPanelSize();
                        resetMaxDishesCountOnPage = true;
                    }
                    else
                    {
                        _pages.ResetOrderPanelSize();
                    }
                    isRepaintScreen = true;
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
                    WpfHelper.SetAppGlobalValue("NewOrderAudioAttention", wavFile);
                    // в config-файле
                    string errMsg;
                    CfgFileHelper.SaveAppSettings("NewOrderAudioAttention", wavFile,out errMsg);
                    // и загрузить в проигрыватель
                    _wavPlayer.SoundLocation = AppEnvironment.GetAppDirectory("Audio") + wavFile;
                    _wavPlayer.LoadAsync();
                }

                if (cfgEdit.AppNewSettings.ContainsKey("IsDishGroupAndSumQuantity"))
                {
                    bool isDishGroupTabVisible = cfgEdit.AppNewSettings["IsDishGroupAndSumQuantity"].ToBool();
                    dishGroupTabVisibility(isDishGroupTabVisible);
                    setStatusTabs(_isMultipleStatusTabs);
                }


                if (resetMaxDishesCountOnPage) _pageHelper.ResetMaxDishesCountOnPage();

                if (isRequestOrders)
                {
                    _forceFromFirstOrder = true;
                    _isNeedSound = false;
                    getOrdersFromService(LeafDirectionEnum.NoLeaf);
                }
                else if (isRepaintScreen)
                {
                    repaintOrders("changed config parameters", LeafDirectionEnum.NoLeaf);
                }

            }
            cfgEdit = null;

            _timer.Start();
        }

        private List<int> getClientDepsList()
        {
            return (from d in _dataProvider.Departments.Values where d.IsViewOnKDS select d.Id).ToList();
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

        #region группировка заказов
        // *** группировка заказов
        private void tbOrderGroup_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            e.Handled = true;

            Debug.Print("tbOrderGroup_MouseDown");
            // сдвинуть текущий элемент
            _orderGroupLooper.SetNextIndex();

            // и отобразить следующий
            AppLib.WriteLogClientAction("Change Order group to " + _orderGroupLooper.Current.ToString());
            setOrderGroupTab();
        }

        private void setFirstPage()
        {
            if (_viewPrevPageButton)
            {
                _pages.SetFirstPage();
                _forceFromFirstOrder = true;
                _isNeedSound = false;
                getOrdersFromService(LeafDirectionEnum.Forward);
            }
        }

        private void setOrdersGroupByTime()
        {
            _orderGroupLooper.Current = OrderGroupEnum.ByCreateTime;
            setOrderGroupTab(true);
        }

        private void setBackTimersInterval()
        {
            // интервал таймера взять из config-файла
            string cfgStr = CfgFileHelper.GetAppSetting("AutoReturnOrdersGroupByTime");

            // и перевести в мсек
            _autoBackTimersInterval = 1000d * ((cfgStr.IsNull()) ? 0d : cfgStr.ToDouble());
            if (_autoBackTimersInterval > 0d)
            {
                resetTimerInterval(_timerBackToOrderGroupByTime, _autoBackTimersInterval);
                // включить таймер при необходимости
                if ((_orderGroupLooper != null) && (_orderGroupLooper.Current == OrderGroupEnum.ByOrderNumber)) _timerBackToOrderGroupByTime.Start();

                resetTimerInterval(_timerBackToFirstPage, _autoBackTimersInterval);
                // включить таймер, если находимся не на первой странице
                if (btnSetPagePrevious.Visibility == Visibility.Visible) _timerBackToFirstPage.Start();
            }
            else
            {
                _timerBackToOrderGroupByTime.Enabled = false;
                _timerBackToFirstPage.Enabled = false;
            }
        }

        private void resetTimerInterval(Timer timer, double interval)
        {
            if (timer.Enabled)
            {
                timer.Stop();
                timer.Interval = interval;
                timer.Start();
            }
            else
                timer.Interval = interval;
        }

        // отобразить на вкладке СЛЕДУЮЩИЙ элемент!!
        private void setOrderGroupTab(bool resetDataPanels = true)
        {
            //OrderGroupEnum eOrderGroup = _orderGroupLooper.GetNextObject();
            OrderGroupEnum eOrderGroup = _orderGroupLooper.Current; // отображать текущий объект!!
            _clientFilter.GroupBy = eOrderGroup;

            switch (eOrderGroup)
            {
                case OrderGroupEnum.ByCreateTime:
                    _tabOrderGroup.Text = "По времени";
                    // выключить таймер автовозврата в группировку по времени, если он был включен
                    if (_timerBackToOrderGroupByTime.Enabled) _timerBackToOrderGroupByTime.Stop();
                    break;

                case OrderGroupEnum.ByOrderNumber:
                    _tabOrderGroup.Text = "По заказам";
                    // включить таймер автовозврата в группировку по времени
                    if (_autoBackTimersInterval > 0d) _timerBackToOrderGroupByTime.Start();
                    break;

                default:
                    break;
            }

            // получить заказы, начиная с первого, и обновить экран
            if (resetDataPanels)
            {
                _forceFromFirstOrder = true;
                _isNeedSound = false;
                getOrdersFromService(LeafDirectionEnum.Forward);
            }
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

        #region вкладки фильтра статусов
        // одна вкладка на все наборы статусов или по вкладке на каждый набор
        private void setStatusTabs(bool newTabsMode)
        {
            _isMultipleStatusTabs = newTabsMode;
            // сохранить статус текущей вкладки
            string curStatesSetName = null;
            foreach (AppLeftTabControl item in pnlLeftTabs.Children.OfType<AppLeftTabControl>())
            {
                if (item.IsEnabled) { curStatesSetName = item.StatesSet.Name; break; }
            }
            // удалить предыдущие вкладки
            pnlLeftTabs.Children.Clear();
            AppLeftTabControl tab;

            #region высота зоны вкладок
            double[] curRowHeights = grdUserConfig.RowDefinitions.Select(row => row.Height.Value).ToArray();
            double[] newRowHeights;
            if (_isMultipleStatusTabs)
            {
                // увеличить зону вкладок (4-й элемент)
                /*
                    <RowDefinition Height="*"/>
                    <RowDefinition Height="*"/>
                    <RowDefinition Height="0.3*"/>
                    <RowDefinition Height="4*"/>
                    <RowDefinition Height="0.3*"/>
                    <RowDefinition Height="0.6*"/>
                    <RowDefinition Height="0.3*"/>
                 */
                newRowHeights = new double[] { 1d, 1d, 0.3d, 4d, 0.3d, 0.6d, 0.3d };
            }
            else
            {
                // уменьшить зону вкладок
                /*
                    <RowDefinition Height="1.5*"/>
                    <RowDefinition Height="1.2*"/>
                    <RowDefinition Height="0.5*"/>
                    <RowDefinition Height="1.2*"/>
                    <RowDefinition Height="*"/>
                    <RowDefinition Height="0.7*"/>
                    <RowDefinition Height="0.5*"/>
                 */
                newRowHeights = new double[] { 1.5d, 1.2d, 0.5d, 1.2d, 1d, 0.7d, 0.5d };
            }

            // изменилась ли высота хоть какой-нибудь строки
            bool isEqualRowHeights = true;
            for (int i = 0; i < curRowHeights.Length; i++)
            {
                if (curRowHeights[i] != newRowHeights[i]) { isEqualRowHeights = false; break; }
            }
            // если изменилась
            if (!isEqualRowHeights)
            {
                for (int i = 0; i < curRowHeights.Length; i++)
                {
                    if (grdUserConfig.RowDefinitions[i].Height.Value != newRowHeights[i])
                    {
                        grdUserConfig.RowDefinitions[i].Height = new GridLength(newRowHeights[i], GridUnitType.Star);
                    }
                }

                // обновить высоту вкладок, у которых установлен флаг принудительной установки высоты
                foreach (UIElement item in grdUserConfig.Children)
                {
                    if (item is AppLeftTabControl)
                    {
                        tab = (item as AppLeftTabControl);
                        if (tab.Visibility == Visibility.Visible)
                        {
                            if (tab.IsForceCallSetHeight)
                            {
                                int gridRow = (int)tab.GetValue(Grid.RowProperty);
                                double newHeight = WpfHelper.GetRowHeightAbsValue(grdUserConfig, gridRow);
                                tab.SetHeight(newHeight);
                            }
                        }
                    }
                }
            }
            #endregion

            // количество наборов статусов
            List<KDSUserStatesSet> curStatuses = KDSModeHelper.CurrentKDSStates.StateSets;
            if (curStatuses.Count == 0) return;

            // расширение панели вкладок состояний в зависимости от видимости вкладки группировки блюд
            if (!_isMultipleStatusTabs || (_tabDishGroup.Visibility == Visibility.Visible))
            {
                // снять расширение панели вкладок состояний
                int rowSpan = System.Convert.ToInt32(pnlLeftTabs.GetValue(Grid.RowSpanProperty));
                if (rowSpan > 1)
                {
                    pnlLeftTabs.SetValue(Grid.RowSpanProperty, 1);
                }
            }
            else
            {
                // расширить панель вкладок состояний на несколько строк
                int rowSpan = System.Convert.ToInt32(pnlLeftTabs.GetValue(Grid.RowSpanProperty));
                if (rowSpan == 1)
                {
                    pnlLeftTabs.SetValue(Grid.RowSpanProperty, 3);
                }
            }

            grdUserConfig.UpdateLayout();
            double tabsZoneHeight = pnlLeftTabs.ActualHeight;
            // для каждого набора статусов отдельная вкладка
            if (_isMultipleStatusTabs)
            {
                // кол-во вкладок - кол-во отображаемых статусов для текущей роли КДСа
                double tabHeight;
                double tabsCount = curStatuses.Count;
                double dBetweenKoef = 0.05d;
                if (tabsCount == 1)
                {
                    tabHeight = tabsZoneHeight;
                    tab = new AppLeftTabControl(grdUserConfig.ActualWidth, tabHeight, null, 0d);
                    tab.SetStatesSet(curStatuses[0]);
                    tab.IsEnabled = true;
                    pnlLeftTabs.Children.Add(tab);
                }
                else
                {
                    tabHeight = tabsZoneHeight / (tabsCount + dBetweenKoef * (tabsCount - 1));
                    bool isFirstItem = true;
                    foreach (KDSUserStatesSet item in curStatuses)
                    {
                        tab = new AppLeftTabControl(grdUserConfig.ActualWidth, tabHeight, null, 
                            (isFirstItem) ? 0d: dBetweenKoef);
                        if (isFirstItem) isFirstItem = false;
                        tab.SetStatesSet(item);
                        tab.PreviewMouseDown += tbDishStatusFilter_MouseDown;
                        pnlLeftTabs.Children.Add(tab);
                    }
                }
            }

            // все статусы на одной вкладке и настроить ее для первого набора состояний
            else
            {
                // создать объект циклического перебора
                _orderStatesLooper = new ListLooper<KDSUserStatesSet>(KDSModeHelper.CurrentKDSStates.StateSets);
                KDSUserStatesSet currentStatesSet = null;
                // ПО УМОЛЧАНИЮ  набор состояний - "В Процессе"
                //cookingSet = _userStatesLooper.InnerList.FirstOrDefault(s => s.Name == "В процессе");
                // ПО УМОЛЧАНИЮ  набор состояний - первый: "Все статусы"
                currentStatesSet = _orderStatesLooper.InnerList[0];
                _orderStatesLooper.Current = currentStatesSet;

                // создать вкладку
                tab = new AppLeftTabControl(grdUserConfig.ActualWidth, tabsZoneHeight, null, 0d);
                tab.SetStatesSet(currentStatesSet);
                // перебор по клику мыши, если наборов больше 1
                if (curStatuses.Count > 1) tab.PreviewMouseDown += tbDishStatusFilter_MouseDown;
                pnlLeftTabs.Children.Add(tab);
            }

            // активизировать вкладку состояния
            if ((_isInit) || (grdUserConfig == null))
            {
                // обновить пользовательские фильтры для первой кнопки
                AppLeftTabControl firstTab = (AppLeftTabControl)pnlLeftTabs.Children[0];
                firstTab.IsEnabled = true;
                updateOrderStateFilter(firstTab.StatesSet);
            }
            else if (curStatesSetName != null)
            {
                foreach (AppLeftTabControl item in pnlLeftTabs.Children.OfType<AppLeftTabControl>())
                {
                    if (item.StatesSet.Name == curStatesSetName) {  item.IsEnabled = true; break; }
                }
            }

            // проверка высоты вкладки группировки блюд
            if (_tabDishGroup.Visibility == Visibility.Visible)
            {
                double dRow5Height = Math.Round(WpfHelper.GetRowHeightAbsValue(grdUserConfig, 5), 2);
                if (Math.Round(_tabDishGroup.ActualHeight, 2) != dRow5Height) _tabDishGroup.SetHeight(dRow5Height);
            }

        }  // method


        // перебор фильтров состояний по клику на вкладке
        private void tbDishStatusFilter_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            e.Handled = true;

            // новый набор состояний
            AppLeftTabControl curTab = (AppLeftTabControl)sender;

            // кнопки для каждого набора статусов
            if (_isMultipleStatusTabs)
            {
                if (curTab.IsEnabled)
                {
                    curTab = null;
                }
                else
                {
                    // снять выделение со всех кнопок
                    foreach (UIElement item in pnlLeftTabs.Children)
                    {
                        if (item is AppLeftTabControl)
                        {
                            AppLeftTabControl tab = (item as AppLeftTabControl);
                            if (tab.IsEnabled) tab.IsEnabled = false;
                        }
                    }
                    // установить выделение на нажатую кнопку
                    curTab.IsEnabled = true;
                }
            }

            // одна кнопка
            else
            {
                // сдвинуть лупер и получить новый набор статусов
                _orderStatesLooper.SetNextIndex();
                // обновить вкладку
                curTab = (AppLeftTabControl)pnlLeftTabs.Children[0];
                curTab.SetStatesSet(_orderStatesLooper.Current);
            }

            // обновить пользовательский фильтр текущим набором
            if (curTab != null)
            {
                updateOrderStateFilter(curTab.StatesSet);
            }
        }

        private void updateOrderStateFilter(KDSUserStatesSet newStatesSet)
        {
            AppLib.WriteLogClientAction("Change Status Filter to '{0}'", newStatesSet.Name);
            _dishesFilter.SetAllowedStatuses(newStatesSet);

            List<int> statusesIDs = newStatesSet.States.Select(state => (int)state).ToList();
            _clientFilter.StatusesList = statusesIDs;

            // получить заказы из БД, начиная с первого
            _forceFromFirstOrder = true;
            _isNeedSound = false;  // без проигрывания мелодии
            getOrdersFromService(LeafDirectionEnum.NoLeaf);
        }

        #endregion

        #region вкладка группировки блюд
        private void _tabDishGroup_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            AppLeftTabControl tab = (AppLeftTabControl)sender;
            if (tab.Tag != null)
            {
                string mode = System.Convert.ToString(tab.Tag);
                if (mode == "group") mode = "ungroup"; else mode = "group";
                tab.Tag = mode;

                setDishGroupTabProperties();
                dishGroupAction(mode);
            }
        }

        private void setDishGroupTabProperties()
        {
            if (_tabDishGroup.Tag != null)
            {
                string mode = System.Convert.ToString(_tabDishGroup.Tag);
                // текст на вкладке - для следующего состояния!
                _tabDishGroup.Text = string.Format("{0}" + Environment.NewLine + "блюд",
                    (mode == "group") ? "Разгруп." : "Групп.");
            }
        }

        private void dishGroupAction(string mode)
        {
            if (mode == "group")
            {
                _clientFilter.IsDishGroupAndSumQuantity = true;
                AppLib.WriteLogClientAction("Запрос к службе: сгруппировать одинаковые блюда...");
            }
            else
            {
                _clientFilter.IsDishGroupAndSumQuantity = false;
                AppLib.WriteLogClientAction("Запрос к службе: все блюда раздельно...");
            }

            _forceFromFirstOrder = true;
            _isNeedSound = false;  // без проигрывания мелодии
            getOrdersFromService(LeafDirectionEnum.NoLeaf);
        }

        private void dishGroupTabVisibility(bool visible)
        {
            if (_tabDishGroup == null) return;

            // отобразить вкладку
            if ((_tabDishGroup.Visibility == Visibility.Hidden) && (visible == true))
            {
                _tabDishGroup.Visibility = Visibility.Visible;
            }
            // скрыть вкладку
            else if ((_tabDishGroup.Visibility == Visibility.Visible) && (visible == false))
            {
                // отключить группировку блюд
                if (_clientFilter.IsDishGroupAndSumQuantity == true)
                {
                    _clientFilter.IsDishGroupAndSumQuantity = false;
                    AppLib.WriteLogClientAction("Запрос к службе: отключить группировку блюд, т.к. выключили соотв. вкладку");
                    _forceFromFirstOrder = true;
                    _isNeedSound = false;  // без проигрывания мелодии
                    getOrdersFromService(LeafDirectionEnum.NoLeaf);
                }

                _tabDishGroup.Visibility = Visibility.Hidden;
            }
        }
        private void cbxDishesGroup_Changed(object sender, RoutedEventArgs e)
        {
            //if (cbxDishesGroup.IsChecked ?? false)
            //{
            //    _clientFilter.IsDishGroupAndSumQuantity = true;
            //    AppLib.WriteLogClientAction("Запрос к службе: сгруппировать одинаковые блюда...");
            //}
            //else
            //{
            //    _clientFilter.IsDishGroupAndSumQuantity = false;
            //    AppLib.WriteLogClientAction("Запрос к службе: все блюда раздельно...");
            //}

            //_forceFromFirstOrder = true;
            //_isNeedSound = false;  // без проигрывания мелодии
            //getOrdersFromService(LeafDirectionEnum.NoLeaf);
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
            if (WpfHelper.IsOpenWindow("ColorLegend"))
            {
                ColorLegend colorLegendWin = (ColorLegend)WpfHelper.GetAppGlobalValue("ColorLegendWindow");
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

        // переписывание панелей из bufferOrderPanels в vbxOrders.Child
        private void movePanelsToView()
        {
            Canvas workContainer = (Canvas)vbxOrders.Child;
            List<FrameworkElement> pnlList = new List<FrameworkElement>();

            // очистить канву отображения
            workContainer.Children.Clear();
            // сохранить панели из канвы размещения в коллекции и очистить канву размещения (отсоединить панели)
            saveOrderPanelsToList(bufferOrderPanels, pnlList);
            bufferOrderPanels.Children.Clear();

            // загрузить панели в канву отображения
            pnlList.ForEach(p => workContainer.Children.Add(p));
        }

        // сохранение панелей заказов в коллекцию и удаление их из контейнера
        private void saveOrderPanelsToList(Canvas container, List<FrameworkElement> saveList)
        {
            saveList.Clear();
            foreach (FrameworkElement item in container.Children) saveList.Add(item);
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            openConfigPanel();
        }

        #endregion

    }  // class MainWindow

}
