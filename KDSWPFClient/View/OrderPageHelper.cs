﻿using IntegraLib;
using KDSWPFClient.Lib;
using KDSWPFClient.ViewModel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KDSWPFClient.View
{
    internal class OrderPageHelper
    {
        private Canvas _canvas;
        // для расчета размещения панелей заказов на канве
        private int _pageColsCount;   // кол-во колонок на странице
        private double _colWidth, _colHeight, _colMargin;
        private double _hdrTopMargin;
        private double _ordHeaderMinHeight, _continuePanelHeight;
        private bool _shiftForward;
        private static int _maxDishesCountOnPage;   // максимальное кол-во однострочных блюд на странице
        public static int MaxDishesCountOnPage { get { return _maxDishesCountOnPage; } }

        private bool _pageBreak;
        public bool PageBreak { get { return _pageBreak; } }

        public UIElementCollection OrderPanels { get { return _canvas.Children; } }

        // переменные для хранения текущих значений при размещении панелей заказов на канве
        int curColIndex;            // текущий столбец - 1-base!!
        double curTopValue;         // текущая координата Top


        public OrderPageHelper(Canvas uiBuffer)
        {
            _canvas = uiBuffer;
        }

        public void ResetOrderPanelSize()
        {
            _pageColsCount = Convert.ToInt32(WpfHelper.GetAppGlobalValue("OrdersColumnsCount"));
            _colWidth = Convert.ToDouble(WpfHelper.GetAppGlobalValue("OrdersColumnWidth"));
            _colMargin = Convert.ToDouble(WpfHelper.GetAppGlobalValue("OrdersColumnMargin"));
            _hdrTopMargin = Convert.ToDouble(WpfHelper.GetAppGlobalValue("OrderPanelTopMargin"));
            _colHeight = _canvas.ActualHeight;  // высота столбца

            OrderPanel minHeaderPanel = new OrderPanel(null, 0, _colWidth, false);
            _canvas.Children.Add(minHeaderPanel);
            DishDelimeterPanel continuePanel = createContinuePanel(true);
            _canvas.Children.Add(continuePanel);
            _canvas.UpdateLayout();

            _ordHeaderMinHeight = minHeaderPanel.ActualHeight;
            _continuePanelHeight = continuePanel.ActualHeight;

            _canvas.Children.Clear();
            minHeaderPanel = null;
        }

        public void ResetMaxDishesCountOnPage()
        {
            // создать элемент заказа (блюдо)
            OrderDishViewModel dishModel = new OrderDishViewModel();
            dishModel.DishName = "QWERTY";
            dishModel.Quantity = 1;
            // создать панель элемента
            DishPanel dshPnl = new DishPanel(dishModel, _colWidth);
            // вычислить его высоту
            _canvas.Children.Add(dshPnl);
            _canvas.UpdateLayout();
            double h = dshPnl.ActualHeight;
            _canvas.Children.Clear();

            // вычислить кол-во элементов на странице, округлить до ближайшего наибольшего
            _maxDishesCountOnPage = Convert.ToInt32(Math.Ceiling((_pageColsCount * _colHeight) / h));
            AppLib.WriteLogTraceMessage(" - reset order items count to " + _maxDishesCountOnPage.ToString());
        }

        /// <summary>
        /// Создает и размещает на одной странице панели заказов (тип OrderPanel).
        /// </summary>
        /// <param name="orders">Список заказов (тип OrderViewModel)</param>
        /// <param name="orderIndex">Индекс заказа, начиная с которого будут строиться панели</param>
        /// <param name="dishIndex">Индекс блюда, начиная с которогу будут строиться панели</param>
        /// <param name="isPanelsForward">Признак того, что коллекция orders будет листаться вперед, от стартовой позиции к концу набора</param>
        /// <returns></returns>
        internal void DrawOrderPanelsOnPage(List<OrderViewModel> orders, int orderStartIndex, int dishStartIndex, bool isPanelsForward, bool keepSplitOrderOnLastColumnByForward)
        {
            _canvas.Children.Clear();
            
            if (orders == null) return;
            if (orderStartIndex >= orders.Count) return;
            if (orders.Count == 0) return;

            OrderViewModel orderModel;
            _shiftForward = isPanelsForward;
            _pageBreak = false;

            int dishIdxFrom=-1, dishIdxTo=-1;
            bool bSplit = false;
            Size sizeMeasure = new Size(_colWidth, _colHeight);
            // инициализация переменных размещения
            if (_shiftForward) { curColIndex = 1; curTopValue = 0; }
            else { curColIndex = _pageColsCount; curTopValue = _colHeight; }
            double freeHeight = getFreeHeight();

            // цикл по заказам
            for (int iOrd = orderStartIndex;
                ((_shiftForward) ? iOrd < orders.Count : iOrd >= 0) && (!_pageBreak);
                iOrd += (_shiftForward) ? 1 : -1)
            {

                // текущий заказ
                orderModel = orders[iOrd];
                bSplit = false;

                // 1. создать панель заказа со всеми блюдами и разделителями подач
                #region текущие начальный и конечный индексы БЛЮД
                //   для последующих заказов - все блюда
                if (dishStartIndex < 0)
                {
                    dishIdxFrom = 0; dishIdxTo = orderModel.Dishes.Count - 1;
                }
                //   для первого заказа - часть блюд
                else
                {
                    if (_shiftForward)
                    {
                        dishIdxFrom = dishStartIndex;
                        dishIdxTo = orderModel.Dishes.Count - 1;
                    }
                    else
                    {
                        dishIdxFrom = 0;
                        dishIdxTo = dishStartIndex;
                        // в обратном порядке с непоследнего, поэтому добавляется разделитель переноса
                        if (dishIdxTo < (orderModel.Dishes.Count - 1)) bSplit = true;
                    }
                }
                #endregion

                DateTime dtTmr = DateTime.Now;
                OrderPanel ordPanel = createOrderPanel(orderModel, dishIdxFrom, dishIdxTo, bSplit);
                AppLib.WriteLogTraceMessage("   - create order panel N {0} - {1}", orderModel.Number, (DateTime.Now - dtTmr).ToString());

                dishStartIndex = -1;  // для последующих заказов - все блюда
                if (ordPanel == null) continue;

                // получить реальные размеры панели заказа
                dtTmr = DateTime.Now;
                if (_shiftForward) _canvas.Children.Add(ordPanel);
                else _canvas.Children.Insert(0, ordPanel);
                // get DesiredSize
                ordPanel.Measure(sizeMeasure);  // легкая, но не всегда точная операция
                //ordPanel.UpdateLayout();  // тяжелая операция
                AppLib.WriteLogTraceMessage("   - get size order panel N {0} - {1}", orderModel.Number, (DateTime.Now-dtTmr).ToString());

                // 2. Размещение панели на странице
                // помещается ли вся панель заказа без разрывов в свободное место
                if (getOrderPanelHeight(ordPanel) < freeHeight)
                {
                    dtTmr = DateTime.Now;
                    setLastPanelPosition(ordPanel, getOrderPanelHeight(ordPanel));
                    AppLib.WriteLogTraceMessage("     - layout whole one column - {0}", (DateTime.Now - dtTmr).ToString());
                }
                // надо разбивать панель
                else
                {
                    ordPanel.UpdateLayout();  // тяжелая операция
                    _canvas.Children.Remove(ordPanel);
                    dtTmr = DateTime.Now;
                    splitOrderViewPanels(ordPanel, keepSplitOrderOnLastColumnByForward);
                    AppLib.WriteLogTraceMessage("     - split order by columns - {0}", (DateTime.Now - dtTmr).ToString());
                }

                freeHeight = getFreeHeight();
            }

            // в обратном направлении для первого заказа curTopValue = 0
            //if (!_shiftForward && (_canvas.Children.Count > 0)) _canvas.Children[0].SetValue(Canvas.TopProperty, 0d);
        }

        private double getOrderPanelHeight(OrderPanel orderPanel)
        {
            double retVal = orderPanel.ActualHeight;
            if (retVal == 0d) retVal = orderPanel.DesiredSize.Height;

            return retVal;
        }

        // преобразование сплошной панели в коллекцию панелей по колонкам
        // панели записываются на канву временного размещения
        // входные параметры: 
        //  - OrderPanel ordPanel // сплошная панель, которую надо разбить по колонкам 
        private void splitOrderViewPanels(OrderPanel ordPanel, bool keepSplitOrderOnLastColumnByForward)
        {
            OrderPanel curPanel = null;
            double panelHeight = 0d;
            string orderLogInfo = string.Format("id: {0}, index: {1}, number: {2}, dishes: {3}", ordPanel.OrderViewModel.Id, ordPanel.OrderViewModel.Index, ordPanel.OrderViewModel.Number, ordPanel.OrderViewModel.Dishes.Count);

            // цикл по блокам заказа, смещая curDishIndex, пока curDishIndex не дойдет до граничного значения в соотв.напр-и
            double freeHeight = getFreeHeight();
            double curBlockHeight;
            List<UIElement> curBlock;

            while (true)
            {
                curBlock = getNextItemsBlock(ordPanel, true);
                // выбрали все блоки - установить координаты последней панели и выйти
                if (curBlock == null)
                {
                    setLastPanelPosition(curPanel, panelHeight);
                    break;  // нормальный выход - выбраны все элементы панели
                }

                // создать панель без заголовка, в которую будем переносить панели блюд
                if (curPanel == null)
                {
                    curPanel = new OrderPanel(ordPanel.OrderViewModel, 0, ordPanel.Width, false);
#if useUpdateLayout
                    curPanel.UpdateLayout();
#else
                    panelHeight = ordPanel.DishTableHeaderHeight;  // только шапка таблицы
#endif
                    _canvas.Children.Add(curPanel);
                }

                // добавить заголовок заказа в панель, если при прямом проходе блок самый первый 
                // иначе - добавлять в панель на последнем блоке из ordPanel
                if (_shiftForward && (curPanel.HeaderPanel == null) && (ordPanel.HeaderPanel != null))
                {
                    OrderPanelHeader header = ordPanel.DetachHeader();
                    curPanel.HeaderPanel = header;
#if useUpdateLayout
                    curPanel.UpdateLayout();
#else
                    panelHeight += curPanel.HeaderPanel.ActualHeight;  // заголовок заказа и шапка таблицы
#endif
                }

                curBlockHeight = getBlockHeight(curBlock);
                // при обратном проходе и отсутствии элементов в ordPanel, 
                // к curBlockHeight прибавить только высоту заголовка и шапку таблицы
                if (!_shiftForward && (ordPanel.ItemsCount == 0)) curBlockHeight += ordPanel.HeaderPanel.ActualHeight;

                // анализ размещения блока в свободном месте
#if useUpdateLayout
                if (curPanel.ActualHeight + curBlockHeight <= freeHeight)
#else
                if (panelHeight + curBlockHeight <= freeHeight)
#endif
                {
                    if (_shiftForward)
                    {
                        curPanel.AddDishes(curBlock);        // добавить текущий блок элементов к панели заказа
                    }
                    else
                    {
                        curPanel.InsertDishes(0, curBlock);  // вставить блок в начало коллекции
                        // и не забываем про заголовок заказа при обратном проходе
                        if ((ordPanel.ItemsCount == 0) && (curPanel.HeaderPanel == null) && (ordPanel.HeaderPanel != null))
                        {
                            OrderPanelHeader header = ordPanel.DetachHeader();
                            curPanel.HeaderPanel = header;  // высота заголовка заказа уже есть в curBlockHeight
                        }
                    }
#if useUpdateLayout
                    curPanel.UpdateLayout();
#else
                    panelHeight += curBlockHeight;
#endif
                }
                // переход в следующий/предыдущий столбец
                else
                {
                    if (_shiftForward)
                    {
#region движение вперед
                        // из последнего столбца
                        if (curColIndex == _pageColsCount)
                        {
                            // если панель заказа в последней колонке является продолжением заказа, т.е. HeaderPanel==null, то обрываем заказ и добавляем разделитель продолжения
                            if (((curPanel.HeaderPanel == null) || keepSplitOrderOnLastColumnByForward) && (curPanel.ItemsCount > 0))
                            {
                                // добавляем в конце разделитель продолжения заказа
                                DishDelimeterPanel delimPanel = createContinuePanel(true);
                                // удалить последний блок
#if useUpdateLayout
    if (curPanel.ActualHeight + delimPanel.ActualHeight > freeHeight)
#else
                                if (panelHeight + _continuePanelHeight > freeHeight)
#endif
                                {
                                    _shiftForward = false; // смещаясь назад
                                    curBlock = getNextItemsBlock(curPanel, true); // поиск блока и удаление его
#if useUpdateLayout == false
                                    curBlockHeight = getBlockHeight(curBlock);
                                    panelHeight -= curBlockHeight;
#endif
                                    _shiftForward = true; // восстановить смещение
                                    // если текущая панель не пустая, то добавляем панель переноса
                                    if ((curBlock != null) && (curPanel.ItemsCount > 0))
                                    {
                                        curPanel.AddDelimiter(delimPanel);
#if useUpdateLayout == false
                                        panelHeight += _continuePanelHeight;
#endif
                                    }
                                    // иначе полностью переносим панель заказа в след.страницу
                                    else
                                    {
                                        _canvas.Children.Remove(curPanel);
                                    }
                                }
                                else
                                {
                                    curPanel.AddDelimiter(delimPanel);
#if useUpdateLayout == false
                                    panelHeight += _continuePanelHeight;
#endif
                                }

#if useUpdateLayout
                                curPanel.UpdateLayout();
#endif
                                setPanelLeftTop(curPanel);
                            }
                            // иначе полностью переносим панель заказа в след.страницу
                            else
                            {
                                _canvas.Children.Remove(curPanel);
                            }
                            // перенос панели на следующую страницу
                            _pageBreak = true;
                            break;
                        }
                        // из НЕпоследнего столбца: создаем новую панель (Top=0)
                        else
                        {
                            // в текущей панели нет элементов или только одна панель DishPanel - 
                            // переносим панель в следующую колонку
                            //if ((curPanel.DishPanels.Count == 0) || (curPanel.DishPanelsCount() == 1))
                            if (curPanel.DishPanels.Count == 0)
                            {
                                curPanel.AddDishes(curBlock);   // добавить текущий блок элементов
#if useUpdateLayout
                                curPanel.UpdateLayout();
#else
                                panelHeight += curBlockHeight;
#endif
                                curColIndex++; curTopValue = 0d;
                            }
                            // размещение предыдущей панели по текущим координатам и создание новой панели
                            else
                            {
//                                dtTmr = DateTime.Now;
                                setPanelLeftTop(curPanel);
//                                AppLib.WriteLogTraceMessage("     - set Left/Top - {0}", (DateTime.Now - dtTmr).ToString());

                                // новая панель без заголовка заказа
//                                dtTmr = DateTime.Now;
                                curPanel = new OrderPanel(ordPanel.OrderViewModel, 0, ordPanel.Width, false);
                                curPanel.AddDishes(curBlock);   // добавить текущий блок элементов
//                                AppLib.WriteLogTraceMessage("     - create new panel & add dishes - {0}", (DateTime.Now - dtTmr).ToString());
#if useUpdateLayout
                                _canvas.Children.Add(curPanel);
                                curPanel.UpdateLayout();
#else
                                panelHeight = ordPanel.DishTableHeaderHeight;  // только шапка таблицы
                                panelHeight += curBlockHeight;
//                                dtTmr = DateTime.Now;
                                _canvas.Children.Add(curPanel);
//                                AppLib.WriteLogTraceMessage("     - add panel to canvas - {0}", (DateTime.Now - dtTmr).ToString());
#endif
                                // координаты следующего столбца
                                curColIndex++; curTopValue = 0d;
                            }
                            freeHeight = getFreeHeight();
                        }
#endregion
                    }

                    else
                    {
#region движение назад
                        // из первого столбца
                        if (curColIndex == 1)
                        {
                            // если в текущей панели еще нет строк, то переносим всю панель на предыдущий лист
                            // удалив текущую пустую панель
                            if (curPanel.ItemsCount == 0)
                            {
                                _canvas.Children.Remove(curPanel);
                            }
                            // если в текущей панели уже есть блюда, т.е. есть что оставлять на текущей странице, 
                            // то добавляем в начало разделитель продолжения заказа
                            else
                            {
                                DishDelimeterPanel delimPanel = createContinuePanel(false);
                                // удалить первый блок из текущей панели
#if useUpdateLayout
                                if (curPanel.ActualHeight + delimPanel.ActualHeight > freeHeight)
#else
                                if (panelHeight + _continuePanelHeight > freeHeight)
#endif
                                {
                                    curBlock = getNextItemsBlock(curPanel, true, true);
#if useUpdateLayout == false
                                    curBlockHeight = getBlockHeight(curBlock);
                                    panelHeight -= curBlockHeight;
#endif
                                    if (curBlock != null)
                                    {
                                        // нет элементов в тек.панели - удалить текущую панель и сделать текущей следующую панель на странице
                                        if (curPanel.ItemsCount == 0)
                                        {
                                            _canvas.Children.Remove(curPanel);
                                            if (_canvas.Children.Count > 0)
                                                curPanel = (OrderPanel)_canvas.Children[0];
                                            else
                                                curPanel = null;
                                        }
                                        // иначе вставляем разделитель переноса на пред.страницу
                                        else
                                        {
                                            curPanel.InsertDelimiter(0, delimPanel);
#if useUpdateLayout == false
                                            panelHeight += _continuePanelHeight;
#endif
                                        }
                                    }
                                    else
                                    {
                                        AppLib.WriteLogErrorMessage("layout order panels on the canvas: не могу удалить первый блок панели для размещения разделителя начала заказа на предыд.странице: " + orderLogInfo);
                                    }
                                }
                                else
                                {
                                    curPanel.InsertDelimiter(0, delimPanel);
#if useUpdateLayout == false
                                    panelHeight += _continuePanelHeight;
#endif
                                }

                                if (curPanel != null)
                                {
#if useUpdateLayout
                                    curPanel.UpdateLayout();
#endif
                                    // установить Top=0 для текущей панели
                                    curTopValue = 0d;
                                    setPanelLeftTop(curPanel);
                                }
                            }
                            // перенос панели на предыдущую страницу
                            _pageBreak = true;
                            break;
                        }
                        // из НЕпервого столбца - создаем новую панель
                        else
                        {
                            // в текущей панели нет элементов - переносим панель НЕ разбивая ее
                            if (curPanel.DishPanels.Count == 0)
                            {
                                // если в этой же колонке есть еще одна панель, то сдвигаем ее вверх
                                if (_canvas.Children.Count > 1)
                                {
                                    UIElement panel2 = _canvas.Children[1];
                                    int panel2ColIndex = ((OrderPanel)panel2).CanvasColumnIndex;
                                    if ((panel2ColIndex == curColIndex) && ((double)panel2.GetValue(Canvas.TopProperty) != 0d))
                                    {
                                        panel2.SetValue(Canvas.TopProperty, 0d);
                                    }
                                }
                                // добавить текущий блок элементов в переносимую панель
                                curPanel.InsertDishes(0, curBlock);
#if useUpdateLayout == false
                                panelHeight += curBlockHeight;
#endif
                            }

                            // размещение предыдущей панели по текущим координатам и создание новой панели
                            else
                            {
                                curTopValue = 0d;
                                setPanelLeftTop(curPanel);

                                // новая панель
                                curPanel = new OrderPanel(ordPanel.OrderViewModel, 0, ordPanel.Width, false);
                                curPanel.InsertDishes(0, curBlock);   // добавить текущий блок элементов
#if useUpdateLayout == false
                                panelHeight = ordPanel.DishTableHeaderHeight;
                                panelHeight += curBlockHeight;
#endif
                                _canvas.Children.Insert(0, curPanel);
                            }

                            // еще раз проверить перенос заголовка панели заказа
                            if ((ordPanel.ItemsCount == 0) && (curPanel.HeaderPanel == null) && (ordPanel.HeaderPanel != null))
                            {
                                OrderPanelHeader header = ordPanel.DetachHeader();
                                curPanel.HeaderPanel = header;
#if useUpdateLayout == false
                                panelHeight += curPanel.HeaderPanel.ActualHeight;
#endif
                            }
#if useUpdateLayout
                            curPanel.UpdateLayout();
#endif
                            curColIndex--; curTopValue = _colHeight;
                            freeHeight = getFreeHeight();
                        }
#endregion
                    }
                }
            }

            return;
        }

        private void setLastPanelPosition(OrderPanel curPanel, double panelHeight)
        {
            // при смещении назад, curTopValue указывает на Bottom панели
#if useUpdateLayout
            if (!_shiftForward) curTopValue -= curPanel.ActualHeight;
#else
            if (!_shiftForward) curTopValue -= panelHeight;
#endif
            setPanelLeftTop(curPanel);

            // при движение вперед, сместить Top на величину _hdrTopMargin
            if (_shiftForward)
            {
#if useUpdateLayout
                curTopValue += curPanel.ActualHeight;
#else
                curTopValue += panelHeight;
#endif

                curTopValue += _hdrTopMargin;
                if (curTopValue > _colHeight)
                {
                    curColIndex++; curTopValue = 0d;
                    // выход за последнюю колонку
                    if (curColIndex > _pageColsCount)
                    {
                        curColIndex = 1;  _pageBreak = true;
                    }
                }
            }

            // при движение назад
            else
            {
                curTopValue -= _hdrTopMargin;
                if (curTopValue <= 0d)
                {
                    curColIndex--;
                    curTopValue = _colHeight;

                }
                // выход за первую колонку
                if (curColIndex <= 0)
                {
                    if ((double)curPanel.GetValue(Canvas.TopProperty) != 0d) curPanel.SetValue(Canvas.TopProperty, 0d);
                    // curTopValue уже равно _colHeight;
                    curColIndex = _pageColsCount;
                    _pageBreak = true;
                }
            }
        }

        private void setPanelLeftTop(FrameworkElement panel)
        {
            panel.SetValue(Canvas.TopProperty, curTopValue);

            // сохранить в панели номер колонки
            if (panel is OrderPanel) ((OrderPanel)panel).CanvasColumnIndex = curColIndex;
            double left = ((curColIndex - 1) * _colWidth) + (curColIndex * _colMargin);
            panel.SetValue(Canvas.LeftProperty, left);
        }

        private double getFreeHeight()
        {
            return (_shiftForward) ? _colHeight - curTopValue : curTopValue;
        }

        private double getBlockHeight(List<UIElement> curBlock)
        {
            double retVal = 0d;
            foreach (UIElement elem in curBlock)
            {
                retVal += elem.RenderSize.Height;
            }
            return retVal;
        }

        private OrderPanel createOrderPanel(OrderViewModel orderModel, int dishIdxFrom, int dishIdxTo, bool isTailSplit)
        {
            if (dishIdxFrom > dishIdxTo) return null;

            OrderPanel ordPanel = new OrderPanel(orderModel, 0, _colWidth, (dishIdxFrom==0 ? true: false));
            // не с первого блюда - добавляем разделитель продолжения на предыд.странице
            if (dishIdxFrom != 0) ordPanel.AddDelimiter(createContinuePanel(false));

            string supplyName = WpfHelper.GetAppGlobalValue("DishesSupplyName", "подача").ToString();
            OrderDishViewModel dishModel;
            int curFiling = 0;
            for (int i = dishIdxFrom; i <= dishIdxTo; i++)
            {
                dishModel = orderModel.Dishes[i];
                // разделитель подач
                if (curFiling != dishModel.FilingNumber)
                {
                    curFiling = dishModel.FilingNumber;
                    Brush foreground = (curFiling == 1) ? Brushes.Red : Brushes.Blue;
                    DishDelimeterPanel newDelimPanel = new DishDelimeterPanel(_colWidth, foreground, Brushes.AliceBlue, supplyName + " " + curFiling.ToString()) { DontTearOffNext = true };
                    ordPanel.AddDelimiter(newDelimPanel);
                }
                // панель блюда
                DishPanel dishPanel = new DishPanel(dishModel, _colWidth);
                ordPanel.AddDish(dishPanel);
            }

            // оторванных хвост (dishIdxTo меньше кол-ва элементов) - добавляем разделитель продолжения на след.странице
            if (isTailSplit) ordPanel.AddDelimiter(createContinuePanel(true));

            return ordPanel;
        }

        // получить индекс колонки панели из Left
        internal int GetColumnIndex(UIElement panel)
        {
            if (this.OrderPanels.Contains(panel))
            {
                double leftValue = (double)panel.GetValue(Canvas.LeftProperty);
                return Convert.ToInt32((leftValue + _colWidth) / (_colWidth + _colMargin));
            }
            else
                return -1;
        }

        // возвращает признак повторного размещения при движении назад: 
        // много свободного места на панели временного размещения _canvas
        internal bool NeedRelayout()
        {
            // нет панелей - выход
            if (this.OrderPanels.Count == 0) return false;

            UIElement panel1 = this.OrderPanels[0];
            int panel1ColIndex = ((OrderPanel)panel1).CanvasColumnIndex;

            // только одна панель заказа и она в первой колонке - не переразмещаем
            if ((this.OrderPanels.Count == 1) && (panel1ColIndex == 1))
            {
                if ((double)panel1.GetValue(Canvas.TopProperty) != 0d) panel1.SetValue(Canvas.TopProperty, 0d);
                return false;
            }
            // а если первая панель не в первой колонке, то переразмещаем
            if (panel1ColIndex != 1)
                return true;
            // перед первой панелью есть пустое место
            else if ((double)((OrderPanel)panel1).GetValue(Canvas.TopProperty) != 0d)
                return true;

            bool retVal = false;
            // несколько панелей - цикл по панелям на странице
            // признак переразмещения: 
            // 1. свободное место по вертикали между соседними панелями в одной колонке больше _hdrTopMargin
            // 2. если панели одного заказа и следующая панель начинается со следующей колонки (или Top==0) и блок из следующей панели можно разместить в свободном месте в конце предыдущей колонки
            OrderPanel prePanel, nextPanel = (OrderPanel)_canvas.Children[0];
            double prePanelTop, prePanelHeight, nextPanelTop, freeSpace, h1;
            for (int i=1; i < _canvas.Children.Count; i++)
            {
                // две соседние панели
                prePanel = nextPanel; nextPanel = (OrderPanel)_canvas.Children[i];
                prePanelTop = (double)prePanel.GetValue(Canvas.TopProperty);
                prePanelHeight = prePanel.ActualHeight;

                // в одной колонке, это должны быть разные заказы
                if (prePanel.CanvasColumnIndex == nextPanel.CanvasColumnIndex)
                {
                    nextPanelTop = (double)nextPanel.GetValue(Canvas.TopProperty);
                    freeSpace = nextPanelTop - (prePanelTop + prePanelHeight);
                    if (freeSpace > _hdrTopMargin) { retVal = true; break;}
                }
                else if ((nextPanel.CanvasColumnIndex - prePanel.CanvasColumnIndex) > 1)
                {
                    retVal = true; break;
                }
                // разные колонки и один заказ
                else if (nextPanel.OrderViewModel == prePanel.OrderViewModel)
                {
                    freeSpace = _colHeight - (prePanelTop + prePanelHeight);
                    // высота первого блока следующей панели
                    List<UIElement> firstBlock = getNextItemsBlock(nextPanel, false, true);
                    h1 = getBlockHeight(firstBlock);
                    if (h1 <= freeSpace) { retVal = true;  break; }
                }
            }

            return retVal;
        }

        private DishDelimeterPanel createContinuePanel(bool isForward)
        {
            BrushesPair brPair = BrushHelper.AppBrushes["delimiterBreakPage"];
            string text = (isForward) 
                ? WpfHelper.GetAppGlobalValue("ContinueOrderNextPage", "see next page").ToString()
                : WpfHelper.GetAppGlobalValue("ContinueOrderPrevPage", "see prev page").ToString();
            DishDelimeterPanel newDelimPanel = new DishDelimeterPanel(_colWidth, brPair.Foreground, brPair.Background, text);
            return newDelimPanel;
        }

        // возвращает следующий/предыдущий блок элементов панели заказа
        // блок элементов - это блюдо с предварительным разделителем подач (точнее панель-разделитель, у которой свойство DontTearOffNext = true), 
        // или блюдо с ингредиентами, 
        // или заголовок заказа с несколькими первыми элементами (не менее 2-х)
        // или два ингредиента
        // isDetachItems == true - блоки отсоединяются от панели
        // forceForward - принудительный просмотр вперед, иначе направление движения зависит от _shiftForward
        private List<UIElement> getNextItemsBlock(OrderPanel orderPanel, bool isDetachItems, bool forceForward = false)
        {
            List<UIElement> retVal = new List<UIElement>();
            bool addToBlock, endBlock=false;
            int dishesCount = 0, delimCount = 0, ingrCount = 0; // счетчики элементов блока
            bool isForward = (forceForward) ? true : _shiftForward;
            int i = ((isForward) ? 0 : orderPanel.ItemsCount - 1);

            UIElement uiElem;
            while ((isDetachItems) ? orderPanel.ItemsCount > 0 : i < orderPanel.DishPanels.Count)
            {
                // в режиме отсоединения элементов берем граничный элемент
                if (isDetachItems) i = ((isForward) ? 0 : orderPanel.ItemsCount - 1);
                uiElem = orderPanel.DishPanels[i];
                // иначе увеличиваем индекс элемента
                if (!isDetachItems) ++i;
                addToBlock = true; // признак включения элемента в блок

                // разделитель
                if (uiElem is DishDelimeterPanel)
                {
                    if (((DishDelimeterPanel)uiElem).DontTearOffNext)  //  не отрывать от следующего
                    {
                        // считаем только неотрываемые разделители
                        delimCount++;
                        // при прямом проходе неотрываемый разделитель может быть только один
                        if (isForward)
                        {
                            if (delimCount > 1) { addToBlock = false; endBlock = true; } // не добавлять и выйти
                        }
                        // при обратном проходе
                        else
                        {
                            endBlock = true;  // добавить и выйти
                        }
                    }
                    // отрываемый разделитель, поведение, как у блюда, но без ингредиентов, поэтому он может быть только один
                    // как в прямом, так и в обратном направлениях
                    else
                    {
                        endBlock = true;
                    }
                }

                // блюдо
                else if (uiElem is DishPanel)
                {
                    DishPanel dsPnl = (uiElem as DishPanel);
                    // блюдо
                    if (dsPnl.DishView.ParentUID.IsNull())
                    {
                        dishesCount++;
                        // при прямом проходе блюдо может быть только одно
                        if (isForward)
                        {
                            // уже был ингредиент или блюдо - не добавлять и выйти
                            if ((ingrCount > 0) || (dishesCount > 1))
                            {
                                addToBlock = false; endBlock = true;
                            }
                        }
                        // при обратном проходе перед блюдом может быть разделитель подач
                        else
                        {
                            // не добавлять и выйти
                            if (dishesCount > 1)
                            {
                                addToBlock = false; endBlock = true;
                            }
                        }
                    }
                    // ингредиент, из ингредиентов делаем блоки по 2 элемента, если перед ними не было блюда
                    else
                    {
                        if (isForward)
                        {
                            ingrCount++;
                            // не добавлять больше 2-х ингредиентов
                            if (ingrCount > 2) { addToBlock = false; endBlock = true; }
                        }
                        else
                        {
                            if (dishesCount == 0)
                            {
                                ingrCount++;
                                // не добавлять больше 2-х ингредиентов
                                if (ingrCount > 2) { addToBlock = false; endBlock = true; }
                            }
                            else // не добавлять и выйти
                            {
                                addToBlock = false; endBlock = true;
                            }
                        }
                    }
                }

                if (addToBlock)
                {
                    retVal.Add(uiElem);
                    // и отсоединить от панели заказа
                    if (isDetachItems) orderPanel.DetachDish(uiElem);
                }
                if (endBlock) break;
            } // for next

            if (retVal.Count == 0) retVal = null;

            return retVal;
        }

    }  // class

    // граница заказа для определения того, обрезался ли заказ при размещении на странице
    public struct OrderModelEdge
    {
        public int OrderIndex { get; set; }
        public int DishIndex { get; set; }
    }

}
