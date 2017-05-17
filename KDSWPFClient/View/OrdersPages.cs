using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using KDSWPFClient.Lib;
using System.Windows;

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
        private double _hdrTopMargin = 20d;

        public int CurrentPageIndex { get; set; }
        public OrdersPage CurrentPage { get; set; }

        public OrdersPages()
        {
            _pages = new List<OrdersPage>();
            _pages.Add(new OrdersPage());
            CurrentPage = _pages[0];

            _screenWidth = (double)AppLib.GetAppGlobalValue("screenWidth");
            _screenHeight = (double)AppLib.GetAppGlobalValue("screenHeight") - 2d * (double)AppLib.GetAppGlobalValue("ordPnlTopBotMargin");

            _pageColsCount = (int)AppLib.GetAppGlobalValue("OrdersColumnsCount");
            _colWidth = (double)AppLib.GetAppGlobalValue("OrdersColumnWidth");
            _colMargin = (double)AppLib.GetAppGlobalValue("OrdersColumnMargin");

            _curColIndex = 1; _curTopValue = 0d;
        }

        public void AddOrder(TestData.OrderTestModel orderModel)
        {
            OrderPanel ordPnl; DishPanel dshPnl;
            double ordTop;  // хранит TOP заказа
            Size availableSize = new Size(_colWidth, _screenHeight);

            ordPnl = new OrderPanel();
            ordPnl.Width = _colWidth;
            ordTop = _curTopValue; if (ordTop > 0d) ordTop += _hdrTopMargin;

            // заголовок заказа
            OrderPanelHeader hdrPnl = new OrderPanelHeader();
            hdrPnl.TableName = orderModel.TableName;
            hdrPnl.OrderNumber = orderModel.Number.ToString();
            hdrPnl.WaiterName = orderModel.Waiter;
            hdrPnl.CreateDate = orderModel.CreateDate;
            ordPnl.SetHeader(hdrPnl);  // добавить заголовок к заказу

            // узнать размер заголовка
            ordPnl.Measure(availableSize);
            if ((_curTopValue + ordPnl.DesiredSize.Height) >= _screenHeight)  // переход в новый столбец
            {
                _curColIndex++; _curTopValue = 0d;
            }
            else
            {
                _curTopValue += ordPnl.DesiredSize.Height;
            }

            // блюда
            int dishIndex = 0;
            foreach (TestData.OrderDishTestModel dishModel in orderModel.Dishes)
            {
                dishIndex++;
                dshPnl = new DishPanel(dishIndex, dishModel.FilingNumber, dishModel.Name, dishModel.Quantity);
                dshPnl.Width = _colWidth;

                // узнать размер панели
                dshPnl.Measure(availableSize);
                if ((_curTopValue + dshPnl.DesiredSize.Height) >= _screenHeight)  // переход в новый столбец
                {
                    if (dishIndex > 2) // разбиваем блюда заказа
                    {
                        //  добавить в канву начало заказа
                        ordPnl.SetValue(Canvas.LeftProperty, getLeftOrdPnl());
                        ordPnl.SetValue(Canvas.TopProperty, ordTop);
                        CurrentPage.Children.Add(ordPnl);
                        //  и создать новый OrderPanel для текущего блюда с заголовком таблицы
                        ordPnl = new OrderPanel();
                        ordPnl.Width = _colWidth;
                        ordTop = 0d;
                    }
                    _curColIndex++; _curTopValue = 0d;
                }
                else
                {
                    _curTopValue += dshPnl.DesiredSize.Height;
                }
                ordPnl.AddDish(dshPnl);
            }

            // смещение слева по номеру тек.колонки
            ordPnl.SetValue(Canvas.LeftProperty, getLeftOrdPnl());
            // смещение сверху
            ordPnl.SetValue(Canvas.TopProperty, ordTop);

            // добавить панель заказа на страницу
            CurrentPage.Children.Add(ordPnl);
        }

        // очистить все страницы и удалить все, кроме первой
        public void Clear()
        {
            foreach (OrdersPage page in _pages) page.ClearOrders();

            _pages.RemoveRange(1, _pages.Count-1);

            _curColIndex = 1; _curTopValue = 0d;
            CurrentPage = _pages[0];
        }

        private double getLeftOrdPnl()
        {
            return (_curColIndex - 1) * (_colWidth + _colMargin) + _colMargin;
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

            double topBotMargValue = (double)AppLib.GetAppGlobalValue("ordPnlTopBotMargin");

            this.Width = _screenWidth; this.Height = _screenHeight - 2d * topBotMargValue;
        }

        internal void ClearOrders()
        {
            _ordPanels.Clear();
        }

        
    }  // class OrdersCanvas

}
