using IntegraLib;
using KDSWPFClient.Lib;
using KDSWPFClient.ViewModel;
using System;
using System.Collections;
using System.Collections.Generic;
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
        private double _colWidth, _colMargin;
        private double _hdrTopMargin;
        private double _ordHeaderMinHeight, _continuePanelHeight;

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
        /// Возвращает список панелей заказов (тип OrderPanel) для одной страницы.
        /// </summary>
        /// <param name="orders">Список заказов (тип OrderViewModel)</param>
        /// <param name="orderIndex">Индекс заказа, начиная с которого будут строиться панели</param>
        /// <param name="dishIndex">Индекс блюда, начиная с которогу будут строиться панели</param>
        /// <param name="isPanelsForward">Признак того, что коллекция orders будет листаться вперед, от стартовой позиции к концу набора</param>
        /// <returns></returns>
        internal void DrawOrderPanelsOnPage(List<OrderViewModel> orders, int orderStartIndex, int dishStartIndex, bool isPanelsForward)
        {
            if (orders == null) return;
            if (orderStartIndex >= orders.Count) return;

            System.Diagnostics.Debug.Print("** начальные индексы: order {0}, dish {1}", orderStartIndex, dishStartIndex);
            OrderViewModel orderModel;

            #region найти след/предыд индексы заказ/блюдо, с которых начинается создание панелей
            // найти след/предыд индексы заказ/блюдо, с которых начинается создание панелей
            // прямое направление
            if (isPanelsForward)
            {
                if (orderStartIndex < 0)
                {
                    orderStartIndex = 0; dishStartIndex = -1;
                }
                else if (dishStartIndex >= orders[orderStartIndex].Dishes.Count-1)
                {
                    orderStartIndex++; dishStartIndex = -1;
                    if (orderStartIndex >= orders.Count) return;
                }
                else
                {
                    dishStartIndex++;
                    orderModel = orders[orderStartIndex];
                    // пропустить "назад" ингредиенты - блюда, у которых не пустой ParentUID
                    while (!orderModel.Dishes[dishStartIndex].ParentUID.IsNull() && (dishStartIndex > 0)) dishStartIndex--;
                }
            }
            // в обратном направлении
            else
            {
                if (dishStartIndex == 0)
                {
                    orderStartIndex--; dishStartIndex = -1;
                    if (orderStartIndex < 0) return;
                }
                else
                {
                    dishStartIndex--;
                    // пропустить "вперед" ингредиенты - блюда, у которых не пустой ParentUID
                    orderModel = orders[orderStartIndex];
                    while (!orderModel.Dishes[dishStartIndex].ParentUID.IsNull() && (dishStartIndex < (orderModel.Dishes.Count - 1))) dishStartIndex++;
                }
            }
            #endregion

            //List<OrderPanel> orderPanels = new List<OrderPanel>();
            int dishIdxFrom=-1, dishIdxTo=-1;

            _canvas.Children.Clear();
            // текущий столбец, с 1
            int curColIndex = (isPanelsForward) ? 1 : _pageColsCount;
            // текущая координата Top
            double curTopValue = (isPanelsForward) ? 0 : _canvas.ActualHeight;
            // высота свободного места в столбце
            double allowedHeight = _canvas.ActualHeight;
            bool isPageBreak = false;

            // цикл по заказам
            for (int iOrd = orderStartIndex;
                (isPanelsForward) ? iOrd < orders.Count : iOrd >= 0;
                iOrd += (isPanelsForward) ? 1 : -1)
            {
                // текущий заказ
                orderModel = orders[iOrd];

                // 1. создать панель заказа со всеми блюдами и разделителями подач
                #region текущие начальный и конечный индексы блюд
                //   для последующих заказов - все блюда
                if (dishStartIndex == -1) { dishIdxFrom = 0; dishIdxTo = orderModel.Dishes.Count - 1; }
                //   для первого заказа - часть блюд
                else
                {
                    if (isPanelsForward)
                    {
                        dishIdxFrom = dishStartIndex;
                        dishIdxTo = orderModel.Dishes.Count - 1;
                    }
                    else
                    {
                        dishIdxFrom = 0;
                        dishIdxTo = dishStartIndex;
                    }
                }
                #endregion
                OrderPanel ordPanel = createOrderPanel(orderModel, dishIdxFrom, dishIdxTo);
                // получить реальные размеры панели заказа
                _canvas.Children.Add(ordPanel);
                _canvas.UpdateLayout();
                dishStartIndex = -1;  // все блюда

                // 2. метод преобразования сплошной панели в коллекцию панелей по колонкам
                // входные параметры: 
                //  - OrderPanel plainOrderPanel // сплошная панель, которую надо разбить по колонкам 
                //  - double allowedHeight // высота колонки
                //  - double curTopValue   // высота (Top), с которой начинается отрисовка исходной панели
                //  - bool isPanelsForward // направление чтения элементов сплошной панели
                // возвращаемое значение:
                //  - List<OrderPanel> // коллекция панелей по колонкам
                List<OrderPanel> ordViewPanels = getOrderViewPanels(ordPanel, allowedHeight, curTopValue, isPanelsForward);

                // 3. размещение коллекции панелей на канве с определением выхода за пределы страницы и установкой индексов начальных/конечных заказа/элемента, если выходим за границы, то обрываем цикл, иначе продолжаем создание и размещение панелей заказов с новыми значениями текущего столбца (curColIndex) и смещения сверху (curTopValue)
                #region draw ordViewPanels
                OrderPanel viewPanel =null;
                if (isPanelsForward)
                {
                    for (int i = 0; i < ordViewPanels.Count; i++)
                    {
                        viewPanel = ordViewPanels[i];
                        viewPanel.SetPosition(curTopValue, getLeftOrdPnl(curColIndex));

                        // выход за пределы страницы
                        if (curColIndex + 1 > _pageColsCount) { isPageBreak = true; break; }
                        else { curColIndex++; curTopValue = 0d; }

                        // смещение по вертикали для следующего заказа
                        curTopValue += viewPanel.ActualHeight + _hdrTopMargin;
                    }
                }
                else
                {
                    for (int i = ordViewPanels.Count-1; i >=0 ; i--)
                    {
                        if (i == 0) curTopValue = allowedHeight - viewPanel.ActualHeight;
                        else curTopValue = 0d;

                        viewPanel = ordViewPanels[i];
                        viewPanel.SetPosition(curTopValue, getLeftOrdPnl(curColIndex));

                        // выход за пределы страницы
                        if ((curColIndex - 1) < 1) { isPageBreak = true; break; }
                        else curColIndex--;

                        // смещение по вертикали для следующего заказа
                        if (i == 0) curTopValue -= _hdrTopMargin;
                    }
                }

                // выход за пределы страницы - добавить разделит.панель продолжения
                if (isPageBreak && (viewPanel != null))
                {
                    DishDelimeterPanel delimPanel = createContinuePanel(isPanelsForward);

                    // помещается в колонку - добавить разделитель продолжения
                    if (viewPanel.ActualHeight + _continuePanelHeight <= allowedHeight)
                    {
                        if (isPanelsForward) viewPanel.AddDelimiter(delimPanel); // в конце панели
                        else viewPanel.DishPanels.Insert(0, delimPanel); // в начале панели
                    }
                    // убрать блок и добавить разделитель продолжения
                    else
                    {
                        List<UIElement> delBlock;
                        if (isPanelsForward)  // последний блок
                            delBlock = getOrderPanelItemsBlock(viewPanel, viewPanel.DishPanels.Count, false);
                        else                  // первый блок
                            delBlock = getOrderPanelItemsBlock(viewPanel, -1, true);
                        if (delBlock.Count > 0)
                        {
                            delBlock.ForEach(e => viewPanel.DishPanels.Remove(e));
                            viewPanel.AddDelimiter(delimPanel);
                        }
                    }
                    break;
                }
                #endregion

                //orderPanels.Add(ordPanel);
                System.Diagnostics.Debug.Print("** order idx - " + iOrd.ToString());
            }
        }


        #region temp
        // размещение панели в столбцах полотна
        // 1. цикл по блокам панели заказа (вверх или вниз)
        // (блок элементов - это блюдо с предварительным разделителем подач (точнее панель-разделитель, у которой свойство DontTearOffNext = true), или блюдо с ингредиентами, или заголовок заказа с несколькими первыми элементами (не менее 2-х))
        //  - получение следующего/предыдущего блока элементов
        //  - если текущая высота отобранных элементов плюс высота полученного блока меньше свободной высоты в текущей колонке, то добавляем блок к отобранным элементам,
        //    иначе к блоку добавляется простой заголовок заказа (только шапка таблицы) и этот блок начинает новую панель экрана

        //OrderPanel viewPanel;
        ////   цикл по элементам панели заказа
        //double curHt = 0d; // текущая высота выбранных элементов
        //int curIdx = 0;  // текущий индекс элемента стека
        //if (isPanelsForward)
        //{

        //}
        //else
        //{
        //    // подымаемся кверху от последнего элемента
        //    curIdx = ordPanel.ItemsCount;
        //    curHt = 0d;
        //    while (curHt + _ordHeaderMinHeight < _canvas.ActualHeight)
        //    {
        //        curIdx--;
        //        curHt += ordPanel.DishPanels[curIdx].RenderSize.Height;

        //        // проверить допустимость предыдущего блока, начать от текущих значений
        //        double tmpHt = curHt;
        //        UIElement uiElem; bool bl;
        //        int tmpIdx = curIdx - 1;
        //        for (; tmpIdx >= 0; tmpIdx--)
        //        {
        //            uiElem = ordPanel.DishPanels[tmpIdx];
        //            // условия вхождения элементов в блок
        //            // - это разделитель (номер подачи)
        //            bl = ((uiElem is DishDelimeterPanel) && ((DishDelimeterPanel)uiElem).DontTearOffNext);
        //            // - или это ингредиент
        //            if ((!bl) && (uiElem is DishPanel))
        //            {
        //                DishPanel dsPnl = (uiElem as DishPanel);
        //                bl = !dsPnl.DishView.ParentUID.IsNull();
        //            }
        //            // или это позиция менее 2
        //            if (!bl && (tmpIdx <= 2))
        //            {
        //                // а в первой позиции невисячий разделитель?
        //                uiElem = ordPanel.DishPanels[0];
        //                if ((uiElem is DishDelimeterPanel) && ((DishDelimeterPanel)uiElem).DontTearOffNext) bl = true;
        //            }

        //            if (bl)
        //                tmpHt += uiElem.RenderSize.Height;
        //            else
        //                break;
        //        }
        //        // дошли до самого верха - добавить высоту заголовка заказа
        //        if (tmpIdx == -1) tmpHt += ordPanel.HeaderHeight;

        //        // можно ли добавить этот блок?
        //        if (tmpHt + _ordHeaderMinHeight <= _canvas.ActualHeight)
        //        {
        //            curIdx = tmpIdx;
        //            curHt = tmpHt;
        //        }
        //        // блок добавить нельзя - перенос в предыдущий столбец
        //        else
        //        {

        //        }
        //    }  // while
        //} // if (isPanelsForward) else
        #endregion

        // 2. метод преобразования сплошной панели в коллекцию панелей по колонкам
        // входные параметры: 
        //  - OrderPanel plainOrderPanel // сплошная панель, которую надо разбить по колонкам 
        //  - double allowedHeight // высота колонки
        //  - double curTopValue   // высота (Top), с которой начинается отрисовка исходной панели
        //  - bool isPanelsForward // направление чтения элементов сплошной панели
        // возвращаемое значение:
        //  - List<OrderPanel> // коллекция панелей по колонкам
        private List<OrderPanel> getOrderViewPanels(OrderPanel ordPanel, double allowedHeight, double curTopValue, bool isPanelsForward)
        {
            List<OrderPanel> retVal = new List<OrderPanel>();

            if (isPanelsForward)
            {
                int curIndex = 0;  // индекс позиции в текущей панели
                if (curTopValue > 0) curTopValue += _hdrTopMargin;
                
                // сместиться на высоту заголовка
                curTopValue += ordPanel.HeaderHeight;
                // читать блоки элементов, пока не прочитаем пустую коллекцию, начиная с нулевого индекса
                List<UIElement> curBlock = getOrderPanelItemsBlock(ordPanel, -1, true);
                while (curBlock.Count > 0)
                {
                    double blockHeight = getBlockHeight(curBlock);
                    // с текущим блоком выходим за нижнюю границу?
                    if (curTopValue + blockHeight > allowedHeight)
                    {
                        // создать новую панель
                        OrderPanel newPanel = new OrderPanel(ordPanel.OrderViewModel, 0, _colWidth, false);
                        // добавить в нее текущий блок
                        foreach (UIElement elem in curBlock) newPanel.DishPanels.Add(elem);
                        curIndex = curBlock.Count;
                    }
                    else
                    {
                        curIndex += 
                    }
                }

            }
            else
            {

            }

            return retVal;
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

        private OrderPanel createOrderPanel(OrderViewModel orderModel, int dishIdxFrom, int dishIdxTo)
        {
            OrderPanel ordPanel = new OrderPanel(orderModel, 0, _colWidth, (dishIdxFrom==0 ? true: false));
            // первый заказ не с первого блюда - добавляем разделитель продолжения
            if (dishIdxFrom != 0)  
            {
                ordPanel.AddDelimiter(createContinuePanel(false));
            }

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
                    { Text = "Подача " + curFiling.ToString(), DontTearOffNext = true };
                    newDelimPanel.Foreground = (curFiling == 1) ? Brushes.Red : Brushes.Blue;
                    ordPanel.AddDelimiter(newDelimPanel);
                }
                // панель блюда
                DishPanel dishPanel = new DishPanel(dishModel);
                ordPanel.AddDish(dishPanel);
                System.Diagnostics.Debug.Print("**   dish index - " + i.ToString());
            }

            // первый заказ не до последнего блюда - добавляем разделитель продолжения
            if (dishIdxFrom != 0)
            {
                ordPanel.AddDelimiter(createContinuePanel(true));
            }

            return ordPanel;
        }

        private double getLeftOrdPnl(int curColIndex)
        {
            return ((curColIndex - 1) * _colWidth) + (curColIndex * _colMargin);
        }

        private DishDelimeterPanel createContinuePanel(bool isForward)
        {
            string text = (isForward) 
                ? "Продолж. см.на СЛЕДУЮЩЕЙ стр."
                : "Начало см.на ПРЕДЫДУЩЕЙ стр.";
            DishDelimeterPanel newDelimPanel = new DishDelimeterPanel()
            {
                Text = text,
                Foreground = Brushes.DarkMagenta
            };
            return newDelimPanel;
        }

        // возвращает следующий/предыдущий блок элементов панели заказа
        // блок начинается или с блюда, или с невисячего разделителя
        private List<UIElement> getOrderPanelItemsBlock(OrderPanel orderPanel, int curItemIndex, bool isRunForward)
        {
            List<UIElement> retVal = new List<UIElement>();
            bool isBlock, isDish=false, preDish = false;

            UIElement uiElem;
            for (int i = curItemIndex + (isRunForward?1:-1);
                (isRunForward) ? i < orderPanel.DishPanels.Count : i >= 0; 
                i += (isRunForward)?1:-1)
            {
                uiElem = orderPanel.DishPanels[i];
                isBlock = true;

                // разделитель
                if (uiElem is DishDelimeterPanel)
                {
                    if (((DishDelimeterPanel)uiElem).DontTearOffNext)  //  не отрывать от следующего
                    {
                        if (!isRunForward) isDish = true;  // при обратном проходе - выход
                    }
                    // отрывать, поведение, как у блюда
                    else
                    {
                        if (isRunForward) { if (i > 1) isDish = true; }
                        else { isDish = true; }
                    }
                }

                // блюдо
                else if (uiElem is DishPanel)
                {
                    DishPanel dsPnl = (uiElem as DishPanel);
                    if (dsPnl.DishView.ParentUID.IsNull())
                    {
                        if (isRunForward)
                        {
                            // сохраняем в выходной коллекции и выходим
                            if (i > 1) isDish = true;
                        }
                        else
                        {
                            if (!preDish) preDish = true;
                            else { isBlock = false; isDish = true; }
                        }
                    }
                }

                if (isBlock) retVal.Add(uiElem);
                if (isDish) break;
            }

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
