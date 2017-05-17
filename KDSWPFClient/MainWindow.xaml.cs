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

        private OrdersPages _pages;

        //private List<OrderViewModel> _viewOrders;
        private List<TestData.OrderTestModel> _viewOrders;

        private bool _isUpdateLayout = false;

        public MainWindow()
        {
            InitializeComponent();

            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;

            double topBotMargValue = (double)AppLib.GetAppGlobalValue("ordPnlTopBotMargin");
            this.vbxOrders.Margin = new Thickness(0, topBotMargValue, 0, topBotMargValue);

            _pages = new OrdersPages();
            //_viewOrders = new List<OrderViewModel>();
            _viewOrders = TestData.TestDataHelper.GetTestOrders(5, 10);
            updateViewOrders();

            //createTestData();

            //_timer = new Timer(1000);
            //_timer.Elapsed += _timer_Elapsed;
            //_timer.Start();
        }

        private void createTestData()
        {
            OrderPanel ordPnl = new OrderPanel();
            ordPnl.Width = 300d; ordPnl.Name = "op1";
            ordPnl.SetValue(Canvas.LeftProperty, 50d);
            ordPnl.SetValue(Canvas.TopProperty, 20d);
            OrderPanelHeader hdr = new OrderPanelHeader();
            hdr.tbWaiter.Text = "qewrqwer werweqr wqerwer dfgsdf sdfg sdfg sdfg hjyuiyui";
            ordPnl.SetHeader(hdr);
            // блюда
            ordPnl.AddDish(new DishPanel(1, 1, "блюдо 1", 1));
            ordPnl.AddDish(new DishPanel(2, 1, "блюдо 2", 2));
            ordPnl.AddDish(new DishPanel(3, 2, "блюдо 3", 0.5m));
            ordPnl.AddDish(new DishPanel(4, 2, "блюдо 4", 1));

            cnvOrders.Children.Add(ordPnl);

            ordPnl = new OrderPanel();
            ordPnl.Width = 300d; ordPnl.Name = "op2";
            ordPnl.SetValue(Canvas.LeftProperty, 500d);
            ordPnl.SetValue(Canvas.TopProperty, 400d);
            hdr = new OrderPanelHeader();
            hdr.tbWaiter.Text = "qewrqwer werweqr";
            ordPnl.SetHeader(hdr);
            // блюда
            ordPnl.AddDish(new DishPanel(1, 1, "суп", 1));
            ordPnl.AddDish(new DishPanel(2, 1, "лапша с курицей", 2));
            ordPnl.AddDish(new DishPanel(3, 2, "пиво", 0.5m));

            cnvOrders.Children.Add(ordPnl);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            //reArrangeChildrens();
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
                List<OrderModel> orders = _dataProvider.GetOrders();
                if (orders != null)
                {
                    //OrderModel om = orders[0];
                    //string s = string.Format("id: {0}; Number {1}; hallName {2}; dishes count: {3}", om.Id, om.Number, om.HallName, om.Dishes.Count);
                    //Debug.Print(s);
                    //updateOrders(orders);

                }
            }
            catch (Exception ex)
            {
                AppLib.WriteLogErrorMessage("Ошибка чтения заказов: {0}", ex.Message);
            }
            finally
            {
                _timer.Start();
            }
        }  // method


        // обновить внутреннюю коллекцию заказов данными, полученными от сервиса
        private void updateOrders()
        {
            //foreach (OrderModel svcOrder in orders)
            //{
            //    OrderViewModel ovm = _viewOrders.FirstOrDefault(o => o.Id == svcOrder.Id);
            //    if (ovm == null)
            //    {
            //        // добавление заказа в словарь
            //        _viewOrders.Add(new OrderViewModel(svcOrder));
            //    }
            //    else
            //    {
            //        // обновить существующий заказ
            //        //curOrder = _orders[dbOrder.Id];
            //        //curOrder.UpdateFromDBEntity(dbOrder);
            //    }
            //}
            // ключи для удаления
            //IEnumerable<int> delKeys = _orders.Keys.Except(dbOrders.Select(o => o.Id));
            //foreach (int key in delKeys) _orders.Remove(key);
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            reArrangeChildrens();
        }

        private void reArrangeChildrens()
        {
            double dTop = 0d;
            foreach (UIElement child in cnvOrders.Children.OfType<OrderPanel>())
            {
                Size size = child.DesiredSize;
                if (size.Height > 0)
                {
                    if (dTop == 0d)
                    {
                        dTop = Convert.ToDouble(child.GetValue(Canvas.TopProperty));
                        dTop += child.RenderSize.Height;
                    }
                    else
                    {
                        dTop += 5d;
                        child.SetValue(Canvas.TopProperty, dTop);
                        child.SetValue(Canvas.LeftProperty, 50d);

                        if ((child is FrameworkElement) && ((FrameworkElement)child).Name == "op2")
                            child.SetValue(Canvas.LeftProperty, (_isUpdateLayout) ? 50d : 500d);

                        dTop += child.RenderSize.Height;
                    }
                }
            }
            _isUpdateLayout = !_isUpdateLayout;
        }

        private void updateViewOrders()
        {
            //cnvOrders.Visibility = Visibility.Hidden;
            // очистить панели заказов
            _pages.Clear();

            foreach (TestData.OrderTestModel ord in _viewOrders)
            {
                _pages.AddOrder(ord);
            }

            this.vbxOrders.Child = _pages.CurrentPage;
        }  // method

    }  // class MainWindow
}
