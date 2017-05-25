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

namespace KDSWPFClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static Timer _timer;

        private AppDataProvider _dataProvider;

        // текущая роль
        private KDSModeEnum _currentKDSMode;
        //  и текущие разрешения
        private KDSModeStates _currentKDSStates;
        private List<int> _userKDSStatesId;

        // классы для циклического перебора клиентских условий отображения блюд
        private ListLooper<OrderGroupEnum> _orderGroupLooper;
        private ListLooper<> _userViewStates;

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

        private DateTime _dtAdmin;


        public MainWindow()
        {
            InitializeComponent();

            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;

            _currentKDSMode = (KDSModeEnum)AppLib.GetAppGlobalValue("KDSMode");
            _currentKDSStates = KDSModeHelper.DefinedKDSModes[_currentKDSMode];
            _dataProvider = (AppDataProvider)AppLib.GetAppGlobalValue("AppDataProvider");

            // классы для циклического перебора клиентских условий отображения блюд
            _orderGroupLooper = new ListLooper<OrderGroupEnum>(new[] { OrderGroupEnum.ByTime, OrderGroupEnum.ByOrderNumber });

            double topBotMargValue = (double)AppLib.GetAppGlobalValue("dishesPanelTopBotMargin");
            this.vbxOrders.Margin = new Thickness(0, topBotMargValue, 0, topBotMargValue);

            _pages = new OrdersPages();
            _viewOrders = new List<OrderViewModel>();
            // debug test data
            //Button_Click(null,null);
            // условия отбора блюд
            _valueDishChecker = new ValueChecker<OrderDishModel>();
            updateDishCheckers();

            _timer = new Timer(1000);
            _timer.Elapsed += _timer_Elapsed;
            _timer.Start();

            // кнопки переключения страниц
            btnSetPagePrevious.Width = (double)AppLib.GetAppGlobalValue("dishesPanelScrollButtonSize");
            btnSetPagePrevious.Height = (double)AppLib.GetAppGlobalValue("dishesPanelScrollButtonSize");
            btnSetPageNext.Width = (double)AppLib.GetAppGlobalValue("dishesPanelScrollButtonSize");
            btnSetPageNext.Height = (double)AppLib.GetAppGlobalValue("dishesPanelScrollButtonSize");

            _delOrderIds = new List<OrderModel>(); _delDishIds = new List<int>();
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

        private void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _timer.Stop();

            try
            {
                this.Dispatcher.Invoke(new Action(updateOrders));
            }
            catch (Exception)
            {

            }

            _timer.Start();
        }  // method


        // обновить внутреннюю коллекцию заказов данными, полученными от сервиса
        // с учетом фильтрации блюд (состояние и отдел)
        private void updateOrders()
        {
            //OrderModel om = orders[0];
            //string s = string.Format("id: {0}; Number {1}; hallName {2}; dishes count: {3}", om.Id, om.Number, om.HallName, om.Dishes.Count);
            //Debug.Print(s);

            // получить заказы от сервиса
            List<OrderModel> svcOrders = _dataProvider.GetOrders();
            if (svcOrders == null) return;

            // удалить из svcOrders блюда, не входящие в условия фильтрации
            // TODO добавить в чекер разрешенные состояния из текущей роли
            // не здесь!!! где хранить чекер для внешних изменений
//            List<OrderStatusEnum> allowedStates = KDSModeHelper.GetKDSModeAllowedStates((KDSModeEnum)AppLib.GetAppGlobalValue("KDSMode"));

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

            Debug.Print("orders {0}", svcOrders.Count);
            svcOrders.ForEach(o => Debug.Print("   order id {0}, dishes {1}", o.Id, o.Dishes.Count));

            // *****
            //  В svcOrder<orderModel> НАХОДИТСЯ СПИСОК, КОТОРЫЙ НЕОБХОДИМО ОТОБРАЗИТЬ НА ЭКРАНЕ
            // *****
            // *** ОБНОВИТЬ _viewOrdes ДАННЫМИ ИЗ svcOrders
            bool isViewRepaint = false;   // признак необходимости перерисовать панели заказов на экране
            // удаление заказов
            List<int> delIds = _viewOrders.Select(vo => vo.Id).Except(svcOrders.Select(o => o.Id)).ToList();
            OrderViewModel orderView;
            foreach (int delId in delIds)
            {
                orderView = _viewOrders.Find(vo => vo.Id == delId);
                if (orderView != null)
                {
                    //_pages.RemoveOrderPanel(orderView);  // удалить панель заказа со страницы
                    _viewOrders.Remove(orderView);  // удалить заказ из внутр.коллекции
                }
                if (isViewRepaint == false) isViewRepaint = true;   // перерисовать
            }
            // обновление списка блюд в заказах
            foreach (OrderModel svcOrder in svcOrders)
            {
                orderView = _viewOrders.FirstOrDefault(o => o.Id == svcOrder.Id);
                if (orderView == null)   // из БД (от службы) пришел новый заказ! - добавить заказ в _viewOrders
                {
                    _viewOrders.Add(new OrderViewModel(svcOrder));
                    if (isViewRepaint == false) isViewRepaint = true;   // перерисовать
                }
                else
                {
                    orderView.UpdateFromSvc(svcOrder);
                    if (orderView.IsDishesListUpdated && !isViewRepaint) isViewRepaint = true;
                }
            }

            // перерисовать полностью
            if (isViewRepaint == true)
            {
                DateTime dt = DateTime.Now;
                _pages.ClearPages(); // очистить панели заказов
                Debug.Print("CLEAR orders - {0}", DateTime.Now - dt);

                // добавить заказы
                dt = DateTime.Now;
                _pages.AddOrdersPanels(_viewOrders);
                Debug.Print("CREATE orders - {0}", DateTime.Now - dt);

                setChangePageButtonsState();

                this.vbxOrders.Child = _pages.CurrentPage;
            }

        }  // method


        private void setChangePageButtonsState()
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
            else if (_pages.CurrentPageIndex < _pages.Count)
            {
                tbPageNextNum.Text = "Стр. " + (_pages.CurrentPageIndex + 1).ToString();
                btnSetPageNext.Visibility = Visibility.Visible;
            }
        }

        private void btnSetPageNext_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_pages.SetNextPage())
            {
                this.vbxOrders.Child = _pages.CurrentPage;
                setChangePageButtonsState();
            }
        }

        private void btnSetPagePrevious_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_pages.SetPreviousPage())
            {
                this.vbxOrders.Child = _pages.CurrentPage;
                setChangePageButtonsState();
            }
        }


        #region настройка приложения через ConfigEdit

        private void grdMain_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Point p = e.GetPosition(grdMain);
            Debug.Print("-- down " + p.ToString());

            if ((p.X <= 15d) && (p.Y <= 15d)) _dtAdmin = DateTime.Now;
        }

        private void grdMain_PreviewMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Point p = e.GetPosition(grdMain);
            int iSec = (DateTime.Now - _dtAdmin).Seconds;
            Debug.Print("-- up {0}, sec {1}", p.ToString(), iSec);

            // длинный тап (более 4 сек) - открываем конфиг
            if ((p.X <= 15d) && (p.Y <= 15d) && (iSec >= 4)) openConfigPanel();
        }

        private void openConfigPanel()
        {
            _timer.Stop();

            ConfigEdit cfgEdit = new ConfigEdit() { DepartmentsDict = _dataProvider.Departments };
            cfgEdit.ShowDialog();

            // обновить фильтр блюд
            if (cfgEdit.AppNewSettings.Count > 0)
            {
                if (cfgEdit.AppNewSettings.ContainsKey("depUIDs")) updCheckerDepUIDs();
                if (cfgEdit.AppNewSettings.ContainsKey("KDSMode"))
                {
                    _currentKDSMode = (KDSModeEnum)AppLib.GetAppGlobalValue("KDSMode");
                    _currentKDSStates = KDSModeHelper.DefinedKDSModes[_currentKDSMode];
                    updCheckerDishStateСfg();
                }
                if (cfgEdit.AppNewSettings.ContainsKey("KDSModeSpecialStates"))
                {
                    _currentKDSStates = KDSModeHelper.DefinedKDSModes[KDSModeEnum.Special];
                    updCheckerDishStateСfg();
                }
            }
            cfgEdit = null;

            _timer.Start();
        }


        private void updateDishCheckers()
        {
            _valueDishChecker.Clear();
            updCheckerDepUIDs();
            updCheckerDishStateСfg();
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
        // разрешенные состояния блюд из config-а
        private void updCheckerDishStateСfg()
        {
            if ((_currentKDSStates.AllowedStates == null) || (_currentKDSStates.AllowedStates.Count == 0))
            {
                _valueDishChecker.Update("dishStatesCfg", (OrderDishModel dish) => false);
            }
            else
            {
                List<int> stateIds = _currentKDSStates.AllowedStates.Select(statEnum => (int)statEnum).ToList();
                _valueDishChecker.Update("dishStatesCfg", (OrderDishModel dish) => stateIds.Contains(dish.DishStatusId));
            }
        }
        private void updCheckerDishStateUser()
        {
            if ((_userKDSStatesId == null) || (_userKDSStatesId.Count == 0))
            {
                _valueDishChecker.Remove("dishStatesUser");
            }
            else
            {
                _valueDishChecker.Update("dishStatesUser", (OrderDishModel dish) => _userKDSStatesId.Contains(dish.DishStatusId));
            }
        }
        #endregion

        #region Настройка приложения боковыми кнопками

        private void set

        private void tbOrderGroup_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {

        }

        private void tbDishStatusFilter_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {

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

            public int CurrentIndex { get { return _currentIndex; } }

            public ListLooper(IEnumerable<T> collection)
            {
                _list = new List<T>(collection);
                _currentIndex = 0;
            }

            public T Next()
            {
                _currentIndex++;
                if (_currentIndex == _list.Count) _currentIndex = 0;

                return _list[_currentIndex];
            }

        }

        private enum OrderGroupEnum { ByTime, ByOrderNumber}

    }  // class MainWindow
}
