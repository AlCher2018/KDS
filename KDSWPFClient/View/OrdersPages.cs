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

namespace KDSWPFClient.View
{
    // список всех страниц заказов
    public class OrdersPages
    {
        private List<OrdersPage> _pages;

        // для расчета размещения панелей заказов на канве
        private double _screenWidth, _screenHeight;
        private int _pageColsCount;
        private double _colWidth, _colMargin;
        private int _curColIndex;
        private double _curTopValue;
        private double _hdrTopMargin;
        private int _currentPageIndex;  // 1-based value !!!!

        public int CurrentPageIndex { get { return _currentPageIndex; } }
        public int Count { get { return _pages.Count; } }
        public OrdersPage CurrentPage { get; set; }

        public OrdersPages()
        {
            _pages = new List<OrdersPage>();
            _pages.Add(new OrdersPage());
            CurrentPage = _pages[0];

            _screenWidth = (double)AppLib.GetAppGlobalValue("screenWidth");
            _screenHeight = (double)AppLib.GetAppGlobalValue("screenHeight");

            _pageColsCount = (int)AppLib.GetAppGlobalValue("OrdersColumnsCount");
            _colWidth = (double)AppLib.GetAppGlobalValue("OrdersColumnWidth");
            _colMargin = (double)AppLib.GetAppGlobalValue("OrdersColumnMargin");
            _hdrTopMargin = (double)AppLib.GetAppGlobalValue("ordPnlTopMargin");

            _curColIndex = 1; _curTopValue = 0d;
        }

        // добавить все заказы и определить кол-во страниц
        public void AddOrdersPanels(List<OrderViewModel> orders)
        {
            ClearPages();

            _curColIndex = 1; _curTopValue = 0d;

            foreach (OrderViewModel ord in orders)
            {
#if DEBUG_LAYOUT
                Debug.Print("размещение заказа {0} (блюд {1})", ord.Number, ord.Dishes.Count);
#endif
                AddOrderPanel(ord);
            }

            CurrentPage = _pages[0]; _currentPageIndex = 1;
        }

        public void AddOrderPanel(OrderViewModel orderModel)
        {
            OrderPanel ordPnl; DishPanel dshPnl;
            double ordTop;  // хранит TOP заказа

            double dishesPanelHeight = CurrentPage.Height;
            Size availableSize = new Size(_colWidth, dishesPanelHeight);

            // СОЗДАТЬ ПАНЕЛЬ ЗАКАЗА
            ordPnl = new OrderPanel(orderModel, _currentPageIndex, _colWidth, true);
            if (_curTopValue > 0d) _curTopValue += _hdrTopMargin; // поле между заказами по вертикали
            ordTop = _curTopValue; // отступ сверху панели заказа
#if DEBUG_LAYOUT
            Debug.Print("start order {3}: _curColIndex {0}, ordTop {1}, _curTopValue {2}", _curColIndex, ordTop, _curTopValue, orderModel.Number);
#endif

            // узнать размер заголовка
            ordPnl.Measure(availableSize);
            if ((_curTopValue + ordPnl.DesiredSize.Height) >= dishesPanelHeight)  // переход в новый столбец
            {
                setNextColumn();
                ordTop = 0d; _curTopValue = ordPnl.DesiredSize.Height;
#if DEBUG_LAYOUT
                Debug.Print("  - MOVE header {3} to next column: _curColIndex {0}, ordTop {1}, _curTopValue {2}", _curColIndex, ordTop, _curTopValue, orderModel.Number);
#endif
            }
            else
            {
                _curTopValue += ordPnl.DesiredSize.Height;
#if DEBUG_LAYOUT
                Debug.Print("  - stay header {3} on current column: _curColIndex {0}, ordTop {1}, _curTopValue {2}", _curColIndex, ordTop, _curTopValue, orderModel.Number);
#endif
            }

            // блюда
            foreach (OrderDishViewModel dishModel in orderModel.Dishes)
            {
                dshPnl = new DishPanel(dishModel);
                dshPnl.Width = _colWidth;

                // узнать размер панели
                dshPnl.Measure(availableSize);
                if ((_curTopValue + dshPnl.DesiredSize.Height) >= dishesPanelHeight)  // переход в новый столбец
                {
                    if ((dishModel.Index > 2) && (_curColIndex < _pageColsCount)) // разбиваем блюда заказа
                    {
                        //  добавить в канву начало заказа
                        ordPnl.SetValue(Canvas.LeftProperty, getLeftOrdPnl());
                        ordPnl.SetValue(Canvas.TopProperty, ordTop);
                        CurrentPage.Children.Add(ordPnl);
#if DEBUG_LAYOUT
                        Debug.Print("  - BREAK order {3}: _curColIndex {0}, ordTop {1}, _curTopValue {2}", _curColIndex, ordTop, _curTopValue, orderModel.Number);
#endif
                        //  и создать новый OrderPanel для текущего блюда с заголовком таблицы
                        ordPnl = new OrderPanel(orderModel, _currentPageIndex, _colWidth, false);
                        ordPnl.Measure(availableSize);
                        setNextColumn();
                        ordTop = 0d; _curTopValue = ordPnl.DesiredSize.Height;
#if DEBUG_LAYOUT
                        Debug.Print("  - BREAKING order {3} on next col: _curColIndex {0}, ordTop {1}, _curTopValue {2}", (_curColIndex + 1), ordTop, _curTopValue, orderModel.Number);
#endif
                    }
                    else   // не разбиваем заказ, а полностью переносим
                    {
                        setNextColumn();
                        _curTopValue = _curTopValue - ordTop;  // "высота" заказа в новом столбце
                        ordTop = 0d;
#if DEBUG_LAYOUT
                        Debug.Print("  - MOVE order {3} to next column: _curColIndex {0}, ordTop {1}, _curTopValue {2}", _curColIndex, ordTop, _curTopValue, orderModel.Number);
#endif
                    }
                }

                _curTopValue += dshPnl.DesiredSize.Height;
#if DEBUG_LAYOUT
                Debug.Print("    - stay dish {3} on current column: _curColIndex {0}, ordTop {1}, _curTopValue {2}", _curColIndex, ordTop, _curTopValue, dishIndex);
#endif

                ordPnl.AddDish(dshPnl);
            }

            // смещение слева по номеру тек.колонки
            ordPnl.SetValue(Canvas.LeftProperty, getLeftOrdPnl());
            // смещение сверху
            ordPnl.SetValue(Canvas.TopProperty, ordTop);

#if DEBUG_LAYOUT
            Debug.Print(" ** finish order {3}: _curColIndex {0}, ordTop {1}, _curTopValue {2}", _curColIndex, ordTop, _curTopValue, orderModel.Number);
#endif

            // добавить панель заказа на страницу
            CurrentPage.Children.Add(ordPnl);
        }

        internal void RemoveOrderPanel(OrderViewModel orderView)
        {
            OrderPanel oPnl = orderView.ViewPanel;
            int pageIndex = oPnl.PageIndex - 1;

            _pages[pageIndex].Children.Remove(oPnl);
        }

        private void setNextColumn()
        {
            _curColIndex++;
            if (_curColIndex > _pageColsCount)
            {
                OrdersPage page = new OrdersPage();
                _pages.Add(page);
                _currentPageIndex = _pages.Count();
                CurrentPage = page;
                _curColIndex = 1;
            }
        }

        // очистить все страницы и удалить все, кроме первой
        public void ClearPages()
        {
            foreach (OrdersPage page in _pages) page.ClearOrders();

            _pages.RemoveRange(1, _pages.Count-1);

            CurrentPage = _pages[0]; _currentPageIndex = 1;
        }

        public bool SetNextPage()
        {
            if (_currentPageIndex < _pages.Count)
            {
                _currentPageIndex++;
                CurrentPage = _pages[_currentPageIndex-1];  // because _currentPageIndex is 1-based var
                return true;
            }
            else
                return false;
        }
        public bool SetPreviousPage()
        {
            if (_currentPageIndex > 1)
            {
                _currentPageIndex--;
                CurrentPage = _pages[_currentPageIndex - 1];  // because _currentPageIndex is 1-based var
                return true;
            }
            else
                return false;
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

        public int Index { get; set; }

        public OrdersPage()
        {
            _ordPanels = new List<OrderPanel>();

            double _screenWidth = (double)AppLib.GetAppGlobalValue("screenWidth");
            double _screenHeight = (double)AppLib.GetAppGlobalValue("screenHeight");

            double topBotMargin = (double)AppLib.GetAppGlobalValue("dishesPanelTopBotMargin");

            this.Width = _screenWidth; this.Height = _screenHeight - 2d * topBotMargin;
        }

        internal void ClearOrders()
        {
            _ordPanels.Clear();
            base.Children.Clear();
        }

        
    }  // class OrdersCanvas

}
