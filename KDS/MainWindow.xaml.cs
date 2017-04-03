using KDS.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace KDS
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<ViewOrder> _viewOrders;

        public MainWindow()
        {
            InitializeComponent();

            _viewOrders = new List<ViewOrder>();

            LoadOrders();
        }

        private void LoadOrders()
        {
            getViewOrders();

            OrderPanel op1 = new OrderPanel();
            op1.ViewOrder = _viewOrders[0];
            op1.SetValue(Canvas.LeftProperty, 20d); op1.SetValue(Canvas.TopProperty, 20d);
            ordersPanel.Children.Add(op1);

            OrderPanel op2 = new OrderPanel();
            op2.ViewOrder = _viewOrders[1];
            op2.SetValue(Canvas.LeftProperty, 420d); op2.SetValue(Canvas.TopProperty, 20d);
            ordersPanel.Children.Add(op2);

        }

        private void getViewOrders()
        {
            _viewOrders.Add(new ViewOrder() { Id=1, HallName="Hall 1", TableName="table_01", DateCreate= DateTime.Now,
                OrderStatusId = OrderStatusEnum.Wait, Garson = "Алина"
            });
            _viewOrders.Add(new ViewOrder()
            {
                Id = 2,
                HallName = "Hall 1",
                TableName = "table_02",
                DateCreate = DateTime.Now,
                OrderStatusId = OrderStatusEnum.Wait,
                Garson = "Официант 1"
            });
            _viewOrders.Add(new ViewOrder()
            {
                Id = 3,
                HallName = "Hall 1",
                TableName = "table_03",
                DateCreate = DateTime.Now,
                OrderStatusId = OrderStatusEnum.InProcess,
                Garson = "Orderman"
            });

        }
    }  // class MainWindow
}
