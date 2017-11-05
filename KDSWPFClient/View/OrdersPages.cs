using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using KDSWPFClient.Lib;
using System.Windows;
using System.Diagnostics;
using KDSWPFClient.ViewModel;
using System.Windows.Media;
using System.Globalization;
using System.Windows.Threading;
using KDSWPFClient.ServiceReference1;
using IntegraLib;

namespace KDSWPFClient.View
{
    // список всех страниц заказов
    public class OrdersPages
    {
        private List<OrdersPage> _pages;
        private Viewbox _uiPanel;
        private Size _uiPanelSize;

        // для расчета размещения панелей заказов на канве
        private int _pageColsCount;
        private double _colWidth, _colMargin;
        private int _curColIndex; // 1-based value !!!!
        private double _curTopValue;
        private double _hdrTopMargin;
        private int _currentPageIndex;  // 1-based value !!!!

        public int CurrentPageIndex { get { return _currentPageIndex; } }
        public int Count { get { return _pages.Count; } }
        public OrdersPage CurrentPage { get; set; }

        public OrdersPages(Viewbox uiPanel)
        {
            // панель, в которой будут размещаться канвы с панелями заказов - необходимо для получения правильных размеров элементов на этой панели
            _uiPanel = uiPanel;
            _uiPanelSize = _uiPanel.RenderSize; 
           
            _pages = new List<OrdersPage>();
            _pages.Add(new OrdersPage(_uiPanel));
            CurrentPage = _pages[0];

            ResetOrderPanelSize();

            _curColIndex = 1; _curTopValue = 0d;
        }

        public void ResetOrderPanelSize()
        {
            _pageColsCount = Convert.ToInt32(AppPropsHelper.GetAppGlobalValue("OrdersColumnsCount"));
            _colWidth = Convert.ToDouble(AppPropsHelper.GetAppGlobalValue("OrdersColumnWidth"));
            _colMargin = Convert.ToDouble(AppPropsHelper.GetAppGlobalValue("OrdersColumnMargin"));
            _hdrTopMargin = Convert.ToDouble(AppPropsHelper.GetAppGlobalValue("OrderPanelTopMargin"));
        }

        public int GetMaxDishesCountOnPage()
        {
            int retVal = 0;
            double h = _uiPanel.ActualHeight;
            double w = _uiPanel.ActualWidth;

            Canvas tmpCanvas = new Canvas();
            OrderDishViewModel dishModel = new OrderDishViewModel();
            dishModel.DishName = "QWERTY";
            DishPanel dshPnl = new DishPanel(dishModel);
            dshPnl.Width = _colWidth;
            tmpCanvas.Children.Add(dshPnl);
            _uiPanel.Child = tmpCanvas;
            _uiPanel.UpdateLayout();

            double dishHeight = dshPnl.DesiredSize.Height;

            _uiPanel.Child = null;
            dshPnl = null;
            tmpCanvas = null;

            return retVal;
        }

        // добавить все заказы и определить кол-во страниц
        public void AddOrdersPanels(List<OrderViewModel> orders)
        {
            ClearPages();
            _curColIndex = 1; _curTopValue = 0d;
            Visibility vis = _uiPanel.Visibility;
            _uiPanel.Visibility = Visibility.Hidden;

            foreach (OrderViewModel ord in orders)
            {
                AddOrderPanel(ord);
            }

            SetFirstPage();
            _uiPanel.Visibility = vis;
        }

        //*******************************************
        //   РАЗМЕЩЕНИЕ ПАНЕЛЕЙ ЗАКАЗОВ
        //*******************************************
        public void AddOrderPanel(OrderViewModel orderModel)
        {
            OrderPanel ordPnl; DishPanel dshPnl; //, curDshPnl = null;

            // СОЗДАТЬ ПАНЕЛЬ ЗАКАЗА вместе с ЗАГОЛОВКОМ заказа и строкой заголовка таблицы блюд
            ordPnl = new OrderPanel(orderModel, _currentPageIndex, _colWidth, true);

            // поле между заказами по вертикали
            if (_curTopValue > 0d) _curTopValue += _hdrTopMargin;

            ordPnl.SetPosition(_curTopValue, getLeftOrdPnl());
            CurrentPage.AddOrder(ordPnl);
            CurrentPage.UpdateLayout();
            // перенос в новый столбец всего заказа
            if ((_curTopValue + ordPnl.PanelHeight) >= _uiPanelSize.Height)
            {
                moveToNewCol(ordPnl);
                CurrentPage.UpdateLayout();
            }

            int curFiling = 0;
            // блюда
            foreach (OrderDishViewModel dishModel in orderModel.Dishes)
            {
                if (curFiling != dishModel.FilingNumber)
                {
                    curFiling = dishModel.FilingNumber;
                    DishDelimeterPanel newDelimPanel = new DishDelimeterPanel()
                    { Text = "Подача " + curFiling.ToString(), DontTearOffNext = true, Background = Brushes.AliceBlue };
                    if (curFiling == 1) newDelimPanel.Foreground = Brushes.Red; else newDelimPanel.Foreground = Brushes.Blue;

                    ordPnl.AddDelimiter(newDelimPanel); // и добавить в стек
                }

                //if (dishModel.ParentUID.IsNull()) curDshPnl = null;  // сохранить родительское блюдо
                dshPnl = new DishPanel(dishModel);  // , curDshPnl
                //if (dishModel.ParentUID.IsNull()) curDshPnl = dshPnl;  // сохранить родительское блюдо

                // добавить строку заказа в стек
                ordPnl.AddDish(dshPnl);
                CurrentPage.UpdateLayout();

                if ((_curTopValue + Math.Ceiling(ordPnl.PanelHeight)) > _uiPanelSize.Height)  // переход в новый столбец
                {
                    // 1. удалить из ordPnl только что добавленное блюдо
                    //    и вернуть массив удаленных элементов, возможно с "висячим" разделителем номера подачи
                    UIElement[] delItems = ordPnl.RemoveDish(dshPnl, _curTopValue, _uiPanelSize.Height);

                    // разбиваем блюда заказа по колонкам на той же странице
                    if ((ordPnl.ItemsCount > 2) 
                        && ((_curColIndex < _pageColsCount) || ((_curColIndex == _pageColsCount) && (Convert.ToDouble(ordPnl.GetValue(Canvas.TopProperty))==0d))))
                    {
                        setNextColumn();
                        // 2. создать новый OrderPanel для текущего блюда с заголовком таблицы
                        ordPnl = new OrderPanel(orderModel, _currentPageIndex, _colWidth, false);
                        // 3. добавить только что удаленные блюда
                        ordPnl.AddDishes(delItems);
                        // 4. привязать к канве
                        ordPnl.SetPosition(_curTopValue, getLeftOrdPnl());
                        CurrentPage.AddOrder(ordPnl);
                    }
                    // не разбиваем заказ, а полностью переносим в новую колонку
                    else
                    {
                        // 2. изменить координаты панели заказа
                        moveToNewCol(ordPnl);
                        // 3. добавить только что удаленные блюда
                        ordPnl.AddDishes(delItems);
                    }
                    CurrentPage.UpdateLayout();
                }

            }  // foreach dishes

            _curTopValue += ordPnl.PanelHeight;
        }

        private void moveToNewCol(OrderPanel ordPnl)
        {
            int iPagePrev = _currentPageIndex;
            setNextColumn();
            ordPnl.SetPosition(_curTopValue, getLeftOrdPnl());
            //    добавлена новая страница(канва) - перенести панель заказа из предыд.канвы в текущую
            if (iPagePrev != _currentPageIndex)
            {
                OrdersPage page = _pages[iPagePrev - 1];
                page.DelOrder(ordPnl);
                ordPnl.SetPosition(_curTopValue, getLeftOrdPnl());
                CurrentPage.AddOrder(ordPnl);
            }
        }

        private Size MeasureString(TextBlock textBlock, string candidate)
        {
            var formattedText = new FormattedText(
                candidate,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(textBlock.FontFamily, textBlock.FontStyle, textBlock.FontWeight, textBlock.FontStretch),
                textBlock.FontSize,
                Brushes.Black);

            return new Size(formattedText.Width, formattedText.Height);
        }

        private void setNextColumn()
        {
            _curColIndex++;
            if (_curColIndex > _pageColsCount) createNewPage();

            _curTopValue = 0d;
        }

        private void createNewPage()
        {
            OrdersPage page = new OrdersPage(_uiPanel);
            _pages.Add(page);
            CurrentPage = page;
            _curColIndex = 1;

            _currentPageIndex = _pages.Count;
            _uiPanel.Child = CurrentPage;
        }

        // очистить все страницы и удалить все, кроме первой
        public void ClearPages()
        {
            // очистить все страницы
            foreach (OrdersPage page in _pages) page.ClearOrders();
            // удалить все страницы, кроме первой
            if (_pages.Count > 1) _pages.RemoveRange(1, _pages.Count - 1);

            CurrentPage = _pages[0]; _currentPageIndex = 1;

            if (CurrentPage.Parent == null) _uiPanel.Child = CurrentPage;
        }

        // возвращает true, если произошла смена страницы, иначе возвращает false
        public bool SetPageIndex(int pageIndex)
        {
            if (pageIndex <= 0) pageIndex = 1;
            else if (pageIndex > _pages.Count) pageIndex = _pages.Count;

            bool retVal = false;
            if (_currentPageIndex != pageIndex)
            {
                retVal = true;
                _currentPageIndex = pageIndex;
                CurrentPage = _pages[_currentPageIndex - 1];

                if (CurrentPage.Parent == null) _uiPanel.Child = CurrentPage;
            }

            return retVal;
        }

        public void SetFirstPage()
        {
            SetPageIndex(1);
        }

        public bool SetNextPage()
        {
            return SetPageIndex(_currentPageIndex + 1);
        }
        public bool SetPreviousPage()
        {
            return SetPageIndex(_currentPageIndex - 1);
        }

        private double getLeftOrdPnl()
        {
            return ((_curColIndex - 1) * _colWidth) + (_curColIndex * _colMargin);
        }

    } // class OrdersCanvasList


    // класс страницы заказов / экран заказов
    public class OrdersPage: Canvas
    {
        private List<OrderPanel> _ordPanels;
        public List<OrderPanel> OrderPanels { get { return _ordPanels; } }

        public int Index { get; set; }

        public OrdersPage(Decorator uiPanel)
        {
            _ordPanels = new List<OrderPanel>();
            
            this.Width = uiPanel.RenderSize.Width;
            this.Height = uiPanel.RenderSize.Height;
            uiPanel.Child = this;
        }

        internal new void UpdateLayout()
        {
            base.UpdateLayout();
        }

        internal void AddOrder(OrderPanel panel)
        {
            _ordPanels.Add(panel);
            base.Children.Add(panel);
        }

        internal void DelOrder(OrderPanel panel)
        {
            if (_ordPanels.Contains(panel))
            {
                _ordPanels.Remove(panel);
                base.Children.Remove(panel);
            }
        }

        internal void ClearOrders()
        {
            _ordPanels.Clear();
            base.Children.Clear();
        }

        
    }  // class OrdersCanvas

}
