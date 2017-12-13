using IntegraLib;
using KDSWPFClient.Lib;
using KDSWPFClient.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KDSWPFClient.View
{
#if notUserControl

    public class OrderPanel : Border
    {
        #region fields & properties
        private Grid grdHeader;
        private Border brdTblHeader;
        private StackPanel stkDishes;

        private OrderViewModel _order;
        private int _pageIndex;

        private Size _size;
        private double _fontSize;

        // высота панели заказа
        public double PanelHeight {
            get {
#if fromActualHeight
                return this.ActualHeight;
#else
                return this.DesiredSize.Height;
#endif
            }
        }

        public double HeaderHeight {
            get {
#if fromActualHeight
                return this.grdHeader.ActualHeight + this.brdTblHeader.ActualHeight;
#else
                return this.grdHeader.DesiredSize.Height + this.brdTblHeader.DesiredSize.Height;
#endif
            }
        }
        public double DishTableHeaderHeight {
            get {
#if fromActualHeight
                return this.brdTblHeader.ActualHeight;
#else
                return this.brdTblHeader.DesiredSize.Height;
#endif
            }
        }

        public UIElementCollection DishPanels { get { return this.stkDishes.Children; } }
        public int ItemsCount { get { return this.DishPanels.Count; } }

        public int CanvasColumnIndex { get; set; }
        public OrderPanelHeader HeaderPanel
        {
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

        public OrderViewModel OrderViewModel { get { return _order; } }

        public int PageIndex { get { return _pageIndex; } }

#endregion


        // CTOR
        public OrderPanel(OrderViewModel orderView, int pageIndex, double width, bool isCreateHeaderPanel)
        {
            base.Width = width;
            this.SnapsToDevicePixels = true;

            _order = orderView;
            if (_order != null) orderView.ViewPanel = this;
            _pageIndex = pageIndex;
            _size = new Size(base.Width, double.PositiveInfinity);
            // установить шрифт текстовых блоков в заголовке таблицы блюд
            double fontScale = (double)WpfHelper.GetAppGlobalValue("AppFontScale", 1.0d);
            double fontSize = (double)WpfHelper.GetAppGlobalValue("ordPnlDishTblHeaderFontSize", 10d);
            _fontSize = fontSize * fontScale;
            // стиль текстовых блоков
            Style tblStyle = new Style(typeof(TextBlock));
            tblStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            tblStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));

            this.BorderThickness = new Thickness(0d);

            Grid grdOrderPanel = new Grid();
            // 0.строка заголовка заказа, может содержать OrderPanelHeader
            grdOrderPanel.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(0, GridUnitType.Auto)});
            // 1. заголовок таблицы блюд
            grdOrderPanel.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(0, GridUnitType.Auto)});
            // 2. строка блюд
            grdOrderPanel.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(0, GridUnitType.Auto) });

            // 0.строка заголовка заказа, может содержать OrderPanelHeader
            grdHeader = new Grid();
            grdHeader.SetValue(Grid.RowProperty, 0);
            // создать заголовок заказа
            if (isCreateHeaderPanel)
            {
                OrderPanelHeader hdrPnl = new OrderPanelHeader(_order, width);
                // и добавить его к заказу
                this.grdHeader.Children.Add(hdrPnl);
            }
            grdOrderPanel.Children.Add(grdHeader);

            // 1. заголовок таблицы блюд
            brdTblHeader = new Border() { Background = Brushes.AliceBlue, BorderBrush = Brushes.DarkBlue, BorderThickness = new Thickness(1, 0, 1, 1) };
            brdTblHeader.SetValue(Grid.RowProperty, 1);
            Grid grdTblHeader = new Grid();
            // 0. № п/п
            grdTblHeader.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(0.2d, GridUnitType.Star)});
            // 1. наименование блюда
            grdTblHeader.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1.0d, GridUnitType.Star)});
            // 2. количество 
            grdTblHeader.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(0.3d, GridUnitType.Star) });
            // 3. таймер состояния
            grdTblHeader.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(0.8d, GridUnitType.Star) });
            TextBlock tblIndex = new TextBlock() { Text = "№", FontSize = _fontSize, Style = tblStyle };
            tblIndex.SetValue(Grid.ColumnProperty, 0); grdTblHeader.Children.Add(tblIndex);
            TextBlock tblDishName = new TextBlock() { Text = "Блюдо", FontSize = _fontSize, Style = tblStyle };
            tblDishName.SetValue(Grid.ColumnProperty, 1); grdTblHeader.Children.Add(tblDishName);
            TextBlock tblQty = new TextBlock() { Text = "Кол-во", FontSize = _fontSize, Style = tblStyle };
            tblQty.SetValue(Grid.ColumnProperty, 2); grdTblHeader.Children.Add(tblQty);
            TextBlock tblTimer = new TextBlock() { Text = "Время", FontSize = _fontSize, Style = tblStyle };
            tblTimer.SetValue(Grid.ColumnProperty, 3); grdTblHeader.Children.Add(tblTimer);
            brdTblHeader.Child = grdTblHeader;
            grdOrderPanel.Children.Add(brdTblHeader);

            // 2. строка блюд
            stkDishes = new StackPanel();
            stkDishes.SetValue(Grid.RowProperty, 2);
            grdOrderPanel.Children.Add(stkDishes);

            this.Child = grdOrderPanel;

            //if (!orderView.DivisionColorRGB.IsNull())
            //{
            //    brdOrder.BorderBrush = AppLib.GetBrushFromRGBString(orderView.DivisionColorRGB);
            //    brdOrder.BorderThickness = new Thickness(10d);
            //    brdOrder.CornerRadius = (isCreateHeaderPanel) ? new CornerRadius(15d,15d,0,0) : new CornerRadius(0);
            //}
        }  // CTOR

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
            double totalHeight = topValue + this.PanelHeight;

            List<UIElement> retVal = new List<UIElement>();
            retVal.Add(dishPanel);
            this.stkDishes.Children.Remove(dishPanel);
#if fromActualHeight
            totalHeight -= dishPanel.ActualHeight;
#else
            totalHeight -= dishPanel.DesiredSize.Height;
#endif
            // если не пусто, то это ингредиент и содержит значение родительского Uid
            string parentUid = dishPanel.DishView.ParentUID;

            UIElement uiElem;
            bool isMove;
            // сохраняем в массиве разделитель подач или все ингредиенты вместе с блюдом
            for (int i = idx - 1; i >= 0; i--)
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
#if fromActualHeight
                    totalHeight -= ((FrameworkElement)uiElem).ActualHeight;
#else
                    totalHeight -= uiElem.DesiredSize.Height;
#endif
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

        internal int DishPanelsCount()
        {
            int retVal = 0;
            foreach (UIElement item in this.stkDishes.Children)
            {
                if (item is DishPanel) retVal++;
            }

            return retVal;
        }

    }  // class OrderPanel
#endif
                }
