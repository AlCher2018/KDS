﻿using KDSWPFClient.Lib;
using KDSWPFClient.ViewModel;
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

        #endregion



        public OrderPanelHeader(OrderViewModel order)
        {
            InitializeComponent();
            
            grdHeader.DataContext = order;

            double fontScale = (double)AppLib.GetAppGlobalValue("AppFontScale");

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
        }

        private void root_MouseUp(object sender, MouseButtonEventArgs e)
        {
            OrderViewModel orderView = (OrderViewModel)grdHeader.DataContext;
            MessageBox.Show(string.Format("order id {0}, number {1}, state {2}", orderView.Id, orderView.Number, orderView.Status));

        }
    }// class 
}
