﻿using KDSWPFClient.Lib;
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

namespace KDSWPFClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Timer _timer;
        private short _canInvokeUpdateOrders;
        private Timer _timerBackToOrderGroupByTime;  //  таймер возврата группировки заказов по времени
        private Timer _timerBackToFirstPage;        // таймер возврата на первую страницу

        private AppDataProvider _dataProvider;

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
        private List<OrderViewModel> _viewOrders;
        //private List<TestData.OrderTestModel> _viewOrders;

        // временные списки для удаления неразрешенных блюд/заказов, т.к. от службы получаем ВСЕ блюда и ВСЕ заказы в нетерминальных состояниях
        private List<OrderModel> _delOrderIds;
        private List<int> _delDishIds;  
        private bool _isUpdateLayout = false;

        // переменные для опеределения условий отображения окна настройки
        private DateTime _adminDate;
        private int _adminBitMask;
        private Timer _adminTimer;

        private bool _mayGetData;

        // звуки
        System.Media.SoundPlayer _wavPlayer;


        public MainWindow()
        {
            InitializeComponent();

            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;

            _currentKDSMode = (KDSModeEnum)AppLib.GetAppGlobalValue("KDSMode");
            _currentKDSStates = KDSModeHelper.DefinedKDSModes[_currentKDSMode];
            _dataProvider = (AppDataProvider)AppLib.GetAppGlobalValue("AppDataProvider");

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
            updCheckerDepUIDs();  // добавить в фильтр отделы
            updCheckerDishState();  // и состояния

            // класс для циклического перебора группировки заказов
            // в коде используется ТЕКУЩИЙ объект, но на вкладках отображается СЛЕДУЮЩИЙ !!!
            _orderGroupLooper = new ListLooper<OrderGroupEnum>(new[] { OrderGroupEnum.ByTime, OrderGroupEnum.ByOrderNumber });
            setOrderGroupTab();

            double topBotMargValue = (double)AppLib.GetAppGlobalValue("dishesPanelTopBotMargin");
            this.vbxOrders.Margin = new Thickness(0, topBotMargValue, 0, topBotMargValue);

            // отрисовка страниц заказов
            _pages = new OrdersPages();
            _pages.OrdersColumnsCount = AppLib.GetAppSetting("OrdersColumnsCount").ToInt();

            _viewOrders = new List<OrderViewModel>();
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

            _adminTimer = new Timer() { Interval = 4000d, AutoReset = false };
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

                    this.Dispatcher.Invoke(new Action(updateOrders));

                }
                catch (Exception ex)
                {
                }
            }
            _timer.Start();
        }  // method


        private void _adminTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _adminTimer.Stop();
            _adminBitMask = 0;
        }

        // админ жест
        private void grdMain_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Point p = e.GetPosition(grdMain);
            //            Debug.Print("-- down " + p.ToString());

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
                
            Debug.Print("_adminMask = {0}", _adminBitMask.ToString());
        }

        private void grdMain_PreviewMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Point p = e.GetPosition(grdMain);
            //            int iSec = (DateTime.Now - _adminDate).Seconds;
            //            Debug.Print("-- up {0}, sec {1}", p.ToString(), iSec);

            if ((p.X <= brdAdmin.ActualWidth) && (p.Y > 30d) && (p.Y <= 60))
                _adminBitMask = _adminBitMask.SetBit(1); // верхний левый со смещением вниз
            else if ((p.X <= brdAdmin.ActualWidth) && (p.Y >= (brdAdmin.ActualHeight-60d)) && (p.Y <= (brdAdmin.ActualHeight-30d)))  // нижний левый со смещением вверх
            {
                _adminBitMask = _adminBitMask.SetBit(3);
                if (_adminBitMask == 15) openConfigPanel();
            }
            else
                _adminBitMask = 0;
            Debug.Print("_adminMask = {0}", _adminBitMask.ToString());
        }




        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // настройки кнопок пользов.группировки и фильтрации
            double hRow = grdUserConfig.RowDefinitions[1].ActualHeight;
            double wRow = grdUserConfig.ActualWidth;
            double rad = 0.2 * wRow;
            CornerRadius crnRad = new CornerRadius(rad, 0d, 0d, rad);
            Thickness leftBtnMargin = new Thickness(rad, 0d, 0d, 0d);
            Thickness leftTbMargin = new Thickness(0.1d*wRow, 0d, -hRow, -wRow);

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

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if ((_timer != null) && _timer.Enabled)
            {
                _timer.Stop(); _timer.Dispose();
            }
        }


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
            else
            {
                if (tblChannelErrorMessage.Visibility != Visibility.Visible)
                    tblChannelErrorMessage.Visibility = Visibility.Visible;
                if (_pages != null) _pages.ClearPages();
                return;
            }

            //OrderModel om = orders[0];
            //string s = string.Format("id: {0}; Number {1}; hallName {2}; dishes count: {3}", om.Id, om.Number, om.HallName, om.Dishes.Count);
            //Debug.Print(s);

            // получить заказы от сервиса
            List<OrderModel> svcOrders = _dataProvider.GetOrders();
            if (svcOrders == null) return;

            // удалить из svcOrders блюда, не входящие в условия фильтрации
            _delOrderIds.Clear(); _delDishIds.Clear();
            foreach (OrderModel orderMode in svcOrders)
            {
                // собрать Id блюд для удаления
                foreach (KeyValuePair<int, OrderDishModel> item in orderMode.Dishes)
                    if (_valueDishChecker.Checked(item.Value) == false) _delDishIds.Add(item.Key);

                _delDishIds.ForEach(key => orderMode.Dishes.Remove(key)); // удалить неразрешенные блюда

                if (orderMode.Dishes.Count == 0) _delOrderIds.Add(orderMode);
            }
            //   и заказы, у которых нет разрешенных блюд
            _delOrderIds.ForEach(o => svcOrders.Remove(o));



            // *** СОРТИРОВКА ЗАКАЗОВ  ***
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
            // сортировка по времени
            else
            {
                svcOrders = svcOrders.OrderBy(o => o.CreateDate).ThenBy(o => o.Id).ToList();
            }


            // после реорганизации списка блюд: группировка по подачам и сортировка по Ид блюда (порядок записи в БД)
            bool isFilingGroup = true;  // группировка по подачам
            Dictionary<int,OrderDishModel> sortedDishes;
            // сортировка словарей блюд
            foreach (OrderModel orderModel in svcOrders)
            {
                if (isFilingGroup)   // сортировка по подачам и Ид
                    sortedDishes = (from dish in orderModel.Dishes.Values orderby dish.FilingNumber, dish.Id select dish).ToDictionary(d => d.Id);
                else  // сортировка толко по Ид
                    sortedDishes = (from dish in orderModel.Dishes.Values orderby dish.Id select dish).ToDictionary(d => d.Id);
                orderModel.Dishes = sortedDishes;
            }

            // появились ли в svcOrders заказы, которых нет в _viewOrders, т.е. новые?
            // поиск по Id
            bool newOrders = false;
            List<int> idsList = _viewOrders.Select(o => o.Id).ToList();
            foreach (OrderModel dbOrder in svcOrders)
            {
                if (!idsList.Contains(dbOrder.Id)) { newOrders = true; break; }
            }

            // *****
            //  В svcOrder<orderModel> НАХОДИТСЯ СПИСОК, КОТОРЫЙ НЕОБХОДИМО ОТОБРАЗИТЬ НА ЭКРАНЕ
            // *****
            // *** ОБНОВИТЬ _viewOrdes ДАННЫМИ ИЗ svcOrders
            bool isViewRepaint = AppLib.JoinSortedLists<OrderViewModel, OrderModel>(_viewOrders, svcOrders);

            if (newOrders) _wavPlayer.Play();
            // перерисовать полностью
            if ((isViewRepaint == true) || (_pages.CurrentPage.Children.Count == 0)) repaintOrders();

        }  // method

        private void repaintOrders()
        {
            if (_pages == null) return;

            _pages.ClearPages(); // очистить панели заказов

            // добавить заказы
            _pages.AddOrdersPanels(_viewOrders);

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
                // обновить фильтр блюд
                if (cfgEdit.AppNewSettings.ContainsKey("depUIDs"))
                {
                    updCheckerDepUIDs();
                }
                if (cfgEdit.AppNewSettings.ContainsKey("KDSMode"))
                {
                    _currentKDSMode = (KDSModeEnum)AppLib.GetAppGlobalValue("KDSMode");
                    _currentKDSStates = KDSModeHelper.DefinedKDSModes[_currentKDSMode];
                    _userStatesLooper = null;  // обнулить, чтобы создать заново
                    updCheckerDishState();
                }
                if (cfgEdit.AppNewSettings.ContainsKey("KDSModeSpecialStates"))
                {
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


        // отделы, разрешенные на данном КДС (из config-файла)
        private void updCheckerDepUIDs()
        {
            string sAllowDeps = (string)AppLib.GetAppGlobalValue("depUIDs");
            if (sAllowDeps.IsNull())
            {
                _valueDishChecker.Update("depId", (OrderDishModel dish) => false);
            }
            else
            {
                _valueDishChecker.Update("depId", (OrderDishModel dish) => sAllowDeps.Contains(dish.Department.UID));
            }
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

                    // отобразить по умолчанию блюда в Процессе
                    KDSUserStatesSet cookingSet = _userStatesLooper.InnerList.FirstOrDefault(s => s.Name == "В процессе");
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

            // и перерисовать панели заказов
            repaintOrders();
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
            // сдвинуть фильтр
            _userStatesLooper.SetNextIndex();
            // обновить пользовательский фильтр текущим набором
            updCheckerDishState();

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
        #endregion

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

        private void button_Click(object sender, RoutedEventArgs e)
        {
            openConfigPanel();
        }


        private void btnColorsLegend_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            bool isLegendOpen = false;
            foreach (Window win in App.Current.Windows)
            {
                if (win is ColorLegend) { win.Close(); isLegendOpen = true; break; }
            }
            if (!isLegendOpen)
            {
                ColorLegend legend = new ColorLegend();
                legend.ShowDialog();
            }
        }

    }  // class MainWindow
}
