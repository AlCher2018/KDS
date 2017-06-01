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

namespace KDSWPFClient.View
{
    // список всех страниц заказов
    public class OrdersPages
    {
        private List<OrdersPage> _pages;

        // для расчета размещения панелей заказов на канве
        private double _screenWidth, _screenHeight;
        private double _colWidth, _colMargin;
        private int _curColIndex;
        private double _curTopValue;
        private double _hdrTopMargin;
        private int _currentPageIndex;  // 1-based value !!!!
        double _pageContentHeight;  // высота страницы по вертикали для размещения панелей заказов
        Size _availableSize;

        // количество столбцов заказов, берется из config-файла и редактируется в окне Настройка
        private int _pageColsCount;
        public int OrdersColumnsCount { set { _pageColsCount = value; } }

        public int CurrentPageIndex { get { return _currentPageIndex; } }
        public int Count { get { return _pages.Count; } }
        public OrdersPage CurrentPage { get; set; }

        public OrdersPages()
        {
            _pages = new List<OrdersPage>();
            _pages.Add(new OrdersPage());
            CurrentPage = _pages[0];

            ResetOrderPanelSize();

            _curColIndex = 1; _curTopValue = 0d;
        }

        public void ResetOrderPanelSize()
        {
            _screenWidth = (double)AppLib.GetAppGlobalValue("screenWidth");
            _screenHeight = (double)AppLib.GetAppGlobalValue("screenHeight");

            _colWidth = (double)AppLib.GetAppGlobalValue("OrdersColumnWidth");
            _colMargin = (double)AppLib.GetAppGlobalValue("OrdersColumnMargin");
            _hdrTopMargin = (double)AppLib.GetAppGlobalValue("ordPnlTopMargin");

            _pageContentHeight = AppLib.GetOrdersPageContentHeight();

            _availableSize = new Size(_colWidth, _pageContentHeight);
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
            OrderPanel ordPnl; DishPanel dshPnl;
            double ordTop;  // хранит TOP заказа
            double curLineHeight;

            // СОЗДАТЬ ПАНЕЛЬ ЗАКАЗА
            // вместе с ЗАГОЛОВКОМ заказа и строкой заголовка таблицы блюд
            ordPnl = new OrderPanel(orderModel, _currentPageIndex, _colWidth, true);  // в конструкторе уже посчитан DesiredSize
            if (_curTopValue > 0d) _curTopValue += _hdrTopMargin; // поле между заказами по вертикали
            ordTop = _curTopValue; // отступ сверху панели заказа
            //if (_curColIndex == 1) CurrentPage.Children.Add(new System.Windows.Shapes.Line() { X1 = 0, Y1 = _curTopValue, X2 = 40, Y2 = _curTopValue, Stroke = System.Windows.Media.Brushes.Red });

            curLineHeight = Math.Round(ordPnl.DesiredSize.Height); // получить размер заголовка

            if ((_curTopValue + curLineHeight) >= _pageContentHeight)  // переход в новый столбец
            {
                setNextColumn();
                ordTop = 0d; _curTopValue = curLineHeight;
            }
            else
            {
                _curTopValue += curLineHeight;
            }
            //if (_curColIndex == 1) CurrentPage.Children.Add(new System.Windows.Shapes.Line() { X1 = 0, Y1 = _curTopValue, X2 = 40, Y2 = _curTopValue, Stroke = System.Windows.Media.Brushes.Red });

            int curFiling = 0;
            // блюда
            foreach (OrderDishViewModel dishModel in orderModel.Dishes)
            {
                if (curFiling != dishModel.FilingNumber)
                {
                    curFiling = dishModel.FilingNumber;
                    DishDelimeterPanel newDelimPanel = new DishDelimeterPanel() { Text = "Подача " + curFiling.ToString(), FilingNumber = curFiling };
                    ordPnl.AddDelimiter(newDelimPanel); // и добавить в стек и измерить высоту
                    //if (_curColIndex == 1) CurrentPage.Children.Add(new System.Windows.Shapes.Line() { X1 = 0, Y1 = _curTopValue, X2 = 40, Y2 = _curTopValue, Stroke = System.Windows.Media.Brushes.Red });
                    curLineHeight = Math.Round(newDelimPanel.DesiredSize.Height);
                    _curTopValue += curLineHeight; // сместить Top
                }

                dshPnl = new DishPanel(dishModel);
                ordPnl.AddDish(dshPnl);  // добавить в стек и измерить высоту

                curLineHeight = Math.Round(dshPnl.DesiredSize.Height); // получить высоту строки блюда
                if ((_curTopValue + curLineHeight) >= _pageContentHeight)  // переход в новый столбец
                {
                    // разбиваем блюда заказа
                    if ((dishModel.Index > 2) && (_curColIndex < _pageColsCount)) 
                    {
                        // 1. удалить из ordPnl только что добавленное блюдо
                        //    и вернуть массив удаленных элементов, возможно с "висячим" разделителем номера подачи
                        UIElement[] delItems = ordPnl.RemoveDish(dshPnl);
                        // 2. добавить в канву начало заказа
                        ordPnl.SetValue(Canvas.LeftProperty, getLeftOrdPnl());
                        ordPnl.SetValue(Canvas.TopProperty, ordTop);
                        CurrentPage.Children.Add(ordPnl);

                        // 3. создать новый OrderPanel для текущего блюда с заголовком таблицы
                        ordPnl = new OrderPanel(orderModel, _currentPageIndex, _colWidth, false); // высота уже измерена
                        ordTop = 0d; _curTopValue = Math.Round(ordPnl.DesiredSize.Height);
                        // 4. добавить только что удаленное блюдо
                        ordPnl.AddDish(delItems);  // добавить в стек без измерения высоты
                        // переопределить приращение, просуммировав высоту всех удаленных элементов
                        curLineHeight = 0; foreach (UIElement item in delItems) curLineHeight += Math.Round(item.DesiredSize.Height);
                        setNextColumn();
                    }
                    // не разбиваем заказ, а полностью переносим
                    else
                    {
                        setNextColumn();
                        _curTopValue = _curTopValue - ordTop;  // "высота" заказа в новом столбце
                        ordTop = 0d;
                    }
                }

                _curTopValue += curLineHeight;
                //if (_curColIndex == 1) CurrentPage.Children.Add(new System.Windows.Shapes.Line() { X1 = 0, Y1 = _curTopValue, X2 = 40, Y2 = _curTopValue, Stroke = System.Windows.Media.Brushes.Red });

            }

            // смещение слева по номеру тек.колонки
            ordPnl.SetValue(Canvas.LeftProperty, getLeftOrdPnl());
            // смещение сверху
            ordPnl.SetValue(Canvas.TopProperty, ordTop);

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

        public OrdersPage()
        {
            _ordPanels = new List<OrderPanel>();

            double _screenWidth = (double)AppLib.GetAppGlobalValue("screenWidth");
            double contentHeight = AppLib.GetOrdersPageContentHeight();

            this.Width = _screenWidth; this.Height = contentHeight;
        }

        internal void ClearOrders()
        {
            _ordPanels.Clear();
            base.Children.Clear();
        }

        
    }  // class OrdersCanvas

}
