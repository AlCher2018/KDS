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

            #region найти след/предыд индексы заказ/блюдо, с которых начинается создание панелей
            // найти след/предыд индексы заказ/блюдо, с которых начинается создание панелей
            // прямое направление
            if (isPanelsForward)
            {
                if (orderStartIndex < 0) orderStartIndex = 0;
                if (dishStartIndex >= orders[orderStartIndex].Dishes.Count-1)
                {
                    orderStartIndex++; dishStartIndex = -1;
                    if (orderStartIndex >= orders.Count) return;
                }
                else
                {
                    dishStartIndex++;
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
                }
            }
            #endregion

            //List<OrderPanel> orderPanels = new List<OrderPanel>();
            OrderViewModel orderModel;
            int dishIdxFrom=-1, dishIdxTo=-1;

            _canvas.Children.Clear();
            // текущий столбец, с 1
            int curColIndex = (isPanelsForward) ? 1 : _pageColsCount;
            // текущая координата Top
            double curTopValue = (isPanelsForward) ? 0 : _canvas.ActualHeight;
            // высота свободного места в столбце
            double colBlankH = _canvas.ActualHeight;
            for (int iOrd = orderStartIndex;
                (isPanelsForward) ? iOrd < orders.Count : iOrd >= 0;
                iOrd += (isPanelsForward) ? 1 : -1)
            {
                // текущий заказ
                orderModel = orders[iOrd];
                // создать панель заказа со всеми блюдами и разделителями подач
                #region индексы блюд
                //   для последующих заказов - все блюда
                if (dishStartIndex == -1) { dishIdxFrom = 0; dishIdxTo = orderModel.Dishes.Count - 1; }
                //   для первого заказа - часть блюд
                else
                {
                    if (isPanelsForward)
                    {
                        // пропустить "назад" ингредиенты - блюда, у которых не пустой ParentUID
                        while (!orderModel.Dishes[dishStartIndex].ParentUID.IsNull() && (dishStartIndex > 0)) dishStartIndex--;
                        dishIdxFrom = dishStartIndex;
                        dishIdxTo = orderModel.Dishes.Count - 1;
                    }
                    else
                    {
                        dishIdxFrom = 0;
                        // пропустить "вперед" ингредиенты - блюда, у которых не пустой ParentUID
                        while (!orderModel.Dishes[dishStartIndex].ParentUID.IsNull() && (dishStartIndex < (orderModel.Dishes.Count-1))) dishStartIndex++;
                        dishIdxTo = dishStartIndex;
                    }
                }
                #endregion
                OrderPanel ordPanel = createOrderPanel(orderModel, dishIdxFrom, dishIdxTo);
                // получить реальные размеры панели заказа
                _canvas.Children.Add(ordPanel);
                _canvas.UpdateLayout();
                dishStartIndex = -1;  // все блюда

                // размещение панели
                //   полностью помещается в свободное место столбца
                if (ordPanel.ActualHeight <= colBlankH)
                {
                    // сдвинуть Top до отрисовки
                    if (!isPanelsForward) curTopValue -= ordPanel.ActualHeight;
                    
                    ordPanel.SetPosition(curTopValue, getLeftOrdPnl(curColIndex));

                    double d = ordPanel.ActualHeight + _hdrTopMargin;
                    // уменьшить свободное место на высоту панели и на расстояние между панелями по вертикали
                    colBlankH -= d;
                    // сдвинуть Top после отрисовки
                    curTopValue += (isPanelsForward) ? d : -_hdrTopMargin;
                }
                //   необходим перенос панели заказа, поэтому в цикле по панелям блюд
                else
                {
                    
                    
                }

                //orderPanels.Add(ordPanel);
                System.Diagnostics.Debug.Print("** order idx - " + iOrd.ToString());
            }

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
                    DishDelimeterPanel newDelimPanel = new DishDelimeterPanel() { Text = "Подача " + curFiling.ToString() };
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


    }  // class
}
