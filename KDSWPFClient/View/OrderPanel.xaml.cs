﻿using KDSWPFClient.Lib;
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

        public int PageIndex { get { return _pageIndex; } }

        // ctor
        public OrderPanel(OrderViewModel orderView, int pageIndex, double width)
        {
            InitializeComponent();

            _pageIndex = pageIndex; base.Width = width;

            orderView.ViewPanel = this;
            // создать заголовок заказа
            OrderPanelHeader hdrPnl = new OrderPanelHeader(orderView);
            // и добавить его к заказу
            this.grdHeader.Children.Add(hdrPnl);

            double fontScale = (double)AppLib.GetAppGlobalValue("AppFontScale");
            double fontSize = (double)AppLib.GetAppGlobalValue("ordPnlDishTblHeaderFontSize"); // 10d
            _fontSize = fontSize * fontScale;

            IEnumerable<TextBlock> tbs = grdTblHeader.Children.OfType<TextBlock>();
            foreach (TextBlock tb in tbs)
            {
                tb.FontSize = _fontSize;
            }

        }


        public void AddDish(DishPanel dishPanel)
        {
            this.stkDishes.Children.Add(dishPanel);
        }


    }  // class
}
