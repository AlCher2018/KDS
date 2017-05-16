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
        private static Timer _timer;
        private static AppDataProvider _dataProvider;

        private List<OrderViewModel> _viewOrders;

        public MainWindow()
        {
            InitializeComponent();

            this.Closing += MainWindow_Closing;

            _viewOrders = new List<OrderViewModel>();

            List<TestData.OrderTestModel> orders = TestData.TestDataHelper.GetTestOrders(5, 10);

            OrderPanelHeader hdr = new OrderPanelHeader();
            hdr.SetValue(Canvas.LeftProperty, 50d);
            hdr.SetValue(Canvas.TopProperty, 20d);
            ordersPanel.Children.Add(hdr);

            //_timer = new Timer(1000);
            //_timer.Elapsed += _timer_Elapsed;
            //_timer.Start();
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
                    updateOrders(orders);

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
        private void updateOrders(List<OrderModel> orders)
        {
            foreach (OrderModel svcOrder in orders)
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
            // ключи для удаления
            //IEnumerable<int> delKeys = _orders.Keys.Except(dbOrders.Select(o => o.Id));
            //foreach (int key in delKeys) _orders.Remove(key);
        }

        private void LoadOrders()
        {
            //getViewOrders();

            //OrderPanel op1 = new OrderPanel();
            //op1.ViewOrder = _viewOrders[0];
            //op1.SetValue(Canvas.LeftProperty, 20d); op1.SetValue(Canvas.TopProperty, 20d);
            //ordersPanel.Children.Add(op1);

            //OrderPanel op2 = new OrderPanel();
            //op2.ViewOrder = _viewOrders[1];
            //op2.SetValue(Canvas.LeftProperty, 420d); op2.SetValue(Canvas.TopProperty, 20d);
            //ordersPanel.Children.Add(op2);

        }

        private void getViewOrders()
        {
            //_viewOrders.Add(new ViewOrder() { Id=1, HallName="Hall 1", TableName="table_01", DateCreate= DateTime.Now,
            //    OrderStatusId = OrderStatusEnum.Wait, Garson = "Алина"
            //});
            //_viewOrders.Add(new ViewOrder()
            //{
            //    Id = 2,
            //    HallName = "Hall 1",
            //    TableName = "table_02",
            //    DateCreate = DateTime.Now,
            //    OrderStatusId = OrderStatusEnum.Wait,
            //    Garson = "Официант 1"
            //});
            //_viewOrders.Add(new ViewOrder()
            //{
            //    Id = 3,
            //    HallName = "Hall 1",
            //    TableName = "table_03",
            //    DateCreate = DateTime.Now,
            //    OrderStatusId = OrderStatusEnum.InProcess,
            //    Garson = "Orderman"
            //});

        }
    }  // class MainWindow
}
