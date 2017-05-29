using KDSWPFClient.Lib;
using System;
using System.Timers;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KDSWPFClient.ViewModel;

namespace KDSWPFClient.View
{
    /// <summary>
    /// Interaction logic for OrderPanel.xaml
    /// </summary>
    public partial class OrderPanel : UserControl
    {
        private double _fontSize;
        private int _pageIndex;
        private OrderViewModel _orderView;

        public OrderViewModel OrderViewModel { get { return _orderView; } }

        public int PageIndex { get { return _pageIndex; } }

        // ctor
        public OrderPanel(OrderViewModel orderView, int pageIndex, double width, bool isCreateHeaderPanel)
        {
            InitializeComponent();

            _pageIndex = pageIndex; base.Width = width;

            _orderView = orderView;
            orderView.ViewPanel = this;

            if (isCreateHeaderPanel)
            {
                // создать заголовок заказа
                OrderPanelHeader hdrPnl = new OrderPanelHeader(_orderView);
                // и добавить его к заказу
                this.grdHeader.Children.Add(hdrPnl);

                hdrPnl.Measure(new Size(base.Width, double.PositiveInfinity));
//                grdHeader.Arrange(new Rect(0, 0, hdrPnl.DesiredSize.Width, hdrPnl.DesiredSize.Height));
                grdHeader.UpdateLayout();
            }

            // установить шрифт текстовых блоков в заголовке таблицы блюд
            double fontSize = (double)AppLib.GetAppGlobalValue("ordPnlDishTblHeaderFontSize"); // 10d
            double fontScale = AppLib.GetAppSetting("AppFontScale").ToDouble();
            _fontSize = fontSize * fontScale;
            IEnumerable<TextBlock> tbs = grdTblHeader.Children.OfType<TextBlock>();
            foreach (TextBlock tb in tbs)
            {
                tb.FontSize = _fontSize;
            }
            // пересчитать размер панели
            brdTblHeader.Measure(new Size(base.Width, double.PositiveInfinity));
//            grdHeader.Arrange(new Rect(0, 0, brdTblHeader.DesiredSize.Width, brdTblHeader.DesiredSize.Height));
            grdHeader.UpdateLayout();

            // пересчитать высоту панели
            this.Measure(new Size(base.Width, double.PositiveInfinity));
        }


        public void AddDish(DishPanel dishPanel)
        {
            this.stkDishes.Children.Add(dishPanel);

            //  update DesiredSize
            dishPanel.Measure(new Size(base.Width, double.PositiveInfinity));
            grdHeader.UpdateLayout();
        }

        internal void RemoveDish(DishPanel dishPanel)
        {
            this.stkDishes.Children.Remove(dishPanel);
        }

        public void AddDelimiter(DishDelimeterPanel delimPanel)
        {
            this.stkDishes.Children.Add(delimPanel);

            //  update DesiredSize
            delimPanel.Measure(new Size(base.Width, double.PositiveInfinity));
            grdHeader.UpdateLayout();
        }

    }  // class
}
