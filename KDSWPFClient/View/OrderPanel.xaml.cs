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
        public double PanelHeight { get { return this.ActualHeight; } }
        public double HeaderHeight { get { return this.grdHeader.ActualHeight + this.brdTblHeader.ActualHeight; } }

        public UIElementCollection DishPanels { get { return this.stkDishes.Children; } }
        public int ItemsCount { get { return this.DishPanels.Count; } }

        public OrderPanelHeader HeaderPanel {
            get
            {
                return (this.grdHeader.Children.Count == 0) ? null : (OrderPanelHeader)this.grdHeader.Children[0];
            }
            set
            {
                if (value == null)
                {
                    if (this.grdHeader.Children.Count != 0) this.grdHeader.Children.Clear();
                }
                else if ((value is OrderPanelHeader) && (value.Parent == null))
                {
                    if (this.grdHeader.Children.Count == 0) this.grdHeader.Children.Add(value);
                    else this.grdHeader.Children[0] = value;
                }
            }
        }


        public OrderViewModel OrderViewModel { get { return _orderView; } }

        public int PageIndex { get { return _pageIndex; } }

        // ctor
        public OrderPanel(OrderViewModel orderView, int pageIndex, double width, bool isCreateHeaderPanel)
        {
            InitializeComponent();

            _pageIndex = pageIndex; base.Width = width;
            _size = new Size(base.Width, double.PositiveInfinity);

            _orderView = orderView;
            if (orderView != null) orderView.ViewPanel = this;

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
        internal void AddDishes(UIElement[] dishPanels)
        {
            foreach (UIElement item in dishPanels)
            {
                this.stkDishes.Children.Add(item);
            }
        }
        internal void AddDishes(List<UIElement> dishPanels)
        {
            foreach (UIElement item in dishPanels)
            {
                this.stkDishes.Children.Add(item);
            }
        }

        internal void InsertDish(int index, UIElement dishPanel)
        {
            this.stkDishes.Children.Insert(index, dishPanel);
        }
        internal void InsertDishes(int index, List<UIElement> dishPanels)
        {
            for (int i = 0; i < dishPanels.Count; i++)
            {
                this.stkDishes.Children.Insert(index, dishPanels[i]);
            }
        }

        internal void DetachDish(UIElement item)
        {
            this.stkDishes.Children.Remove(item);
        }
        internal void DetachDishes(List<UIElement> items)
        {
            items.ForEach(e => this.stkDishes.Children.Remove(e));
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
                isMove = ((uiElem is DishDelimeterPanel) && ((DishDelimeterPanel)uiElem).DontTearOffNext);

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
        public void InsertDelimiter(int index, DishDelimeterPanel delimPanel)
        {
            this.stkDishes.Children.Insert(index, delimPanel);
        }

        // отсоединить заголовок и вернуть его
        internal OrderPanelHeader DetachHeader()
        {
            if ((this.grdHeader.Children.Count > 0) && (this.grdHeader.Children[0] is OrderPanelHeader))
            {
                OrderPanelHeader retVal = (OrderPanelHeader)this.grdHeader.Children[0];
                this.grdHeader.Children.Clear();
                return retVal;
            }
            else
                return null;
        }
    }  // class
}
