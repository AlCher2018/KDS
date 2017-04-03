using KDS.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
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
    /// Interaction logic for OrderPanel.xaml
    /// </summary>
    public partial class OrderPanel : UserControl
    {
        // ширину панели блюда будем устанавливать извне, возможно брать из конфига.
        #region simple properties
        private Timer _timer;
        private TimeSpan _orderSpentTime = TimeSpan.Zero;
        #endregion


        // ctor
        public OrderPanel()
        {
            InitializeComponent();

            _timer = new Timer(1000);
            _timer.Elapsed += _timer_Elapsed;
            _timer.Start();
        }
        ~OrderPanel()
        {
            _timer.Close(); _timer.Dispose(); _timer = null;
        }

        private void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {
                _orderSpentTime = _orderSpentTime.Add(TimeSpan.FromSeconds(1d));
                this.tbOrderCookingCounter.Text = _orderSpentTime.ToString(@"hh\:mm\:ss");

                if (_orderSpentTime.CompareTo(TimeSpan.FromSeconds(10)) == 0) _timer.Stop();
            });
        }

        // static ctor for depend.prop.
        static OrderPanel()
        {
            // заказ для режима проектирования
            ViewOrder dsnOrder = new ViewOrder()
            {
                Id = 1, HallName = "Hall 1",  TableName = "table_01", DateCreate = DateTime.Now,
                OrderStatusId = OrderStatusEnum.Wait,  Garson = "Алина"
            };
            ViewOrderProperty = DependencyProperty.Register("ViewOrder", typeof(ViewOrder), typeof(OrderPanel), new PropertyMetadata(null));

            PanelWidthProperty = DependencyProperty.Register("PanelWidth", typeof(double), typeof(OrderPanel), new PropertyMetadata(320d));
            HeaderBackground12Property = DependencyProperty.Register("HeaderBackground12", typeof(Brush), typeof(OrderPanel), new PropertyMetadata(new SolidColorBrush(Colors.Green)));
            HeaderBackground3Property = DependencyProperty.Register("HeaderBackground3", typeof(Brush), typeof(OrderPanel), new PropertyMetadata(new SolidColorBrush(Colors.White)));
            OrderCookingCounterBackgroundProperty = DependencyProperty.Register("OrderCookingCounterBackground", typeof(Brush), typeof(OrderPanel), new PropertyMetadata(new SolidColorBrush(Colors.YellowGreen)));
            HeaderForeground12Property = DependencyProperty.Register("HeaderForeground12", typeof(Brush), typeof(OrderPanel), new PropertyMetadata(new SolidColorBrush(Colors.White)));
            HeaderForeground3Property = DependencyProperty.Register("HeaderForeground3", typeof(Brush), typeof(OrderPanel), new PropertyMetadata(new SolidColorBrush(Colors.Black)));

        }

        #region dependency properties
        public static readonly DependencyProperty ViewOrderProperty;
        public ViewOrder ViewOrder
        {
            get { return (ViewOrder)GetValue(ViewOrderProperty); }
            set { SetValue(ViewOrderProperty, value); }
        }

        public static readonly DependencyProperty PanelWidthProperty;
        public double PanelWidth
        {
            get { return (double)GetValue(PanelWidthProperty); }
            set { SetValue(PanelWidthProperty, value); }
        }
        // фон заголовка для строк 1 и 2
        public static readonly DependencyProperty HeaderBackground12Property;
        public Brush HeaderBackground12
        {
            get { return (Brush)GetValue(HeaderBackground12Property); }
            set { SetValue(HeaderBackground12Property, value); }
        }
        // фон заголовка для строки 3
        public static readonly DependencyProperty HeaderBackground3Property;
        public Brush HeaderBackground3
        {
            get { return (Brush)GetValue(HeaderBackground3Property); }
            set { SetValue(HeaderBackground3Property, value); }
        }
        // фон счетчика приготовления Заказа
        public static readonly DependencyProperty OrderCookingCounterBackgroundProperty;
        public Brush OrderCookingCounterBackground
        {
            get { return (Brush)GetValue(OrderCookingCounterBackgroundProperty); }
            set { SetValue(OrderCookingCounterBackgroundProperty, value); }
        }

        // цвет текста для строк 1, 2
        public static readonly DependencyProperty HeaderForeground12Property;
        public Brush HeaderForeground12
        {
            get { return (Brush)GetValue(HeaderForeground12Property); }
            set { SetValue(HeaderForeground12Property, value); }
        }
        // цвет текста для строки 3
        public static readonly DependencyProperty HeaderForeground3Property;
        public Brush HeaderForeground3
        {
            get { return (Brush)GetValue(HeaderForeground3Property); }
            set { SetValue(HeaderForeground3Property, value); }
        }


        #endregion


    }
}
