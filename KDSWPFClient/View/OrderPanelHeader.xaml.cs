using IntegraLib;
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
        //public static readonly DependencyProperty PanelWidthProperty = DependencyProperty.Register("PanelWidth", typeof(double), typeof(OrderPanelHeader), new PropertyMetadata(300d));
        //public double PanelWidth
        //{
        //    get { return (double)GetValue(PanelWidthProperty); }
        //    set { SetValue(PanelWidthProperty, value); }
        //}

        //// фон заголовка для строк 1 и 2
        //public static readonly DependencyProperty HeaderBackground12Property = DependencyProperty.Register("HeaderBackground12", typeof(Brush), typeof(OrderPanelHeader), new PropertyMetadata(new SolidColorBrush(Colors.Green)));
        //public Brush HeaderBackground12
        //{
        //    get { return (Brush)GetValue(HeaderBackground12Property); }
        //    set { SetValue(HeaderBackground12Property, value); }
        //}
        //// фон заголовка для строки 3
        //public static readonly DependencyProperty HeaderBackground3Property = DependencyProperty.Register("HeaderBackground3", typeof(Brush), typeof(OrderPanelHeader), new PropertyMetadata(new SolidColorBrush(Colors.White)));
        //public Brush HeaderBackground3
        //{
        //    get { return (Brush)GetValue(HeaderBackground3Property); }
        //    set { SetValue(HeaderBackground3Property, value); }
        //}
        //// фон счетчика приготовления Заказа
        //public static readonly DependencyProperty OrderStatusTSBackgroundProperty = DependencyProperty.Register("OrderStatusTSBackground", typeof(Brush), typeof(OrderPanelHeader), new PropertyMetadata(new SolidColorBrush(Colors.YellowGreen)));
        //public Brush OrderStatusTSBackground
        //{
        //    get { return (Brush)GetValue(OrderStatusTSBackgroundProperty); }
        //    set { SetValue(OrderStatusTSBackgroundProperty, value); }
        //}

        //// цвет текста для строк 1, 2
        //public static readonly DependencyProperty HeaderForeground12Property = DependencyProperty.Register("HeaderForeground12", typeof(Brush), typeof(OrderPanelHeader), new PropertyMetadata(new SolidColorBrush(Colors.White)));
        //public Brush HeaderForeground12
        //{
        //    get { return (Brush)GetValue(HeaderForeground12Property); }
        //    set { SetValue(HeaderForeground12Property, value); }
        //}
        //// цвет текста для строки 3
        //public static readonly DependencyProperty HeaderForeground3Property = DependencyProperty.Register("HeaderForeground3", typeof(Brush), typeof(OrderPanelHeader), new PropertyMetadata(new SolidColorBrush(Colors.Black)));
        //public Brush HeaderForeground3
        //{
        //    get { return (Brush)GetValue(HeaderForeground3Property); }
        //    set { SetValue(HeaderForeground3Property, value); }
        //}

        #endregion

        public OrderPanelHeader(OrderViewModel order, double width)
        {
            InitializeComponent();

            grdHeader.DataContext = order;

            double fontScale = (double)WpfHelper.GetAppGlobalValue("AppFontScale", 1.0d);

            double fSize = fontScale * (double)WpfHelper.GetAppGlobalValue("ordPnlHdrLabelFontSize");
            tbTableLabel1.FontSize = fSize;
            tbTableLabel2.FontSize = fSize;
            tbOrderDateLabel.FontSize = fSize;
            tbOrderCookingCounterLabel.FontSize = fSize;

            tbTableName.FontSize = fontScale * (double)WpfHelper.GetAppGlobalValue("ordPnlHdrTableNameFontSize");
            tbOrderNumber.FontSize = fontScale * (double)WpfHelper.GetAppGlobalValue("ordPnlHdrOrderNumberFontSize");
            tbWaiter.FontSize = fontScale * (double)WpfHelper.GetAppGlobalValue("ordPnlHdrWaiterNameFontSize");
            tbOrderDate.FontSize = fontScale * (double)WpfHelper.GetAppGlobalValue("ordPnlHdrOrderCreateDateFontSize");
            tbOrderCookingCounter.FontSize = fontScale * (double)WpfHelper.GetAppGlobalValue("ordPnlHdrOrderTimerFontSize");

            if (!order.DivisionColorRGB.IsNull())
            {
                brdDivisionMark.Fill = WpfHelper.GetBrushFromRGBString(order.DivisionColorRGB);
            }
        }

        private void root_MouseUp(object sender, MouseButtonEventArgs e)
        {
            string sLogMsg = "click on order HEADER";

            // 1. настройка в config-файле для заголовка заказа
            if ((bool)WpfHelper.GetAppGlobalValue("OrderHeaderClickable", false) == false)
            {
                AppLib.WriteLogClientAction(sLogMsg + " - NO action (клик по заголовку не разрешен в OrderHeaderClickable)");
                return;
            }

            OrderViewModel orderView = (OrderViewModel)grdHeader.DataContext;

            AppLib.WriteLogClientAction("{0} - open StateChange window for orderId {1} (№ {2})", sLogMsg, orderView.Id, orderView.Number);

            App.OpenStateChangeWindow(orderView, null);

            e.Handled = true;
        }
    }// class 
}
