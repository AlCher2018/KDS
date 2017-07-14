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

        private AppDataProvider _dataProvider;
        private bool _isTraceLog, _traceOrderDetails;

        // текущая роль
        private KDSModeEnum _currentKDSMode;
        //  и текущие разрешения
        private KDSModeStates _currentKDSStates;

        // классы для циклического перебора клиентских условий отображения блюд
        private ListLooper<OrderGroupEnum> _orderGroupLooper;
        private ListLooper<KDSUserStatesSet> _userStatesLooper;  // набор фильтров состояний/вкладок (имя, кисти фона и текста, список состояний)

        // страницы заказов
        private OrdersPages _pages;
        // фильтр заказов на данном КДС. Может быть статическим (отделы из config-файла) или динамическим (статус заказов) 
        private ValueChecker<OrderDishModel> _valueDishChecker;
        private Dictionary<int, List<int>> _dependDeps; // словарь (по Id) для хранения Ид отделов (для блюд - дочерних ингредиентов, для ингредиента - родительского блюда)
        private List<int> _preOrdersId;
        private List<OrderViewModel> _viewOrders;  // для отображения на экране

        // временные списки для удаления неразрешенных блюд/заказов, т.к. от службы получаем ВСЕ блюда и ВСЕ заказы в нетерминальных состояниях
        private List<OrderModel> _delOrderIds;
        private List<int> _delDishIds;  

        // переменные для опеределения условий отображения окна настройки
        private int _adminBitMask;
        private Timer _adminTimer;
        private DateTime _adminDate;

        private bool _mayGetData;

        // звуки
        System.Media.SoundPlayer _wavPlayer;

        // CONSTRUCTOR
        public MainWindow()
        {
            InitializeComponent();

            this.Loaded += MainWindow_Loaded;

            _screenWidth = (double)AppLib.GetAppGlobalValue("screenWidth");
            _screenHeight = (double)AppLib.GetAppGlobalValue("screenHeight");

            this.Top = 0; this.Left = 0;
            this.Width = _screenWidth; this.Height = _screenHeight;

            // админ-кнопка для открытия окна конфигурации
            btnCFG.Visibility = (AppLib.GetAppSetting("IsShowCFGButton").ToBool()) ? Visibility.Visible : Visibility.Hidden;
            _isTraceLog = AppLib.GetAppSetting("IsWriteTraceMessages").ToBool();
            _traceOrderDetails = AppLib.GetAppSetting("TraceOrdersDetails").ToBool();

            _dataProvider = (AppDataProvider)AppLib.GetAppGlobalValue("AppDataProvider");
            setCurrentKDSMode();

            double timerInterval = getOrderGroupTimerInterval(); // интервал таймера взять из config-файла
            // таймер автоматического перехода группировки заказов из "По номерам" в "По времени"
            _timerBackToOrderGroupByTime = new Timer() { AutoReset = false, Interval = timerInterval };
            Action _timerBackToOrderGroupByTimeDelegate = new Action(setOrderGroupTab);
            _timerBackToOrderGroupByTime.Elapsed += (object sender, ElapsedEventArgs e) => 
            {
                if (_orderGroupLooper.Current != OrderGroupEnum.ByTime) _orderGroupLooper.Current = OrderGroupEnum.ByTime;
                this.Dispatcher.Invoke(_timerBackToOrderGroupByTimeDelegate);
            };

            // таймер возврата на первую страницу
            _timerBackToFirstPage = new Timer() { AutoReset = false, Interval = timerInterval };
            Action _timerBackToFirstPageDelegate = new Action(() =>
            {
                _pages.SetFirstPage();
                setCurrentPage();
            });
            _timerBackToFirstPage.Elapsed += (object sender, ElapsedEventArgs e) => this.Dispatcher.Invoke(_timerBackToFirstPageDelegate);

            // условия отбора блюд
            _valueDishChecker = new ValueChecker<OrderDishModel>();
            // добавить в фильтр отделы, разрешенные на данном КДС
            _valueDishChecker.Update("depId", checkAllowDepertment);
            updCheckerDishState();  // и состояния

            // класс для циклического перебора группировки заказов
            // в коде используется ТЕКУЩИЙ объект, но на вкладках отображается СЛЕДУЮЩИЙ !!!
            _orderGroupLooper = new ListLooper<OrderGroupEnum>(new[] { OrderGroupEnum.ByTime, OrderGroupEnum.ByOrderNumber });
            setOrderGroupTab();

            double topBotMargValue = (double)AppLib.GetAppGlobalValue("dishesPanelTopBotMargin");
            this.vbxOrders.Margin = new Thickness(0, topBotMargValue, 0, topBotMargValue);

            _preOrdersId = new List<int>();
            _viewOrders = new List<OrderViewModel>();
            _dependDeps = new Dictionary<int, List<int>>();
            // debug test data
            //Button_Click(null,null);

            // основной таймер опроса сервиса
            _timer = new Timer(100) { AutoReset = false };
            _timer.Elapsed += _timer_Elapsed;
            _timer.Start(); _canInvokeUpdateOrders = -1;

            // кнопки переключения страниц
            btnSetPagePrevious.Width = (double)AppLib.GetAppGlobalValue("dishesPanelScrollButtonSize");
            btnSetPagePrevious.Height = (double)AppLib.GetAppGlobalValue("dishesPanelScrollButtonSize");
            btnSetPageNext.Width = (double)AppLib.GetAppGlobalValue("dishesPanelScrollButtonSize");
            btnSetPageNext.Height = (double)AppLib.GetAppGlobalValue("dishesPanelScrollButtonSize");

            _delOrderIds = new List<OrderModel>(); _delDishIds = new List<int>();

            _adminTimer = new Timer() { Interval = 3000d, AutoReset = false };
            _adminTimer.Elapsed += _adminTimer_Elapsed;

            // звук предупреждения о появлении нового заказа
            _wavPlayer = new System.Media.SoundPlayer();
            var wavFile = AppLib.GetAppGlobalValue("NewOrderAudioAttention");
            if (wavFile != null)
            {
                _wavPlayer.SoundLocation = AppLib.GetAppDirectory("Audio") + wavFile;
                _wavPlayer.LoadAsync();
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // hide Close button
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~WS_SYSMENU);

            // размер канвы для панелей заказов
            double topBotMargin = (double)AppLib.GetAppGlobalValue("dishesPanelTopBotMargin");
            double cnvHeight = Math.Floor(grdMain.ActualHeight - 2d * topBotMargin);
            double cnvWidth = grdMain.ActualWidth;
            _pages = new OrdersPages(cnvWidth, cnvHeight);
            _pages.OrdersColumnsCount = AppLib.GetAppSetting("OrdersColumnsCount").ToInt();

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

        private string getDepsFilter()
        {
            string retVal = "";
            DepartmentViewModel curItem;
            foreach (KeyValuePair<int, DepartmentViewModel> pair in _dataProvider.Departments)
            {
                curItem = pair.Value;
                if (curItem.IsViewOnKDS)
                {
                    if (retVal.IsNull() == false) retVal += ", ";
                    retVal += string.Format("{0} (id {1})", curItem.Name, curItem.Id);
                }
            }
            return retVal;
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
        private void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            DateTime dt = DateTime.Now;
            short seconds = (short)dt.Second;
            if ((dt.Millisecond <= 200) && (_canInvokeUpdateOrders != seconds))
            {
                _timer.Stop();
                _canInvokeUpdateOrders = seconds;
                _mayGetData = false;
                try
                {
                    if (_dataProvider.EnableChannels == false)
                        _mayGetData = _dataProvider.CreateChannels();
                    else
                        _mayGetData = true;
                }
                catch (Exception ex)
                {
                    AppLib.WriteLogErrorMessage("** Ошибка обновления заказов: {0}", AppLib.GetShortErrMessage(ex));
                }

                this.Dispatcher.Invoke(new Action(updateOrders));
            }
            _timer.Start();
        }  // method


        // 
        // *********  ОСНОВНОЙ МЕТОД ПОЛУЧЕНИЯ ЗАКАЗОВ И ИХ ФИЛЬТРАЦИИ И ГРУППИРОВКИ НА КДСe  ************
        //
        // обновить внутреннюю коллекцию заказов данными, полученными от сервиса
        // с учетом фильтрации блюд (состояние и отдел)
        private void updateOrders()
        {
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
                return;
            }

            // получить заказы от сервиса
            List<OrderModel> svcOrders = _dataProvider.GetOrders();
            if (svcOrders == null) return;

            if (_traceOrderDetails)
            {
                string s1 = "";
                if (svcOrders.Count > 0) s1 = string.Join("; ", svcOrders.Select(o => o.Id.ToString() + ", блюд "+o.Dishes.Count.ToString()).ToArray());
                AppLib.WriteLogTraceMessage(" - от службы получено заказов: {0} (ids: {1})", svcOrders.Count, s1);
                AppLib.WriteLogTraceMessage(" - отображаемые отделы: {0}", getDepsFilter());
            }

            //обновить словарь зависимых/зависящих отделов, т.е. для блюда это будет список отделов ингредиентов этого блюда, а для ингредиента это будет отдел родительского блюда. Ключ - поле Id из БД, для уникальности в пределах всех заказов
            if (_traceOrderDetails) AppLib.WriteLogTraceMessage(" - обновляю служебный словарь _dependDeps...");
            try
            {
                updateDependDepsDict(svcOrders);
                if (_traceOrderDetails) AppLib.WriteLogTraceMessage("   словарь _dependDeps обновлен успешно");
            }
            catch (Exception ex1)
            {
                AppLib.WriteLogErrorMessage("   Error: {0}", AppLib.GetShortErrMessage(ex1));
            }

            // удалить из svcOrders блюда, не входящие в условия фильтрации
            if (_traceOrderDetails) AppLib.WriteLogTraceMessage(" - фильтрация блюд (цеха и статусы данного КДС)...");
            try
            {
                _delOrderIds.Clear(); _delDishIds.Clear();
                foreach (OrderModel orderModel in svcOrders)
                {
                    // собрать Id блюд для удаления, т.е. не прошедшие фильтры по отделам и состояниям
                    foreach (KeyValuePair<int, OrderDishModel> item in orderModel.Dishes)
                    {
                        if (_valueDishChecker.Checked(item.Value) == false) _delDishIds.Add(item.Key);
                    }

                    _delDishIds.ForEach(key => orderModel.Dishes.Remove(key)); // удалить неразрешенные блюда

                    if (orderModel.Dishes.Count == 0) _delOrderIds.Add(orderModel);
                }
                //   и заказы, у которых нет разрешенных блюд
                _delOrderIds.ForEach(o => svcOrders.Remove(o));
            }
            catch (Exception ex2)
            {
                AppLib.WriteLogErrorMessage("Ошибка удаления из svcOrders блюд, не входящие в условия фильтрации: {0}", ex2.ToString());
            }

            if (_traceOrderDetails)
            {
                string s1 = "";
                if (svcOrders.Count > 0) s1 = string.Join("; ", svcOrders.Select(o => o.Id.ToString() + ", блюд " + o.Dishes.Count.ToString()).ToArray());
                AppLib.WriteLogTraceMessage("   после фильтрации заказов {0}, (ids: {1})", svcOrders.Count, s1);
            }

            // условие проигрывания мелодии при появлении нового заказа
            // появились ли в svcOrders (УЖЕ ОТФИЛЬТРОВАННОМ ПО ОТДЕЛАМ И СТАТУСАМ) заказы, 
            // которых нет в preOrdersId, т.е. новые? (поиск по Id)
            int[] curOrdersId = svcOrders.Select(o => o.Id).Distinct().ToArray();  // собрать уникальные Id
            if ((_preOrdersId.Count > 0) || (_preOrdersId.Count==0 && svcOrders.Count != 0))
            {
                foreach (int curId in curOrdersId)
                    if (!_preOrdersId.Contains(curId)) {
                        _wavPlayer.Play() ; break;
                    }
                _preOrdersId.Clear();
            }
            _preOrdersId.AddRange(curOrdersId);


            // *** СОРТИРОВКА ЗАКАЗОВ  ***
            if (_traceOrderDetails) AppLib.WriteLogTraceMessage(" - режим группировки заказов: {0}", _orderGroupLooper.Current.ToString().ToUpper());
            // группировка и сортировка заказов по номерам
            if (_orderGroupLooper.Current == OrderGroupEnum.ByOrderNumber)
            {
                svcOrders = svcOrders.OrderBy(o => o.Number).ToList();

                int cntOrders = svcOrders.Count;
                OrderModel curOrder, sameNumOrder;
                for (int i = 0; i < cntOrders; i++)
                {
                    curOrder = svcOrders[i];
                    if (curOrder.Dishes.Count > 0)
                    {
                        sameNumOrder = null;
                        // найти еще заказы с таким же номером
                        for (int j = i+1; j < cntOrders; j++)
                        {
                            if (curOrder.Number == svcOrders[j].Number)
                            {
                                sameNumOrder = svcOrders[j];
                                foreach (OrderDishModel item in sameNumOrder.Dishes.Values) curOrder.Dishes.Add(item.Id, item);
                                sameNumOrder.Dishes.Clear();
                                i++;
                            }
                        }
                    }
                }
                // пройтись еще раз по заказам и удалить пустые
                svcOrders.RemoveAll(o => o.Dishes.Count==0);
            }

            // группировка по времени БЛЮДА и по номеру заказа
            else
            {
                List<DateNumberOrder> sortOrders = new List<DateNumberOrder>();
                OrderModel tmpOrder;
                foreach (OrderModel om in svcOrders)
                {
                    // список уникальных дат
                    List<DateTime> dtList = om.Dishes.Values.Select(d => d.CreateDate).Distinct().ToList();
                    // копия блюд по датам
                    foreach (DateTime dt in dtList)
                    {
                        tmpOrder = getCopyOrderModel(om, dt);
                        sortOrders.Add(new DateNumberOrder() {DishDate = dt, Number = om.Number, Order = tmpOrder } );
                    }
                }
                // сортировка по CreateDate блюд и номеру заказа
                sortOrders = (from o in sortOrders
                              orderby o.DishDate ascending, o.Number ascending
                              select o).ToList();

                svcOrders.Clear();
                svcOrders = sortOrders.Select(s => s.Order).ToList();
            }

            if (_traceOrderDetails)
            {
                string s1 = "";
                if (svcOrders.Count > 0) s1 = string.Join("; ", svcOrders.Select(o => o.Id.ToString() + ", блюд " + o.Dishes.Count.ToString()).ToArray());
                AppLib.WriteLogTraceMessage("   заказов после группировки: {0}, (ids: {1})", svcOrders.Count, s1);
            }

            // после реорганизации списка блюд
            // группировка по подачам если надо
            bool isFilingGroup = true;  
            Dictionary<int,OrderDishModel> sortedDishes;
            // сортировка словарей блюд
            foreach (OrderModel orderModel in svcOrders)
            {
                if (isFilingGroup)   // сортировка по подачам и Ид
                {
                    sortedDishes = (from dish in orderModel.Dishes.Values orderby dish.FilingNumber select dish).ToDictionary(d => d.Id);
                    orderModel.Dishes = sortedDishes;
                }
            }

            // *** ОБНОВИТЬ _viewOrdes (для отображения на экране) ДАННЫМИ ИЗ svcOrders (получено из БД)
            // в случае с группировкой по времени и разбивкой заказов на несколько панелей AppLib.JoinSortedLists() работает НЕПРАВИЛЬНО!!!
            //bool isViewRepaint = AppLib.JoinSortedLists<OrderViewModel, OrderModel>(_viewOrders, svcOrders);
            // поэтому сделано уникальной процедурой
            if (_traceOrderDetails) AppLib.WriteLogTraceMessage("   обновление служебной коллекции заказов (для отображения на экране)...");
            bool isViewRepaint2 = false;
            try
            {
                isViewRepaint2 = updateViewOrdersList(svcOrders);
            }
            catch (Exception ex)
            {
                AppLib.WriteLogErrorMessage("Ошибка обновления служебной коллекции заказов для отображения на экране: {0}", ex.ToString());
            }

            if (_traceOrderDetails)
            {
                string s1 = "";
                if (_viewOrders.Count > 0) s1 = string.Join("; ", _viewOrders.Select(o => o.Id.ToString() + ", блюд " + o.Dishes.Count.ToString()).ToArray());
                AppLib.WriteLogTraceMessage("   для отображения на экране заказов: {0}, (ids: {1}) - {2}", _viewOrders.Count, s1, (isViewRepaint2 ? "ПЕРЕРИСОВКА всех заказов" : "только счетчики"));
            }

            // перерисовать полностью
            if ((isViewRepaint2 == true) 
                || ((_pages.CurrentPage.Children.Count == 0) && (_viewOrders.Count != 0))) repaintOrders();

        }  // method


        //создать словарь зависимых/зависящих отделов, т.е. для блюда это будет список отделов ингредиентов этого блюда, а для ингредиента это будет отдел родительского блюда. Ключ - поле Id из БД, для уникальности в пределах всех заказов
        // словарь будет уровня класса, чтобы не нарушать работу вложенного класса ValueChecker для отделов
        private void updateDependDepsDict(List<OrderModel> svcOrders)
        {
            _dependDeps.Clear();
            foreach (OrderModel orderModel in svcOrders)
            {
                // отобрать только блюда
                List<OrderDishModel> v1 = orderModel.Dishes.Values.Where(d => d.ParentUid.IsNull()).ToList();
                foreach (OrderDishModel dish in v1)
                {
                    List<int> dd = new List<int>();
                    _dependDeps.Add(dish.Id, dd);
                    // ингредиенты к блюду
                    List<OrderDishModel> v2 = orderModel.Dishes.Values.Where(d => d.ParentUid == dish.Uid).ToList();
                    foreach (OrderDishModel ingr in v2)
                    {
                        dd.Add(ingr.DepartmentId);
                        if (_dependDeps.ContainsKey(ingr.Id) == false)
                            _dependDeps.Add(ingr.Id, new List<int>() { dish.DepartmentId });  // а для ингредиента - Ид отдела родит.блюда
                    }
                }  // foreach dish
            }  // foreach order
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

        // создать копию OrderMode с блюдами, у которых дата равна параметру
        // для разбивки заказов по датам блюд (!!!)
        private OrderModel getCopyOrderModel(OrderModel om, DateTime dtDish)
        {
            OrderModel retVal = new OrderModel()
            {
                CreateDate = dtDish,
                Dishes = new Dictionary<int, OrderDishModel>(),
                HallName = om.HallName,
                Id = om.Id,
                Number = om.Number,
                OrderStatusId = om.OrderStatusId,
                TableName = om.TableName,
                Uid = om.Uid,
                Waiter = om.Waiter,
                WaitingTimerString = om.WaitingTimerString,
                DivisionColorRGB = om.DivisionColorRGB
            };
            // скопировать ссылки на блюда
            // ингредиенты копируются вместе с блюдом, независимо от флажка IsIngredientsIndependent
            bool isIngrIndepend = (bool)AppLib.GetAppGlobalValue("IsIngredientsIndependent", false);
            // все блюда
            List<OrderDishModel> dishes = om.Dishes.Values.ToList();
            // блюда для копирования
            List<OrderDishModel> dishesForCopy = dishes.Where(d => d.ParentUid.IsNull() && d.CreateDate == dtDish).ToList();
            List<OrderDishModel> ingrsForCopy;
            foreach (OrderDishModel dm in dishesForCopy)
            {
                retVal.Dishes.Add(dm.Id, dm);
                dishes.Remove(dm);
                // и собрать все ингредиенты с ТАКИМ же CreateDate!
                ingrsForCopy = dishes.Where(d => (d.Uid == dm.Uid) && (d.ParentUid == dm.Uid) && (d.CreateDate == dtDish)).ToList();
                foreach (OrderDishModel dmIngr in ingrsForCopy)
                {
                    if (!retVal.Dishes.ContainsKey(dmIngr.Id))
                    {
                        retVal.Dishes.Add(dmIngr.Id, dmIngr);
                        dishes.Remove(dmIngr);
                    }
                }
            }
            // оставшиеся блюда/ингредиенты на дату dtDish
            List<OrderDishModel> rest = dishes.Where(d => d.CreateDate == dtDish).ToList();
            foreach (OrderDishModel dm in rest) retVal.Dishes.Add(dm.Id, dm);

            return retVal;
        }

        private void repaintOrders()
        {
            if (_pages == null) return;

            _pages.ClearPages(); // очистить панели заказов

            // добавить заказы
            //DebugTimer.Init("AddOrdersPanels");
            _pages.AddOrdersPanels(_viewOrders);
            //DebugTimer.GetInterval();

            setCurrentPage();
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

        private void setCurrentPage()
        {
            this.vbxOrders.Child = _pages.CurrentPage;

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
            if (_timerBackToFirstPage.Enabled) _timerBackToFirstPage.Stop();
            if (_pages.CurrentPageIndex > 1) _timerBackToFirstPage.Start();
        }

        #endregion

        #region настройка приложения через ConfigEdit


        // ******
        // ФОРМА НАСТРОЕК И ОБНОВЛЕНИЕ ПОЛЕЙ ПОСЛЕ НАСТРОЙКИ ПАРАМЕТРОВ ПРИЛОЖЕНИЯ
        // ******
        private void openConfigPanel()
        {
            _timer.Stop();

            ConfigEdit cfgEdit = new ConfigEdit() { DepartmentsDict = _dataProvider.Departments };
            cfgEdit.ShowDialog();


            //  ОБНОВЛЕНИЕ ПАРАМЕТРОВ ПРИЛОЖЕНИЯ
            if (cfgEdit.AppNewSettings.Count > 0)
            {
                if (cfgEdit.AppNewSettings.ContainsKey("depUIDs"))
                {
                    setCurrentKDSMode();
                }

                // обновить фильтр блюд
                if (cfgEdit.AppNewSettings.ContainsKey("KDSMode"))
                {
                    setCurrentKDSMode();
                    _userStatesLooper = null;  // обнулить, чтобы создать заново
                    updCheckerDishState();
                }
                else if (cfgEdit.AppNewSettings.ContainsKey("KDSModeSpecialStates") 
                    && (cfgEdit.AppNewSettings["KDSModeSpecialStates"].IsNull() == false))
                {
                    if (_currentKDSMode != KDSModeEnum.Special) _currentKDSMode = KDSModeEnum.Special;
                    _currentKDSStates = KDSModeHelper.DefinedKDSModes[KDSModeEnum.Special];
                    _userStatesLooper = null;  // обнулить, чтобы создать заново
                    updCheckerDishState();
                }

                // масштаб шрифта
                if (cfgEdit.AppNewSettings.ContainsKey("AppFontScale"))
                {
                    repaintOrders();  // перерисовать полностью, т.к. по таймеру может все не обновиться
                }

                // кол-во колонок заказов
                if (cfgEdit.AppNewSettings.ContainsKey("OrdersColumnsCount"))
                {
                    _pages.OrdersColumnsCount = AppLib.GetAppSetting("OrdersColumnsCount").ToInt();
                    AppLib.RecalcOrderPanelsLayot();
                    _pages.ResetOrderPanelSize();
                    repaintOrders();  // перерисовать заказы
                }

                // интервал таймера сброса группировки заказов по номерам
                if (cfgEdit.AppNewSettings.ContainsKey("AutoReturnOrdersGroupByTime"))
                {
                    double newInterval = getOrderGroupTimerInterval();
                    _timerBackToOrderGroupByTime.Interval = newInterval;
                    _timerBackToFirstPage.Interval = newInterval;
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
                    AppLib.SetAppGlobalValue("NewOrderAudioAttention", wavFile);
                    // в config-файле
                    AppLib.SaveAppSettings("NewOrderAudioAttention", wavFile);
                    // и загрузить в проигрыватель
                    _wavPlayer.SoundLocation = AppLib.GetAppDirectory("Audio") + wavFile;
                    _wavPlayer.LoadAsync();
                }

            }
            cfgEdit = null;

            _timer.Start();
        }

        // получить из config-файла интервал таймера сброса группировки заказов по номерам
        private double getOrderGroupTimerInterval()
        {
            string cfgStr = AppLib.GetAppSetting("AutoReturnOrdersGroupByTime");
            return 1000d * ((cfgStr.IsNull()) ? 10d : cfgStr.ToDouble());  // и перевести в мсек
        }

        private void setCurrentKDSMode()
        {
            _currentKDSMode = (KDSModeEnum)AppLib.GetAppGlobalValue("KDSMode");
            _currentKDSStates = KDSModeHelper.DefinedKDSModes[_currentKDSMode];

            // отобразить в заголовке роль КДСа
            string title = "KDS - " + _currentKDSMode.ToString().ToUpper();
            // и отображаемые цеха
            if (_dataProvider != null)
            {
                string depNames = _dataProvider.GetDepNames();
                if (!depNames.IsNull()) title += " (" + depNames + ")";
            }

            this.Title = title;
        }

        // в _dataProvider.Departments, поле IsViewOnKDS = true, если отдел разрешен для показа на этом КДСе
        // а в _dependDeps - для каждого Id блюда/ингр список зависимых Id отделов
        private bool checkAllowDepertment(OrderDishModel dishModel)
        {
            // проверка в разрешенных самого блюда
            if (isDepIdAllow(dishModel.DepartmentId)) return true;

            // проверка зависимых ингредиентов
            if (_dependDeps.ContainsKey(dishModel.Id) && _dependDeps[dishModel.Id] != null)
            {
                foreach (int ddId in _dependDeps[dishModel.Id])
                    if (isDepIdAllow(ddId)) return true;
            }

            return false;
        }
        private bool isDepIdAllow(int depId)
        {
            return AppLib.IsDepViewOnKDS(depId, _dataProvider);
        }

        // обновить фильтр состояний блюд
        private void updCheckerDishState()
        {
            List<int> ids;
            // если нет пользовательского фильтра состояний, то взять фильтр роли
            if ((_currentKDSStates.StateSets == null) || (_currentKDSStates.StateSets.Count == 0))
            {
                // применить фильтр роли
                if ((_currentKDSStates.AllowedStates == null) || (_currentKDSStates.AllowedStates.Count == 0))
                {
                    _valueDishChecker.Update("dishStates", (OrderDishModel dish) => false);
                }
                else
                {
                    ids = _currentKDSStates.AllowedStates.Select(statEnum => (int)statEnum).ToList();
                    _valueDishChecker.Update("dishStates", (OrderDishModel dish) => ids.Contains(dish.DishStatusId));
                }

                // уничтожить объект перебора значений
                if (_userStatesLooper != null) _userStatesLooper = null;
                // скрыть вкладку перебора фильтра состояний
                btnDishStatusFilter.Visibility = Visibility.Hidden;
            }

            // применить пользовательский фильтр состояний
            else
            {
                // попытаться создать объект перебора значений
                if (_userStatesLooper == null)
                {
                    _userStatesLooper = new ListLooper<KDSUserStatesSet>(_currentKDSStates.StateSets);

                    KDSUserStatesSet cookingSet = null;
                    // ПО УМОЛЧАНИЮ  набор состояний - "В Процессе"
                    //cookingSet = _userStatesLooper.InnerList.FirstOrDefault(s => s.Name == "В процессе");
                    // ПО УМОЛЧАНИЮ  набор состояний - первый: "Все статусы"
                    if ((_userStatesLooper.InnerList != null) && (_userStatesLooper.InnerList.Count > 0))
                        cookingSet = _userStatesLooper.InnerList[0];

                    if (cookingSet != null) _userStatesLooper.Current = cookingSet;
                }

                if (_userStatesLooper == null)
                {
                    btnDishStatusFilter.Visibility = Visibility.Hidden;
                    return;
                }

                // текущий набор состояний
                KDSUserStatesSet statesSet = _userStatesLooper.Current;
                // для фильтра берем список Ид состояний
                ids = statesSet.States.Select(s => (int)s).ToList();
                _valueDishChecker.Update("dishStates", (OrderDishModel dish) => ids.Contains(dish.DishStatusId));

                // вкладка перебора фильтров состояний
                if (btnDishStatusFilter.Visibility == Visibility.Hidden) btnDishStatusFilter.Visibility = Visibility.Visible;
                setUserStatesTab();  // отобразить на вкладке текущий набор
            }
        }
        #endregion

        #region Настройка приложения боковыми кнопками
        // *************************
        // *** группировка заказов
        private void tbOrderGroup_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // сдвинуть текущий элемент
            _orderGroupLooper.SetNextIndex();
            // и отобразить следующий
            setOrderGroupTab();
        }

        // отобразить на вкладке СЛЕДУЮЩИЙ элемент!!
        private void setOrderGroupTab()
        {
            //OrderGroupEnum eOrderGroup = _orderGroupLooper.GetNextObject();
            OrderGroupEnum eOrderGroup = _orderGroupLooper.Current; // отображать текущий объект!!

            switch (eOrderGroup)
            {
                case OrderGroupEnum.ByTime:
                    tbOrderGroup.Text = "По времени";
                    break;

                case OrderGroupEnum.ByOrderNumber:
                    tbOrderGroup.Text = "По заказам";
                    if (_timerBackToOrderGroupByTime != null) _timerBackToOrderGroupByTime.Start();
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
            _userStatesLooper.SetNextIndex();

            // обновить пользовательский фильтр текущим набором
            updCheckerDishState();

            _preOrdersId.Clear();

            _timer.Start();
        }

        // отобразить следующий набор фильтров по состоянию на вкладке
        private void setUserStatesTab()
        {
            //KDSUserStatesSet statesSet = _userStatesLooper.GetNextObject();
            KDSUserStatesSet statesSet = _userStatesLooper.Current;

            btnDishStatusFilter.Background = statesSet.BackBrush;

            tbDishStatusFilter.Text = statesSet.Name;
            tbDishStatusFilter.Foreground = statesSet.FontBrush;
        }

        #endregion

        #region inner classes
        // тип TObj - тип объекта, для которого будет проверяться предикат
        private class ValueChecker<T>
        {
            private List<KeyValuePair<string, Predicate<T>>> _predicatesList;

            public ValueChecker()
            {
                _predicatesList = new List<KeyValuePair<string, Predicate<T>>>();
            }

            // все условия должны быть true, т.е. соединяем по AND
            public bool Checked(T checkObject)
            {
                foreach (KeyValuePair<string, Predicate<T>> item in _predicatesList)
                {
                    if (item.Value(checkObject) == false) return false;
                }
                return true;
            }

            public void Add(string key, Predicate<T> predicate)
            {
                _predicatesList.Add(new KeyValuePair<string, Predicate<T>>(key, predicate));
            }

            public void Update(string key, Predicate<T> newPredicate)
            {
                KeyValuePair<string, Predicate<T>> pred = _predicatesList.FirstOrDefault(p => p.Key == key);

                if (pred.Key != null) _predicatesList.Remove(pred);

                _predicatesList.Add(new KeyValuePair<string, Predicate<T>>(key, newPredicate));
            }

            // удалить ВСЕ записи с данным ключем
            public void Remove(string key)
            {
                KeyValuePair<string, Predicate<T>> pred = _predicatesList.FirstOrDefault(p => p.Key == key);

                if (pred.Key != null)
                {
                    _predicatesList.Remove(pred);
                }
            }

            public void Clear()
            {
                _predicatesList.Clear();
            }

        }

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

        private enum OrderGroupEnum { ByTime, ByOrderNumber}

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
//            Debug.Print("-- up {0}, sec {1}", p.ToString(), iSec);

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

        private void btnColorsLegend_PreviewMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            double admSecs = (DateTime.Now - _adminDate).TotalSeconds;
            if ((admSecs > 3d) && (admSecs < 10d))
            {
                openConfigPanel();
            }
            else
                App.OpenColorLegendWindow();

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
