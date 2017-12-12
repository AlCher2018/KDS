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
#if notUserControl == false
    /// <summary>
    /// Interaction logic for OrderPanelHeader.xaml
    /// </summary>
    public partial class OrderPanelHeader : UserControl
    {
        public OrderPanelHeader(OrderViewModel order, double width)
        {
            InitializeComponent();
            
            this.MouseUp += root_MouseUp;
            grdHeader.DataContext = order;

            // стили и кисти
            BrushesPair brPair;
            StatusEnum status1 = order.Status, status2 = order.StatusAllowedDishes;
            string key = null;
            if (((bool)WpfHelper.GetAppGlobalValue("IsShowOrderStatusByAllShownDishes"))
                && (status2 != StatusEnum.None) && (status2 != StatusEnum.WaitingCook) && (status2 != status1))
                key = status2.ToString();
            else
                key = status1.ToString();
            if (!key.IsNull() && BrushHelper.AppBrushes.ContainsKey(key))
            {
                brPair = BrushHelper.AppBrushes[key];
                brdHrdTableRow.Background = brPair.Background;
                brdHrdTableRow.SetValue(TextBlock.ForegroundProperty, brPair.Foreground);
                brdHdrWaiter.Background = brPair.Background;
                brdHdrWaiter.SetValue(TextBlock.ForegroundProperty, brPair.Foreground);
                brdHdrOrderTime.Background = brPair.Background;
                brdHdrOrderTime.SetValue(TextBlock.ForegroundProperty, brPair.Foreground);
            }
            // уголки рамок
            brdHrdTableRow.CornerRadius = new CornerRadius(0.03 * width, 0.03 * width, 0, 0);
            // отступы
            Thickness rowMargin = new Thickness(0.02 * width, 0, 0.02 * width, 0);
            tblTable.Margin = rowMargin;
            tblOrderNumber.Margin = rowMargin;
            tbWaiter.Margin = rowMargin;
            grdHdrOrderTime.Margin = rowMargin;

            // таймер
            double timerCornerRadius = 0.025 * width;
            brdOrderTimer.CornerRadius = new CornerRadius(timerCornerRadius, timerCornerRadius, timerCornerRadius, timerCornerRadius);
            brPair = BrushHelper.AppBrushes["orderHeaderTimer"];
            if (brPair != null)
            {
                brdOrderTimer.Background = brPair.Background;
                brdOrderTimer.SetValue(TextBlock.ForegroundProperty, brPair.Foreground);
            }

            // шрифты
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
#endif
}
