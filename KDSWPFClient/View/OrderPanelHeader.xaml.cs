using KDSWPFClient.Lib;
using KDSWPFClient.ViewModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        #endregion

        private Brush _divisionMarkBrush = null;


        public OrderPanelHeader(OrderViewModel order)
        {
            InitializeComponent();
            this.Loaded += OrderPanelHeader_Loaded;

            grdHeader.DataContext = order;

            double fontScale = AppLib.GetAppSetting("AppFontScale").ToDouble();

            double fSize = fontScale * (double)AppLib.GetAppGlobalValue("ordPnlHdrLabelFontSize");  // 12d
            tbTableLabel1.FontSize = fSize;
            tbTableLabel2.FontSize = fSize;
            tbOrderDateLabel.FontSize = fSize;
            tbOrderCookingCounterLabel.FontSize = fSize;

            tbTableName.FontSize = fontScale * (double)AppLib.GetAppGlobalValue("ordPnlHdrTableNameFontSize");  // 14d
            tbOrderNumber.FontSize = fontScale * (double)AppLib.GetAppGlobalValue("ordPnlHdrOrderNumberFontSize");  // 14d
            tbWaiter.FontSize = fontScale * (double)AppLib.GetAppGlobalValue("ordPnlHdrWaiterNameFontSize");  // 12d
            tbOrderDate.FontSize = tbTableName.FontSize;

            tbOrderCookingCounter.FontSize = fontScale * (double)AppLib.GetAppGlobalValue("ordPnlHdrOrderTimerFontSize");  // 12d

            if (!order.DivisionColorRGB.IsNull())
            {
                brdDivisionMark.Fill = AppLib.GetBrushFromRGBString(order.DivisionColorRGB);
            }

        }

        private void OrderPanelHeader_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void root_MouseUp(object sender, MouseButtonEventArgs e)
        {
            // 1. настройка в config-файле для заголовка заказа
            if ((bool)AppLib.GetAppGlobalValue("OrderHeaderClickable", false) == false) return;

            OrderViewModel orderView = (OrderViewModel)grdHeader.DataContext;

            StateChange win = new StateChange() { Order = orderView, Dish = null };
            AppLib.SetWinSizeToMainWinSize(win);

            win.ShowDialog();
            e.Handled = true;
        }
    }// class 
}
