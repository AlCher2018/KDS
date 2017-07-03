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

        public int Lines { get { return this.stkDishes.Children.Count; } }

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

            //if (!orderView.DivisionColorRGB.IsNull())
            //{
            //    brdOrder.BorderBrush = AppLib.GetBrushFromRGBString(orderView.DivisionColorRGB);
            //    brdOrder.BorderThickness = new Thickness(10d);
            //    brdOrder.CornerRadius = (isCreateHeaderPanel) ? new CornerRadius(15d,15d,0,0) : new CornerRadius(0);
            //}
        }


        // добавить блюдо в панель заказа и измерить высоту строки блюда
        public void AddDish(DishPanel dishPanel)
        {
            this.stkDishes.Children.Add(dishPanel);

            //  update DesiredSize
            dishPanel.Measure(new Size(base.Width, double.PositiveInfinity));
            //grdHeader.UpdateLayout();
            stkDishes.UpdateLayout();
        }

        // добавить массив элементов в стек БЕЗ измерения высоты
        internal void AddDish(UIElement[] delItems)
        {
            foreach (UIElement item in delItems)
            {
                this.stkDishes.Children.Add(item);
            }
        }

        // для удаляемого блюда (при переносе в следующую колонку), создать массив UI-элементов, которые будут
        // переноситься в следующую колонку для предотвращения "висячих" разделителей номера подачи и ингредиентов
        internal UIElement[] RemoveDish(DishPanel dishPanel)
        {
            List<UIElement> retVal = new List<UIElement>();
            retVal.Add(dishPanel);
            this.stkDishes.Children.Remove(dishPanel);

            string parentUid = dishPanel.DishView.ParentUID;  // если не пусто, то содержит значение родительского Uid

            int idx = stkDishes.Children.IndexOf(dishPanel);
            idx = stkDishes.Children.Count - 1;
            UIElement uiElem;
            // сохраняем в массиве разделитель подач или все ингредиенты вместе с блюдом
            for (int i = idx; i >= 0; i--)
            {
                uiElem = stkDishes.Children[i];
                if ((uiElem is DishDelimeterPanel)
                    || ((uiElem is DishPanel) && !parentUid.IsNull() 
                        && ((uiElem as DishPanel).DishView.UID == parentUid)))
                {
                    retVal.Add(uiElem);
                    this.stkDishes.Children.Remove(uiElem);
                }
                else
                    break;
            }
            if (retVal.Count > 1) retVal.Reverse();

            return retVal.ToArray();
        }

        public void AddDelimiter(DishDelimeterPanel delimPanel)
        {
            this.stkDishes.Children.Add(delimPanel);

            //  update DesiredSize
            delimPanel.Measure(new Size(base.Width, double.PositiveInfinity));
            //grdHeader.UpdateLayout();
            stkDishes.UpdateLayout();
        }

    }  // class
}
