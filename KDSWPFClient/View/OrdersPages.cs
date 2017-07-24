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

namespace KDSWPFClient.View
{
    // список всех страниц заказов
    public class OrdersPages
    {
        private double _cnvWidth, _cnvHeight;
        private List<OrdersPage> _pages;

        // для расчета размещения панелей заказов на канве
        private double _colWidth, _colMargin;
        private int _curColIndex;
        private double _curTopValue;
        private double _hdrTopMargin;
        private int _currentPageIndex;  // 1-based value !!!!

        // количество столбцов заказов, берется из config-файла и редактируется в окне Настройка
        private int _pageColsCount;
        public int OrdersColumnsCount { set { _pageColsCount = value; } }

        public int CurrentPageIndex { get { return _currentPageIndex; } }
        public int Count { get { return _pages.Count; } }
        public OrdersPage CurrentPage { get; set; }

        public OrdersPages(double cnvWidth, double cnvHeight)
        {
            _cnvWidth = cnvWidth; _cnvHeight = cnvHeight;

            _pages = new List<OrdersPage>();
            _pages.Add(new OrdersPage(_cnvWidth, _cnvHeight));
            CurrentPage = _pages[0];

            ResetOrderPanelSize();

            _curColIndex = 1; _curTopValue = 0d;
        }

        public void ResetOrderPanelSize()
        {
            _colWidth = (double)AppLib.GetAppGlobalValue("OrdersColumnWidth");
            _colMargin = (double)AppLib.GetAppGlobalValue("OrdersColumnMargin");
            _hdrTopMargin = (double)AppLib.GetAppGlobalValue("ordPnlTopMargin");
        }

        // добавить все заказы и определить кол-во страниц
        public void AddOrdersPanels(List<OrderViewModel> orders)
        {
            ClearPages();

            _curColIndex = 1; _curTopValue = 0d;

            foreach (OrderViewModel ord in orders)
            {
                AddOrderPanel(ord);
            }

            CurrentPage = _pages[0]; _currentPageIndex = 1;
        }

        //*******************************************
        //   РАЗМЕЩЕНИЕ ПАНЕЛЕЙ ЗАКАЗОВ
        //*******************************************
        public void AddOrderPanel(OrderViewModel orderModel)
        {
            OrderPanel ordPnl; DishPanel dshPnl, curDshPnl = null;

            // 2017-07-24 по заявке Ридченко
            // одинаковый ли статус всех блюд? 
            // если статус ВСЕХ блюд не равен статусу заказа, то отобразить статус заказа статусом ОТОБРАЖАЕМЫХ блюд, 
            // т.к. на КДСе могут отображаться не ВСЕ блюда (в зависим. от настроек)
            OrderStatusEnum allDishesStatus = AppLib.GetStatusAllDishes(orderModel.Dishes);
            if ((allDishesStatus != OrderStatusEnum.None) 
                && (allDishesStatus != OrderStatusEnum.WaitingCook) 
                && ((int)allDishesStatus != (int)orderModel.Status) 
                && (bool)AppLib.GetAppGlobalValue("IsShowOrderStatusByAllShownDishes"))
            {
                orderModel.SetStatus((StatusEnum)(int)allDishesStatus);
            }

//            DebugTimer.Init("order id " + orderModel.Id + " Header");
            // СОЗДАТЬ ПАНЕЛЬ ЗАКАЗА
            // вместе с ЗАГОЛОВКОМ заказа и строкой заголовка таблицы блюд
            ordPnl = new OrderPanel(orderModel, _currentPageIndex, _colWidth, true);  // в конструкторе уже посчитан DesiredSize

            if (_curTopValue > 0d) _curTopValue += _hdrTopMargin; // поле между заказами по вертикали

            if ((_curTopValue + ordPnl.HeightPanel) >= _cnvHeight)  // переход в новый столбец
            {
                setNextColumn();
                _curTopValue = 0d;
            }
//            DebugTimer.GetInterval();

            int curFiling = 0;
            // блюда
//            DebugTimer.Init("order id " + orderModel.Id + " Dishes");
            foreach (OrderDishViewModel dishModel in orderModel.Dishes)
            {
                if (curFiling != dishModel.FilingNumber)
                {
                    curFiling = dishModel.FilingNumber;
                    DishDelimeterPanel newDelimPanel = new DishDelimeterPanel() { Text = "Подача " + curFiling.ToString(), FilingNumber = curFiling };
                    ordPnl.AddDelimiter(newDelimPanel); // и добавить в стек и измерить высоту
                }

                if (dishModel.ParentUID.IsNull()) curDshPnl = null;  // сохранить родительское блюдо
                dshPnl = new DishPanel(dishModel, curDshPnl);
                if (dishModel.ParentUID.IsNull()) curDshPnl = dshPnl;  // сохранить родительское блюдо

                ordPnl.AddDish(dshPnl);  // добавить в стек и измерить высоту
                if ((_curTopValue + ordPnl.HeightPanel) >= _cnvHeight)  // переход в новый столбец
                {
                    // 1. удалить из ordPnl только что добавленное блюдо
                    //    и вернуть массив удаленных элементов, возможно с "висячим" разделителем номера подачи
                    UIElement[] delItems = ordPnl.RemoveDish(dshPnl, _cnvHeight);

                    // разбиваем блюда заказа по колонкам на той же странице
                    if ((ordPnl.Lines > 2) && (_curColIndex < _pageColsCount))
                    {
                        // 2. добавить в канву начало заказа
                        ordPnl.SetValue(Canvas.LeftProperty, getLeftOrdPnl());
                        //ordPnl.SetValue(Canvas.TopProperty, ordTop);
                        ordPnl.SetValue(Canvas.TopProperty, _curTopValue);
                        CurrentPage.Children.Add(ordPnl);
                        // 3. создать новый OrderPanel для текущего блюда с заголовком таблицы
                        ordPnl = new OrderPanel(orderModel, _currentPageIndex, _colWidth, false); // высота уже измерена
                    }
                    // не разбиваем заказ, а полностью переносим новую колонку
                    else
                    {
                        if (ordPnl.HeightPanel >= _cnvHeight)
                        {
                            setNextColumn(); _curTopValue = 0d;
                            // 2. добавить в канву начало заказа
                            ordPnl.SetValue(Canvas.LeftProperty, getLeftOrdPnl());
                            //ordPnl.SetValue(Canvas.TopProperty, ordTop);
                            ordPnl.SetValue(Canvas.TopProperty, _curTopValue);
                            CurrentPage.Children.Add(ordPnl);
                            // 3. создать новый OrderPanel для текущего блюда с заголовком таблицы
                            ordPnl = new OrderPanel(orderModel, _currentPageIndex, _colWidth, false); // высота уже измерена
                        }
                    }

                    // 4. добавить только что удаленные блюда
                    ordPnl.AddDish(delItems);
                    setNextColumn();
                    _curTopValue = 0d;
                }

            }  // foreach dishes
//            DebugTimer.GetInterval();

            // смещение слева по номеру тек.колонки
            ordPnl.SetValue(Canvas.LeftProperty, getLeftOrdPnl());
            // смещение сверху
            //ordPnl.SetValue(Canvas.TopProperty, ordTop);
            ordPnl.SetValue(Canvas.TopProperty, _curTopValue);
            _curTopValue += ordPnl.HeightPanel;

            // добавить панель заказа на страницу
            CurrentPage.Children.Add(ordPnl);
        }


        internal void RemoveOrderPanel(OrderViewModel orderView)
        {
            OrderPanel oPnl = orderView.ViewPanel;
            int pageIndex = oPnl.PageIndex - 1;

            _pages[pageIndex].Children.Remove(oPnl);
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
            if (_curColIndex > _pageColsCount)
            {
                OrdersPage page = new OrdersPage(_cnvWidth, _cnvHeight);
                _pages.Add(page);
                _currentPageIndex = _pages.Count();
                CurrentPage = page;
                _curColIndex = 1;
            }
        }

        // очистить все страницы и удалить все, кроме первой
        public void ClearPages()
        {
            if (_pages.Count > 1)
            {
                foreach (OrdersPage page in _pages) page.ClearOrders();
                _pages.RemoveRange(1, _pages.Count - 1);
            }

            CurrentPage = _pages[0]; _currentPageIndex = 1;
            CurrentPage.ClearOrders();
        }


        public void SetFirstPage()
        {
            _currentPageIndex = 1;
            CurrentPage = _pages[_currentPageIndex - 1];  // because _currentPageIndex is 1-based var
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

        public OrdersPage(double width, double height)
        {
            _ordPanels = new List<OrderPanel>();

            this.Width = width;
            this.Height = height;
        }

        internal void ClearOrders()
        {
            _ordPanels.Clear();
            base.Children.Clear();
        }

        
    }  // class OrdersCanvas

}
