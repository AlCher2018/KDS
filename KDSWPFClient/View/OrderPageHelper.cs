using IntegraLib;
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
        private int _pageColsCount;
        private double _colWidth, _colHeight, _colMargin;
        private double _hdrTopMargin;
        private double _ordHeaderMinHeight, _continuePanelHeight;
        private bool _shiftForward;

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
            _pageColsCount = Convert.ToInt32(AppPropsHelper.GetAppGlobalValue("OrdersColumnsCount"));
            _colWidth = Convert.ToDouble(AppPropsHelper.GetAppGlobalValue("OrdersColumnWidth"));
            _colMargin = Convert.ToDouble(AppPropsHelper.GetAppGlobalValue("OrdersColumnMargin"));
            _hdrTopMargin = Convert.ToDouble(AppPropsHelper.GetAppGlobalValue("OrderPanelTopMargin"));
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

        /// <summary>
        /// Создает и размещает на одной странице панели заказов (тип OrderPanel).
        /// </summary>
        /// <param name="orders">Список заказов (тип OrderViewModel)</param>
        /// <param name="orderIndex">Индекс заказа, начиная с которого будут строиться панели</param>
        /// <param name="dishIndex">Индекс блюда, начиная с которогу будут строиться панели</param>
        /// <param name="isPanelsForward">Признак того, что коллекция orders будет листаться вперед, от стартовой позиции к концу набора</param>
        /// <returns></returns>
        internal void DrawOrderPanelsOnPage(List<OrderViewModel> orders, int orderStartIndex, int dishStartIndex, bool isPanelsForward)
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

                OrderPanel ordPanel = createOrderPanel(orderModel, dishIdxFrom, dishIdxTo, bSplit);
                // получить реальные размеры панели заказа
                if (_shiftForward) _canvas.Children.Add(ordPanel);
                else _canvas.Children.Insert(0, ordPanel);
                ordPanel.UpdateLayout();
                dishStartIndex = -1;  // все блюда

                // 2. Размещение панели на странице
                // помещается ли вся панель заказа без разрывов в свободное место
                if (ordPanel.ActualHeight < freeHeight)
                {
                    setPanelLeftTop(ordPanel);
                    if (_shiftForward)
                    {
                        curTopValue += ordPanel.ActualHeight;
                        curTopValue += _hdrTopMargin;
                    }
                    else
                    {
                        curTopValue -= ordPanel.ActualHeight;
                        curTopValue -= _hdrTopMargin;
                    }
                }
                // надо разбивать панель
                else
                {
                    _canvas.Children.Remove(ordPanel);
                    splitOrderViewPanels(ordPanel);
                }

                freeHeight = getFreeHeight();
            }

            // в обратном направлении для первого заказа curTopValue = 0
            //if (!_shiftForward && (_canvas.Children.Count > 0)) _canvas.Children[0].SetValue(Canvas.TopProperty, 0d);
        }

        // преобразование сплошной панели в коллекцию панелей по колонкам
        // панели записываются на канву временного размещения
        // входные параметры: 
        //  - OrderPanel ordPanel // сплошная панель, которую надо разбить по колонкам 
        private void splitOrderViewPanels(OrderPanel ordPanel)
        {
            OrderPanel curPanel=null;
            string orderLogInfo = string.Format("id: {0}, index: {1}, number: {2}, dishes: {3}", ordPanel.OrderViewModel.Id, ordPanel.OrderViewModel.Index, ordPanel.OrderViewModel.Number, ordPanel.OrderViewModel.Dishes.Count);

            // цикл по блокам заказа, смещая curDishIndex, пока curDishIndex не дойдет до граничного значения в соотв.напр-и
            double freeHeight = getFreeHeight();
            double curBlockHeight;
            List<UIElement> curBlock;

            while (true)
            {
                curBlock = getNextItemsBlock(ordPanel);
                // выбрали все блоки - установить координаты последней панели и выйти
                if (curBlock == null)
                {
                    // при смещении назад, curTopValue указывает на Bottom панели
                    if (!_shiftForward) curTopValue -= curPanel.ActualHeight;
                    setPanelLeftTop(curPanel);

                    #region смещение координат
                    // сместить Top на величину _hdrTopMargin
                    if (_shiftForward)
                    {
                        curTopValue += curPanel.ActualHeight;
                        curTopValue += _hdrTopMargin;
                        if (curTopValue > _colHeight) { curColIndex++; curTopValue = 0d; }
                    }
                    else
                    {
                        curTopValue -= _hdrTopMargin;
                        if (curTopValue <= 0d)
                        {
                            curColIndex--;
                            curTopValue = _colHeight;
                            
                        }
                        if ((curColIndex <= 1) && ((double)curPanel.GetValue(Canvas.TopProperty) != 0d))
                        {
                            curPanel.SetValue(Canvas.TopProperty, 0d);
                            curTopValue = 0d;
                        }
                    }
                    #endregion
                    break;  // нормальный выход - выбраны все элементы панели
                }

                // создать панель
                if (curPanel == null)
                {
                    curPanel = new OrderPanel(ordPanel.OrderViewModel, 0, ordPanel.Width, false);
                    if (_shiftForward)
                        _canvas.Children.Add(curPanel);
                    else
                    {
                        // перед добавлением новой панели заказа, установить принудительно для первой панели Top=0
                        if ((_canvas.Children.Count > 0) && ((double)_canvas.Children[0].GetValue(Canvas.TopProperty) != 0d))
                        {
                            //_canvas.Children[0].SetValue(Canvas.TopProperty, 0d);
                        }
                        _canvas.Children.Insert(0, curPanel);
                    }
                    curPanel.UpdateLayout();
                }

                // добавить заголовок заказа в панель, если при прямом проходе блок самый первый 
                // иначе - добавлять в панель на последнем блоке из ordPanel
                if (_shiftForward && (curPanel.HeaderPanel == null) && (ordPanel.HeaderPanel != null))
                {
                    OrderPanelHeader header = ordPanel.DetachHeader();
                    curPanel.HeaderPanel = header;
                    curPanel.UpdateLayout();
                }

                curBlockHeight = getBlockHeight(curBlock);
                // при обратном проходе и отсутствии элементов в ordPanel, к curBlockHeight прибавить высоту заголовка
                if (!_shiftForward && (ordPanel.ItemsCount == 0)) curBlockHeight += ordPanel.HeaderHeight;

                // анализ размещения блока в свободном месте
                if (curPanel.ActualHeight + curBlockHeight <= freeHeight)
                {
                    if (_shiftForward)
                        curPanel.AddDishes(curBlock);        // добавить текущий блок элементов к панели заказа
                    else
                    {
                        curPanel.InsertDishes(0, curBlock);  // вставить блок в начало коллекции
                        // и не забываем про заголовок заказа при обратном проходе
                        if (ordPanel.ItemsCount == 0)
                        {
                            OrderPanelHeader header = ordPanel.DetachHeader();
                            curPanel.HeaderPanel = header;
                        }
                    }
                    curPanel.UpdateLayout();
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
                            if (curPanel.HeaderPanel == null)
                            {
                                // добавляем в конце разделитель продолжения заказа
                                DishDelimeterPanel delimPanel = createContinuePanel(true);
                                // удалить последний блок
                                if (curPanel.ActualHeight + delimPanel.ActualHeight > freeHeight)
                                {
                                    _shiftForward = false; // смещаясь назад
                                    curBlock = getNextItemsBlock(curPanel); // поиск блока и удаление его
                                    _shiftForward = true; // восстановить смещение
                                    if (curBlock != null)
                                    {
                                        curPanel.AddDelimiter(delimPanel);
                                    }
                                    else
                                    {
                                        AppLib.WriteLogErrorMessage("layout order panels on the canvas: не могу удалить последний блок панели для размещения разделителя продолжения заказа на след.странице: " +  orderLogInfo);
                                    }
                                }
                                else
                                    curPanel.AddDelimiter(delimPanel);
                                curPanel.UpdateLayout();
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
                            if ((curPanel.DishPanels.Count == 0) || (curPanel.DishPanelsCount() == 1))
                            {
                                curPanel.AddDishes(curBlock);   // добавить текущий блок элементов
                                curPanel.UpdateLayout();
                                curColIndex++; curTopValue = 0d;
                            }
                            // размещение предыдущей панели по текущим координатам и создание новой панели
                            else
                            {
                                setPanelLeftTop(curPanel);
                                // новая панель
                                curPanel = new OrderPanel(ordPanel.OrderViewModel, 0, ordPanel.Width, false);
                                curPanel.AddDishes(curBlock);   // добавить текущий блок элементов
                                _canvas.Children.Add(curPanel);
                                curPanel.UpdateLayout();
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
                            // если не помещается хвост из менее, чем 3 элемента
                            // или не помещается в 1/3 часть колонки, то переносим всю панель
                            if ((ordPanel.ItemsCount < 3) || (freeHeight <= (_colHeight / 3d)))
                            {
                                _canvas.Children.Remove(curPanel);
                                // а первую панель подтягиваем кверху
                                _canvas.Children[0].SetValue(Canvas.TopProperty, 0d);
                            }
                            // разбиваем панель, добавляем в начало разделитель продолжения заказа
                            else
                            {
                                DishDelimeterPanel delimPanel = createContinuePanel(false);
                                // удалить первый блок
                                if (curPanel.ActualHeight + delimPanel.Height > freeHeight)
                                {
                                    _shiftForward = true; // смещаясь вперед
                                    curBlock = getNextItemsBlock(ordPanel);
                                    _shiftForward = false; // восстановить смещение
                                    if (curBlock != null)
                                    {
                                        curPanel.InsertDelimiter(0, delimPanel);
                                    }
                                    else
                                    {
                                        AppLib.WriteLogErrorMessage("layout order panels on the canvas: не могу удалить первый блок панели для размещения разделителя начала заказа на предыд.странице: " + orderLogInfo);
                                    }
                                }
                                else
                                    curPanel.InsertDelimiter(0, delimPanel);
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
                                    int panel2ColIndex = GetColumnIndex(panel2);
                                    if ((panel2ColIndex == curColIndex) && ((double)panel2.GetValue(Canvas.TopProperty) != 0d))
                                    {
                                        panel2.SetValue(Canvas.TopProperty, 0d);
                                    }
                                }
                                // добавить текущий блок элементов в переносимую панель
                                curPanel.InsertDishes(0, curBlock);
                                curPanel.UpdateLayout();
                                curColIndex--; curTopValue = _colHeight;
                            }
                            // размещение предыдущей панели по текущим координатам и создание новой панели
                            else
                            {
                                curTopValue = 0d;
                                setPanelLeftTop(curPanel);
                                // новая панель
                                curPanel = new OrderPanel(ordPanel.OrderViewModel, 0, ordPanel.Width, false);
                                curPanel.InsertDishes(0, curBlock);   // добавить текущий блок элементов
                                _canvas.Children.Insert(0, curPanel);
                                // еще раз проверить перенос заголовка панели заказа
                                if ((ordPanel.ItemsCount == 0) && (curPanel.HeaderPanel == null) && (ordPanel.HeaderPanel != null))
                                {
                                    OrderPanelHeader header = ordPanel.DetachHeader();
                                    curPanel.HeaderPanel = header;
                                }
                                curPanel.UpdateLayout();
                                // координаты предыдущего столбца
                                curColIndex--; curTopValue = _colHeight;
                            }
                            freeHeight = getFreeHeight();
                        }
                        #endregion
                    }
                }
            }

            return;
        }

        private void setPanelLeftTop(FrameworkElement panel)
        {
            panel.SetValue(Canvas.TopProperty, curTopValue);
            panel.SetValue(Canvas.LeftProperty, getLeftProperty());
        }

        private double getFreeHeight()
        {
            return (_shiftForward) ? _colHeight - curTopValue : curTopValue;
        }

        private double getPanelHeight(OrderPanel panel)
        {
            panel.UpdateLayout();

            return panel.ActualHeight;
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
            OrderPanel ordPanel = new OrderPanel(orderModel, 0, _colWidth, (dishIdxFrom==0 ? true: false));
            // не с первого блюда - добавляем разделитель продолжения на предыд.странице
            if (dishIdxFrom != 0) ordPanel.AddDelimiter(createContinuePanel(false));

            string supplyName = AppPropsHelper.GetAppGlobalValue("DishesSupplyName", "подача").ToString();
            OrderDishViewModel dishModel;
            int curFiling = 0;
            for (int i = dishIdxFrom; i <= dishIdxTo; i++)
            {
                dishModel = orderModel.Dishes[i];
                // разделитель подач
                if (curFiling != dishModel.FilingNumber)
                {
                    curFiling = dishModel.FilingNumber;
                    DishDelimeterPanel newDelimPanel = new DishDelimeterPanel()
                    { Text = supplyName + " " + curFiling.ToString(), DontTearOffNext = true, Background = Brushes.AliceBlue };
                    newDelimPanel.Foreground = (curFiling == 1) ? Brushes.Red : Brushes.Blue;
                    ordPanel.AddDelimiter(newDelimPanel);
                }
                // панель блюда
                DishPanel dishPanel = new DishPanel(dishModel);
                ordPanel.AddDish(dishPanel);
            }

            // оторванных хвост (dishIdxTo меньше кол-ва элементов) - добавляем разделитель продолжения на след.странице
            if (isTailSplit) ordPanel.AddDelimiter(createContinuePanel(true));

            return ordPanel;
        }

        // получить Left из индекса колонки
        private double getLeftProperty()
        {
            return ((curColIndex - 1) * _colWidth) + (curColIndex * _colMargin);
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

        // признак повторного размещения: первая панель не в первой колонке или много места в первой колонке
        internal bool NeedRelayout()
        {
            // нет панелей - выход
            if (this.OrderPanels.Count == 0) return false;

            UIElement panel1 = this.OrderPanels[0];
            int panel1ColIndex = GetColumnIndex(panel1);

            // только одна панель в первой колонке - не переразмещаем
            if ((this.OrderPanels.Count == 1) && (panel1ColIndex == 1))
            {
                if ((double)panel1.GetValue(Canvas.TopProperty) != 0d) panel1.SetValue(Canvas.TopProperty, 0d);
                return false;
            }

            // первая панель не в первой колонке - переразмещаем
            if (panel1ColIndex != 1) return true;

            // в первой колонке более одной панели - НЕ переразмещаем
            UIElement panel2 = this.OrderPanels[1];
            int panel2ColIndex = GetColumnIndex(panel2);
            if (panel2ColIndex == 1) return false;

            // в первой колонке только одна панель - находим свободное место
            if (Convert.ToDouble(panel1.GetValue(Canvas.TopProperty)) != 0d) panel1.SetValue(Canvas.TopProperty, 0d);
            double freeCol1Height = _colHeight - ((OrderPanel)panel1).ActualHeight;
            if (freeCol1Height > (_colHeight / 2d))
                return true;
            else
                return false;
        }


        private DishDelimeterPanel createContinuePanel(bool isForward)
        {
            string text = (isForward) 
                ? AppPropsHelper.GetAppGlobalValue("ContinueOrderNextPage", "see next page").ToString()
                : AppPropsHelper.GetAppGlobalValue("ContinueOrderPrevPage", "see prev page").ToString();
            DishDelimeterPanel newDelimPanel = new DishDelimeterPanel()
            {
                Text = text,
                Background = BrushHelper.AppBrushes["delimiterBreakPage"].Background,
                Foreground = BrushHelper.AppBrushes["delimiterBreakPage"].Foreground
            };

            // измерить высоту панели, т.е. получить ActualHeight
            _canvas.Children.Add(newDelimPanel);
            newDelimPanel.UpdateLayout();
            _canvas.Children.Remove(newDelimPanel);

            return newDelimPanel;
        }

        // возвращает следующий/предыдущий блок элементов панели заказа
        // блок элементов - это блюдо с предварительным разделителем подач (точнее панель-разделитель, у которой свойство DontTearOffNext = true), 
        // или блюдо с ингредиентами, 
        // или заголовок заказа с несколькими первыми элементами (не менее 2-х)
        // или два ингредиента
        // и сразу их отсоединяет!!
        private List<UIElement> getNextItemsBlock(OrderPanel orderPanel)
        {
            List<UIElement> retVal = new List<UIElement>();
            bool addToBlock, endBlock=false;
            int dishesCount = 0, delimCount = 0, ingrCount = 0; // счетчики элементов блока
            
            UIElement uiElem;
            while (orderPanel.ItemsCount > 0)
            {
                int i = ((_shiftForward) ? 0 : orderPanel.ItemsCount - 1);
                uiElem = orderPanel.DishPanels[i];
                addToBlock = true; // признак включения элемента в блок

                // разделитель
                if (uiElem is DishDelimeterPanel)
                {
                    if (((DishDelimeterPanel)uiElem).DontTearOffNext)  //  не отрывать от следующего
                    {
                        // считаем только неотрываемые разделители
                        delimCount++;
                        // при прямом проходе неотрываемый разделитель может быть только один
                        if (_shiftForward)
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
                        if (_shiftForward)
                        {
                            // уже был ингредиент или блюдо - не добавлять и выйти
                            if ((ingrCount > 0) || (dishesCount > 1))
                            {
                                addToBlock = false; endBlock = true;
                            }
                        }
                        // при обратном проходе перед блюдом может быть разделитель подач
                        else if (dishesCount > 1)
                        {
                            // не добавлять и выйти
                            addToBlock = false; endBlock = true;
                        }
                    }
                    // ингредиент, из ингредиентов делаем блоки по 2 элемента, если перед ними не было блюда
                    else
                    {
                        if (_shiftForward)
                        {
                            ingrCount++;
                            if (ingrCount == 2) endBlock = true;
                        }
                        else
                        {
                            if (dishesCount == 0)
                            {
                                ingrCount++;
                                if (ingrCount == 2) endBlock = true;
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
                    orderPanel.DetachDish(uiElem);
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
