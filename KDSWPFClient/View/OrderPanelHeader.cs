using KDSWPFClient.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Input;
using KDSWPFClient.Lib;
using IntegraLib;

namespace KDSWPFClient.View
{
#if notUserControl
    public class OrderPanelHeader: Grid
    {
        #region fields & properties
        OrderViewModel _order;

        #endregion

        public OrderPanelHeader(OrderViewModel order, double width)
        {
            _order = order;
            this.Width = width;
            this.SnapsToDevicePixels = true;

            Binding binding;
            BrushesPair brPair;
            // кисть заголовка
            BrushesPair brPairHeader = getHeaderBrushes();
            // отступы
            Thickness rowMargin = new Thickness(0.02 * width, 0, 0.02 * width, 0);
            // шрифты
            double fontScale = (double)WpfHelper.GetAppGlobalValue("AppFontScale", 1.0d);
            double labelFontSize = fontScale * (double)WpfHelper.GetAppGlobalValue("ordPnlHdrLabelFontSize");


            // 0. номер стола и заказа
            this.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1.0d, GridUnitType.Star) });
            // 1. официант
            this.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(0.7d, GridUnitType.Star) });
            // 2. дата создания заказа и таймер заказа
            this.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1.0d, GridUnitType.Star) });

            // 0. номер стола и заказа
            Border brdHdrTableRow = new Border() { BorderBrush = Brushes.DarkBlue};
            brdHdrTableRow.SetValue(Grid.RowProperty, 0);
            brdHdrTableRow.BorderThickness = new Thickness(1, 1, 1, 0);
            if (brPairHeader != null)
            {
                brdHdrTableRow.Background = brPairHeader.Background;
                brdHdrTableRow.SetValue(TextBlock.ForegroundProperty, brPairHeader.Foreground);
            }
            // уголки рамок
            brdHdrTableRow.CornerRadius = new CornerRadius(0.03 * width, 0.03 * width, 0, 0);

            Grid grdHdrTableRow = new Grid();
            grdHdrTableRow.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(0, GridUnitType.Auto) });
            grdHdrTableRow.ColumnDefinitions.Add(new ColumnDefinition());
            //  стол
            TextBlock tblTable = new TextBlock() { VerticalAlignment = VerticalAlignment.Center, Margin = rowMargin };
            tblTable.SetValue(Grid.ColumnProperty, 0);
            Run tbTableLabel1 = new Run() { Text= "Стол №: ", FontSize = labelFontSize };
            tblTable.Inlines.Add(tbTableLabel1);
            Run tbTableName = new Run() { FontWeight = FontWeights.Bold };
            tbTableName.FontSize = fontScale * (double)WpfHelper.GetAppGlobalValue("ordPnlHdrTableNameFontSize");
            binding = new Binding("TableName") { Source = _order};
            tbTableName.SetBinding(Run.TextProperty, binding);
            tblTable.Inlines.Add(tbTableName);
            grdHdrTableRow.Children.Add(tblTable);
            //  номер заказа
            Border brdOrderNumber = new Border();
            brdOrderNumber.SetValue(Grid.ColumnProperty, 1);
            TextBlock tblOrderNumber = new TextBlock() { HorizontalAlignment= HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = rowMargin };
            Run tbTableLabel2 = new Run() { Text= "Заказ №: ", FontSize = labelFontSize };
            tblOrderNumber.Inlines.Add(tbTableLabel2);
            Run tbOrderNumber = new Run() { FontWeight = FontWeights.Bold };
            tbOrderNumber.FontSize = fontScale * (double)WpfHelper.GetAppGlobalValue("ordPnlHdrOrderNumberFontSize");
            binding = new Binding("Number") { Source = _order};
            tbOrderNumber.SetBinding(Run.TextProperty, binding);
            tblOrderNumber.Inlines.Add(tbOrderNumber);
            brdOrderNumber.Child = tblOrderNumber;
            grdHdrTableRow.Children.Add(brdOrderNumber);

            brdHdrTableRow.Child = grdHdrTableRow;
            this.Children.Add(brdHdrTableRow);

            // 1. официант
            Border brdHdrWaiter = new Border() { BorderBrush = Brushes.DarkBlue, BorderThickness = new Thickness(1, 0, 1, 0) };
            brdHdrWaiter.SetValue(Grid.RowProperty, 1);
            if (brPairHeader != null)
            {
                brdHdrWaiter.Background = brPairHeader.Background;
                brdHdrWaiter.SetValue(TextBlock.ForegroundProperty, brPairHeader.Foreground);
            }
            TextBlock tbWaiter = new TextBlock()
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                TextWrapping = TextWrapping.Wrap,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0.02 * width, 0, 0.02 * width, 0)
            };
            tbWaiter.FontSize = fontScale * (double)WpfHelper.GetAppGlobalValue("ordPnlHdrWaiterNameFontSize");
            binding = new Binding("Waiter") {Source = _order };
            tbWaiter.SetBinding(TextBlock.TextProperty, binding);
            brdHdrWaiter.Child = tbWaiter;
            this.Children.Add(brdHdrWaiter);

            // метка отдела
            Viewbox vbxDivisionMark = new Viewbox() { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Stretch = Stretch.Fill, ClipToBounds = true};
            vbxDivisionMark.Width = 0.2d * width;
            vbxDivisionMark.SetValue(Grid.ColumnProperty, 1);
            vbxDivisionMark.SetValue(Grid.RowProperty, 0);
            vbxDivisionMark.SetValue(Grid.RowSpanProperty, 2);
            Polygon divisionMark = new Polygon()
            {
                Opacity = 0.7,
                Points = new PointCollection(new Point[] { new Point(0, 0), new Point(10, 0), new Point(10, 10) })
            };
            if (!_order.DivisionColorRGB.IsNull()) divisionMark.Fill = WpfHelper.GetBrushFromRGBString(order.DivisionColorRGB);
            vbxDivisionMark.Child = divisionMark;
            this.Children.Add(vbxDivisionMark);

            // 2. дата создания заказа и таймер заказа
            Border brdHdrOrderTime = new Border() { BorderBrush = Brushes.DarkBlue, BorderThickness = new Thickness(1.0d)};
            brdHdrOrderTime.SetValue(Grid.RowProperty, 2);
            if (brPairHeader != null)
            {
                brdHdrOrderTime.Background = brPairHeader.Background;
                brdHdrOrderTime.SetValue(TextBlock.ForegroundProperty, brPairHeader.Foreground);
            }

            Grid grdHdrOrderTime = new Grid() { Margin = rowMargin };
            grdHdrOrderTime.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(4.0d, GridUnitType.Star)});
            grdHdrOrderTime.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(4.0d, GridUnitType.Star) });
            //  дата создания заказа
            WrapPanel pnlOrderDate = new WrapPanel() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left};
            pnlOrderDate.SetValue(Grid.ColumnProperty, 0);
            TextBlock tbOrderDateLabel = new TextBlock() { Text = "Создан в: ", FontSize = labelFontSize, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left};
            pnlOrderDate.Children.Add(tbOrderDateLabel);
            TextBlock tbOrderDate = new TextBlock() { FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap};
            tbOrderDate.FontSize = fontScale * (double)WpfHelper.GetAppGlobalValue("ordPnlHdrOrderCreateDateFontSize");
            binding = new Binding("CreateDate") { Source = _order, Converter = new ViewDateConverter() };
            tbOrderDate.SetBinding(TextBlock.TextProperty, binding);
            pnlOrderDate.Children.Add(tbOrderDate);
            grdHdrOrderTime.Children.Add(pnlOrderDate);
            //  таймер
            Border brdOrderTimer = new Border() { Padding = new Thickness(5d, 3d, 5d, 3d), Margin = new Thickness(0,3d,0,3d)};
            brdOrderTimer.SetValue(Grid.ColumnProperty, 1);
            //  уголки
            double timerCornerRadius = 0.025 * width;
            brdOrderTimer.CornerRadius = new CornerRadius(timerCornerRadius, timerCornerRadius, timerCornerRadius, timerCornerRadius);
            brPair = BrushHelper.AppBrushes["orderHeaderTimer"];
            if (brPair != null)
            {
                brdOrderTimer.Background = brPair.Background;
                brdOrderTimer.SetValue(TextBlock.ForegroundProperty, brPair.Foreground);
            }
            WrapPanel pnlOrderTimer = new WrapPanel() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            TextBlock tbOrderTimerLabel = new TextBlock() { Text="Прошло: ", FontSize = labelFontSize, FontStretch = FontStretches.Condensed};
            pnlOrderTimer.Children.Add(tbOrderTimerLabel);
            TextBlock tbOrderTimer = new TextBlock() { FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap};
            tbOrderTimer.FontSize = fontScale * (double)WpfHelper.GetAppGlobalValue("ordPnlHdrOrderTimerFontSize");
            binding = new Binding("WaitingTimerString") { Source = _order};
            tbOrderTimer.SetBinding(TextBlock.TextProperty, binding);
            pnlOrderTimer.Children.Add(tbOrderTimer);
            brdOrderTimer.Child = pnlOrderTimer;
            grdHdrOrderTime.Children.Add(brdOrderTimer);

            brdHdrOrderTime.Child = grdHdrOrderTime;
            this.Children.Add(brdHdrOrderTime);
        }

        private BrushesPair getHeaderBrushes()
        {
            StatusEnum status1 = _order.Status, status2 = _order.StatusAllowedDishes;
            string key = null;
            if (((bool)WpfHelper.GetAppGlobalValue("IsShowOrderStatusByAllShownDishes"))
                && (status2 != StatusEnum.None) && (status2 != StatusEnum.WaitingCook) && (status2 != status1))
                key = status2.ToString();
            else
                key = status1.ToString();

            BrushesPair retVal = null;
            if (!key.IsNull() && BrushHelper.AppBrushes.ContainsKey(key)) retVal = BrushHelper.AppBrushes[key];

            return retVal;
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

            AppLib.WriteLogClientAction("{0} - open StateChange window for orderId {1} (№ {2})", sLogMsg, _order.Id, _order.Number);

            App.OpenStateChangeWindow(_order, null);

            e.Handled = true;
        }


    }  // class OrderPanelHeader
#endif
}
