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


namespace KDSWPFClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static Timer _timer;   // статический, чтобы не было повторяющихся чисел
        private static AppDataProvider _dataProvider;
        public AppDataProvider DataProvider { set { _dataProvider = value; } }

        private OrdersPages _pages;

        private List<OrderViewModel> _viewOrders;
        //private List<TestData.OrderTestModel> _viewOrders;

        private OrderStatusEnum _allowStatus = OrderStatusEnum.None;

        private bool _isUpdateLayout = false;

        public MainWindow()
        {
            InitializeComponent();

            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;

            double topBotMargValue = (double)AppLib.GetAppGlobalValue("dishesPanelTopBotMargin");
            this.vbxOrders.Margin = new Thickness(0, topBotMargValue, 0, topBotMargValue);

            _pages = new OrdersPages();
            _viewOrders = new List<OrderViewModel>();
            // debug test data
            //Button_Click(null,null);

            _timer = new Timer(1000);
            _timer.Elapsed += _timer_Elapsed;
            _timer.Start();

            // кнопки переключения страниц
            btnSetPagePrevious.Width = (double)AppLib.GetAppGlobalValue("dishesPanelScrollButtonSize");
            btnSetPagePrevious.Height = (double)AppLib.GetAppGlobalValue("dishesPanelScrollButtonSize");
            btnSetPageNext.Width = (double)AppLib.GetAppGlobalValue("dishesPanelScrollButtonSize");
            btnSetPageNext.Height = (double)AppLib.GetAppGlobalValue("dishesPanelScrollButtonSize");
        
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
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

            updateOrders();

            _timer.Start();
        }  // method


        // обновить внутреннюю коллекцию заказов данными, полученными от сервиса
        // с учетом фильтрации блюд (состояние и отдел)
        private void updateOrders()
        {
            //OrderModel om = orders[0];
            //string s = string.Format("id: {0}; Number {1}; hallName {2}; dishes count: {3}", om.Id, om.Number, om.HallName, om.Dishes.Count);
            //Debug.Print(s);

            // получение заказов
            List<OrderModel> svcOrders = _dataProvider.GetOrders();
            if (svcOrders == null) return;

            // удалить из svcOrders блюда, не входящие в условия фильтрации
            foreach (OrderModel ord in svcOrders)
            {
                foreach (OrderDishModel item in ord.Dishes.Values)
                {
                    if (isDishAllow(item) == false) ord.Dishes.Remove(item.Id);
                }
            }

            // *** ОБНОВИТЬ _viewOrdes ДАННЫМИ ИЗ orders
            // удаление заказов
            IEnumerable<int> delKeys = _viewOrders.Select(vo => vo.Id).Except(svcOrders.Select(o => o.Id));
            foreach (int key in delKeys) _viewOrders.Remove(_viewOrders.Find(vo => vo.Id == key));

            foreach (OrderModel svcOrder in svcOrders)
            {
                OrderViewModel ovm = _viewOrders.FirstOrDefault(o => o.Id == svcOrder.Id);
                if (ovm == null)
                {
                    // добавление заказа в словарь
                    _viewOrders.Add(new OrderViewModel(svcOrder));
                }
                else
                {
                    // обновить существующий заказ
                    //curOrder = _orders[dbOrder.Id];
                    //curOrder.UpdateFromDBEntity(dbOrder);
                }
            }
        }

        private bool isDishAllow(OrderDishModel item)
        {
            // проверка на отдел
            DepartmentViewModel dep = _dataProvider.GetDepartmentById(item.Department.Id);
            if (dep.IsViewOnKDS == false) return false;

            // проверка состояния
            if (_allowStatus == OrderStatusEnum.None)
                return true;
            else if ((item.Status != _allowStatus) && (item.Status != OrderStatusEnum.Cancelled))
            {
                return false;
            }

            return true;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //_viewOrders = TestData.TestDataHelper.GetTestOrders(5, 30);

            //updateViewOrders();
        }


        private void updateViewOrders()
        {
            DateTime dt = DateTime.Now;
            _pages.Clear(); // очистить панели заказов
            Debug.Print("CLEAR orders - {0}", DateTime.Now - dt);

            // добавить заказы
            dt = DateTime.Now;
            _pages.AddOrders(_viewOrders);
            Debug.Print("CREATE orders - {0}", DateTime.Now - dt);

            setChangePageButtonsState();

            this.vbxOrders.Child = _pages.CurrentPage;
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

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            ConfigEdit cfgEdit = new ConfigEdit() { DepartmentsDict = _dataProvider.Departments };
            cfgEdit.ShowDialog();
        }


    }  // class MainWindow
}
