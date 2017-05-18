using KDSWPFClient.Lib;
using KDSWPFClient.ServiceReference1;
using KDSWPFClient.View;
using KDSWPFClient.ViewModel;
using KDSWPFClient.Model;
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

        private OrdersPages _pages;

        private List<OrderViewModel> _viewOrders;
        //private List<TestData.OrderTestModel> _viewOrders;

        // фильтр данных
        // эти поля устанавливаются где-либо и используются при обращении к службе за данными (getOrdersFromService)
        private ServiceOrderQueueEnum _svcQueue = ServiceOrderQueueEnum.None;  // тип запроса к сервису
        private int _svcQueueIntValue;      // int-значение при обращении к сервису

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
            
            //btnScrollLeft.Visibility = Visibility.Visible;
            //btnScrollRight.Visibility = Visibility.Visible;
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

            // получение различных наборов данных согласно условий отбора
            List<OrderModel> orders = getOrdersFromService();
            if (orders != null) updateOrders(orders);

            _timer.Start();
        }  // method

        private List<OrderModel> getOrdersFromService()
        {
            if (_svcQueue != ServiceOrderQueueEnum.None)
            {
                try
                {
                    List<OrderModel> retVal = null;
                    switch (_svcQueue)
                    {
                        case ServiceOrderQueueEnum.None:
                            break;
                        case ServiceOrderQueueEnum.AllOrders:
                            retVal = _dataProvider.GetOrdersByConditions();
                            break;
                        case ServiceOrderQueueEnum.ByDishStatus:
                            retVal = _dataProvider.GetOrdersByConditions();
                            break;
                        case ServiceOrderQueueEnum.ByDepartment:
                            retVal = _dataProvider.GetOrdersByConditions();
                            break;
                        case ServiceOrderQueueEnum.ByDepartmentGroup:
                            retVal = _dataProvider.GetOrdersByConditions();
                            break;
                        default:
                            break;
                    }
                    return retVal;
                }
                catch (Exception ex)
                {
                    AppLib.WriteLogErrorMessage("Ошибка чтения заказов: {0}", ex.Message);
                    return null;
                }
            }
            return null;
        }


        // обновить внутреннюю коллекцию заказов данными, полученными от сервиса
        private void updateOrders(List<OrderModel> svcOrders)
        {
            //OrderModel om = orders[0];
            //string s = string.Format("id: {0}; Number {1}; hallName {2}; dishes count: {3}", om.Id, om.Number, om.HallName, om.Dishes.Count);
            //Debug.Print(s);

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

    }  // class MainWindow
}
