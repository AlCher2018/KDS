using KDSWPFClient.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;


namespace KDSWPFClient.View
{
    internal class OrderPageHelper
    {
        private Canvas _canvas;

        internal double PanelWidth { get; set; }

        internal void SetPageSize(double width, double height)
        {
            _canvas = new Canvas();
            _canvas.Width = width; _canvas.Height = height;
        }

        /// <summary>
        /// Возвращает список панелей заказов (тип OrderPanel) для одной страницы.
        /// </summary>
        /// <param name="orders">Список заказов (тип OrderViewModel)</param>
        /// <param name="orderIndex">Индекс заказа, начиная с которого будут строиться панели</param>
        /// <param name="dishIndex">Индекс блюда, начиная с которогу будут строиться панели</param>
        /// <param name="isPanelsForward">Признак того, что коллекция orders будет листаться вперед, от стартовой позиции к концу набора</param>
        /// <returns></returns>
        internal List<OrderPanel> GetOrderPanelsPerPage(List<OrderViewModel> orders, int orderStartIndex, int dishStartIndex, bool isPanelsForward)
        {
            if (orders == null) return null;
            if (orderStartIndex >= orders.Count) return null;

            System.Diagnostics.Debug.Print("** начальные индексы: order {0}, dish {1}", orderStartIndex, dishStartIndex);

            #region найти след/предыд индексы заказ/блюдо, с которых начинается создание панелей
            // найти след/предыд индексы заказ/блюдо, с которых начинается создание панелей
            // прямое направление
            if (isPanelsForward)
            {
                if (dishStartIndex >= orders[orderStartIndex].Dishes.Count-1)
                {
                    orderStartIndex++; dishStartIndex = 0;
                    if (orderStartIndex >= orders.Count) return null;
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
                    orderStartIndex--;
                    if (orderStartIndex < 0) return null;
                }
                else
                {
                    dishStartIndex--;
                }
            }
            #endregion

            List<OrderPanel> retVal = new List<OrderPanel>();
            OrderViewModel tmpOrder; OrderDishViewModel tmpDish;
            for (int iOrd = orderStartIndex;
                (isPanelsForward) ? iOrd < orders.Count : iOrd >= 0;
                iOrd += (isPanelsForward) ? 1 : -1)
            {
                tmpOrder = orders[iOrd];
                OrderPanel ordPanel = new OrderPanel(tmpOrder, 0, PanelWidth, true);
                System.Diagnostics.Debug.Print("** order idx - " + iOrd.ToString());
                if (dishStartIndex < 0) dishStartIndex = (isPanelsForward) ? 0 : tmpOrder.Dishes.Count;
                for (int iDish = dishStartIndex;
                    (isPanelsForward) ? iDish < tmpOrder.Dishes.Count : iDish >= 0;
                    iDish += (isPanelsForward) ? 1 : -1)
                {
                    tmpDish = tmpOrder.Dishes[iDish];
                    DishPanel dishPanel = new DishPanel(tmpDish, null);
                    ordPanel.AddDish(dishPanel);
                    System.Diagnostics.Debug.Print("**   dish index - " + iDish.ToString());
                }
                retVal.Add(ordPanel);
                // после первого прохода по заказу, сбрасывать dishStartIndex в -1, чтобы он вычислялся динамически
                dishStartIndex = -1;
            }

            if (retVal.Count == 0) retVal = null;
            else if ((retVal.Count == 1) && (retVal[0].DishPanelsCount == 0)) retVal = null;

            return retVal;
        }


    }  // class
}
