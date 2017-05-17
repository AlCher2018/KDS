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

namespace KDSWPFClient.View
{
    /// <summary>
    /// Interaction logic for OrderPanelHeader.xaml
    /// </summary>
    public partial class OrderPanelHeader : UserControl
    {
        #region dependency properties
        public static readonly DependencyProperty PanelWidthProperty = DependencyProperty.Register("PanelWidth", typeof(double), typeof(OrderPanelHeader), new PropertyMetadata(300d));
        public double PanelWidth
        {
            get { return (double)GetValue(PanelWidthProperty); }
            set { SetValue(PanelWidthProperty, value); }
        }

        // фон заголовка для строк 1 и 2
        public static readonly DependencyProperty HeaderBackground12Property = DependencyProperty.Register("HeaderBackground12", typeof(Brush), typeof(OrderPanelHeader), new PropertyMetadata(new SolidColorBrush(Colors.Green)));
        public Brush HeaderBackground12
        {
            get { return (Brush)GetValue(HeaderBackground12Property); }
            set { SetValue(HeaderBackground12Property, value); }
        }
        // фон заголовка для строки 3
        public static readonly DependencyProperty HeaderBackground3Property = DependencyProperty.Register("HeaderBackground3", typeof(Brush), typeof(OrderPanelHeader), new PropertyMetadata(new SolidColorBrush(Colors.White)));
        public Brush HeaderBackground3
        {
            get { return (Brush)GetValue(HeaderBackground3Property); }
            set { SetValue(HeaderBackground3Property, value); }
        }
        // фон счетчика приготовления Заказа
        public static readonly DependencyProperty OrderStatusTSBackgroundProperty = DependencyProperty.Register("OrderStatusTSBackground", typeof(Brush), typeof(OrderPanelHeader), new PropertyMetadata(new SolidColorBrush(Colors.YellowGreen)));
        public Brush OrderStatusTSBackground
        {
            get { return (Brush)GetValue(OrderStatusTSBackgroundProperty); }
            set { SetValue(OrderStatusTSBackgroundProperty, value); }
        }

        // цвет текста для строк 1, 2
        public static readonly DependencyProperty HeaderForeground12Property = DependencyProperty.Register("HeaderForeground12", typeof(Brush), typeof(OrderPanelHeader), new PropertyMetadata(new SolidColorBrush(Colors.White)));
        public Brush HeaderForeground12
        {
            get { return (Brush)GetValue(HeaderForeground12Property); }
            set { SetValue(HeaderForeground12Property, value); }
        }
        // цвет текста для строки 3
        public static readonly DependencyProperty HeaderForeground3Property = DependencyProperty.Register("HeaderForeground3", typeof(Brush), typeof(OrderPanelHeader), new PropertyMetadata(new SolidColorBrush(Colors.Black)));
        public Brush HeaderForeground3
        {
            get { return (Brush)GetValue(HeaderForeground3Property); }
            set { SetValue(HeaderForeground3Property, value); }
        }

        // Using a DependencyProperty as the backing store for TableName.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TableNameProperty =
            DependencyProperty.Register("TableName", typeof(string), typeof(OrderPanelHeader), new PropertyMetadata(null));
        public string TableName
        {
            get { return (string)GetValue(TableNameProperty); }
            set { SetValue(TableNameProperty, value); }
        }

        public string OrderNumber
        {
            get { return (string)GetValue(OrderNumberProperty); }
            set { SetValue(OrderNumberProperty, value); }
        }
        // Using a DependencyProperty as the backing store for OrderNumber.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty OrderNumberProperty =
            DependencyProperty.Register("OrderNumber", typeof(string), typeof(OrderPanelHeader), new PropertyMetadata(""));

        public string WaiterName
        {
            get { return (string)GetValue(WaiterNameProperty); }
            set { SetValue(WaiterNameProperty, value); }
        }
        // Using a DependencyProperty as the backing store for WaiterName.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty WaiterNameProperty =
            DependencyProperty.Register("WaiterName", typeof(string), typeof(OrderPanelHeader), new PropertyMetadata(""));


        public DateTime CreateDate
        {
            get { return (DateTime)GetValue(CreateDateProperty); }
            set { SetValue(CreateDateProperty, value); }
        }

        // Using a DependencyProperty as the backing store for CreateDate.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CreateDateProperty =
            DependencyProperty.Register("CreateDate", typeof(DateTime), typeof(OrderPanelHeader), new PropertyMetadata(DateTime.MinValue));

        #endregion



        public OrderPanelHeader()
        {
            InitializeComponent();
        }

    }// class 
}
