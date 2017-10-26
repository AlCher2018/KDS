using KDSWPFClient.Lib;
using System;
using System.Timers;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KDSWPFClient.ViewModel;
using System.Diagnostics;
using IntegraLib;

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
        private Size _size;

        // высота панели заказа
        public double HeightPanel { get { return this.DesiredSize.Height; } }

        public int DishPanelsCount { get { return this.stkDishes.Children.Count; } }

        public OrderViewModel OrderViewModel { get { return _orderView; } }

        public int PageIndex { get { return _pageIndex; } }

        public int Lines { get { return this.stkDishes.Children.Count; } }

        // ctor
        public OrderPanel(OrderViewModel orderView, int pageIndex, double width, bool isCreateHeaderPanel)
        {
            InitializeComponent();

            _pageIndex = pageIndex; base.Width = width;
            _size = new Size(base.Width, double.PositiveInfinity);

            _orderView = orderView;
            orderView.ViewPanel = this;

            if (isCreateHeaderPanel)
            {
                // создать заголовок заказа
                OrderPanelHeader hdrPnl = new OrderPanelHeader(_orderView, width);
                // и добавить его к заказу
                this.grdHeader.Children.Add(hdrPnl);
            }

            // установить шрифт текстовых блоков в заголовке таблицы блюд
            double fontSize = (double)AppPropsHelper.GetAppGlobalValue("ordPnlDishTblHeaderFontSize"); // 10d
            double fontScale = (double)AppPropsHelper.GetAppGlobalValue("AppFontScale", 1.0d);
            _fontSize = fontSize * fontScale;
            IEnumerable<TextBlock> tbs = grdTblHeader.Children.OfType<TextBlock>();
            foreach (TextBlock tb in tbs)
            {
                tb.FontSize = _fontSize;
            }

            // пересчитать высоту панели
//            this.Measure(_size);

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
        }

        // добавить массив элементов в стек
        internal void AddDish(UIElement[] delItems)
        {
            foreach (UIElement item in delItems)
            {
                this.stkDishes.Children.Add(item);
            }
        }

        // для удаляемого блюда (при переносе в следующую колонку), создать массив UI-элементов, которые будут
        // переноситься в следующую колонку для предотвращения "висячих" разделителей номера подачи и ингредиентов
        internal UIElement[] RemoveDish(DishPanel dishPanel, double topValue, double cnvHeight)
        {
            int idx = stkDishes.Children.IndexOf(dishPanel);
            double totalHeight = topValue + this.DesiredSize.Height;

            List<UIElement> retVal = new List<UIElement>();
            retVal.Add(dishPanel);
            this.stkDishes.Children.Remove(dishPanel);
            totalHeight -= dishPanel.DesiredSize.Height;

            // если не пусто, то это ингредиент и содержит значение родительского Uid
            string parentUid = dishPanel.DishView.ParentUID;  

            UIElement uiElem;
            bool isMove;
            // сохраняем в массиве разделитель подач или все ингредиенты вместе с блюдом
            for (int i = idx-1; i >= 0; i--)
            {
                uiElem = stkDishes.Children[i];

                // условия переноса строки заказа в следующий столбец
                // - это разделитель (номер подачи)
                isMove = (uiElem is DishDelimeterPanel);
                // - или это блюдо для переносимого ингредиента
                if ((!isMove) && (uiElem is DishPanel) && !parentUid.IsNull())
                {
                    DishPanel dsPnl = (uiElem as DishPanel);
                    isMove = (dsPnl.DishView.UID == parentUid) && dsPnl.DishView.ParentUID.IsNull(); // признак блюда
                   //((uiElem as DishPanel).DishView.ParentUID == parentUid)))
                }
                // - или выходим за рамки по вертикали
                if (!isMove && (Math.Ceiling(totalHeight) >= cnvHeight)) isMove = true;

                if (isMove)
                {
                    retVal.Add(uiElem);
                    this.stkDishes.Children.Remove(uiElem);
                    totalHeight -= uiElem.DesiredSize.Height;
                }
                else 
                    break;
            }
            if (retVal.Count > 1) retVal.Reverse();

            return retVal.ToArray();
        }

        internal void SetPosition(double top, double left)
        {
            this.SetValue(Canvas.TopProperty, top);
            this.SetValue(Canvas.LeftProperty, left);
        }

        public void AddDelimiter(DishDelimeterPanel delimPanel)
        {
            this.stkDishes.Children.Add(delimPanel);
        }

    }  // class
}
