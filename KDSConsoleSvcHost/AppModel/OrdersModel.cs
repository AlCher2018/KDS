using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using KDSConsoleSvcHost;
using KDSService.Lib;
using System.Diagnostics;

namespace KDSService.AppModel
{
    // основной класс службы
    public class OrdersModel : IDisposable
    {
        private Dictionary<int, OrderModel> _orders;
        public Dictionary<int, OrderModel> Orders { get { return _orders; } }

        private string _errorMsg;


        // CONSTRUCTOR
        public OrdersModel()
        {
            _orders = new Dictionary<int, OrderModel>();
        }

        //**************************************
        // ГЛАВНАЯ ПРОЦЕДУРА ОБНОВЛЕНИЯ ЗАКАЗОВ
        //**************************************
        public string UpdateOrders()
        {
            // получить заказы из БД
            List<Order> dbOrders = null;
            try
            {
                using (KDSEntities db = new KDSEntities())
                {
                    // отобрать заказы, которые не были закрыты (статус < 5)
                    // в запрос включить блюда, отделы и группы отделов
                    // группировка и сортировка осуществляется на клиенте
                    dbOrders = db.Order
                        .Include("OrderDish")
                        .Include("OrderDish.Department")
                        .Where(isProcessingOrderStatusId)
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }

            // цикл по полученным из БД заказам
            if (dbOrders != null)
            {
                lock (this)
                {
                    //foreach (Order item in dbOrders)
                    //{
                    //    Debug.Print("order Id - {0}, status - {1}", item.Id, item.OrderStatusId);
                    //    Debug.Print("\tdish status: {0}", string.Join("; ", item.OrderDish.Select(o => string.Format("id {0}: {1}", o.Id, o.DishStatusId)).ToArray()));
                    //}

                    int[] delIds;
                    // для последующех обработок удалить из заказов блюда с ненужными статусами
                    foreach (Order dbOrder in dbOrders)
                    {
                        // массив блюд для удаления
                        OrderDish[] dishesForDel = dbOrder.OrderDish.Where(d => isProcessingDishStatusId(d)==false).ToArray();
                        // удаление ненужных блюд
                        foreach (OrderDish delDish in dishesForDel) dbOrder.OrderDish.Remove(delDish);
                    }

                    // обновить словарь блюд с их количеством, которые ожидают готовки или уже готовятся
                    updateDishesQuantityDict(dbOrders);

                    // удалить из внутр.словаря заказы, которых уже нет в БД
                    // причины две: или запись была удалена из БД, или запись получила статут, не входящий в условия отбора
                    //    словарь состояний заказов <id, status>
                    Dictionary<int, OrderStatusEnum> ordersStatusDict = new Dictionary<int, OrderStatusEnum>();
                    dbOrders.ForEach(o => ordersStatusDict.Add(o.Id, AppLib.GetStatusEnumFromNullableInt(o.OrderStatusId)));

                    delIds = _orders.Keys.Except(ordersStatusDict.Keys).ToArray();
                    foreach (int id in delIds)
                    {
                        //if (_orders[id].Status == OrderStatusEnum.Commit) _orders[id].UpdateStatus(OrderStatusEnum.Commit, true);
                        _orders[id].Dispose();
                        _orders.Remove(id);
                    }

                    // обновить или добавить заказы во внутр.словаре
                    foreach (Order dbOrder in dbOrders)
                    {
                        if (_orders.ContainsKey(dbOrder.Id))
                        {
                            // обновить существующий заказ
                            _orders[dbOrder.Id].UpdateFromDBEntity(dbOrder);
                        }
                        else
                        {
                            // добавление заказа в словарь
                            OrderModel newOrder = new OrderModel(dbOrder);
                            _orders.Add(dbOrder.Id, newOrder);
                        }  //curOrder
                    }  // foreach

                }  // lock
                dbOrders = null; GC.Collect(0, GCCollectionMode.Optimized);
            }

            return null;
        }  // method

        // процедуры проверки числ.значения статуса Заказа/Блюда (из БД) на обработку в КДС
        // статус: 0 - ожидает приготовления, 1 - готовится, 2 - готово, 3 - выдано, 4 - отмена, 5 - зафиксировано
        // дата создания: только текущая!
        private bool isProcessingOrderStatusId(Order order)
        {
            return ( 
                ((order.OrderStatusId <= 2) || (order.OrderStatusId == 4) || (order.OrderStatusId == 7)) 
                && (order.CreateDate.Date.Equals(DateTime.Now.Date))
            );
        }
        // статус: null - не указан, ... см.выше
        // и количество != 0 (положительные - готовятся, отрицательные - отмененные)
        private bool isProcessingDishStatusId(OrderDish dish)
        {
            return ((dish.DishStatusId == null) 
                || (dish.DishStatusId <= 2) 
                || (dish.DishStatusId == 4) 
                || (dish.DishStatusId == 7))
                && (dish.Quantity != 0m);
        }


        // блюда, которые ожидают готовки или уже готовятся, с их кол-вом в заказах
        // словарь хранится в свойствах приложения
        private void updateDishesQuantityDict(List<Order> dbOrders)
        {
            // получить или создать словарь
            Dictionary<string, decimal> dishesQty = null;
            var v1 = AppEnv.GetAppProperty("dishesQty");
            if (v1 == null) dishesQty = new Dictionary<string, decimal>();
            else dishesQty = (Dictionary<string, decimal>)v1;

            // очистить кол-во
            List<string> keys = dishesQty.Keys.ToList();
            foreach (string key in keys) dishesQty[key] = 0m;

            foreach (Order order in dbOrders)   // orders loop
            {
                foreach (OrderDish dish in order.OrderDish)  //  dishes loop
                {
                    // только для блюд в состоянии приготовления 
                    if (dish.ParentUid.IsNull() && ((dish.DishStatusId??0) == 1))
                    {
                        if (dishesQty.ContainsKey(dish.UID))
                            dishesQty[dish.UID] += dish.Quantity;
                        else
                            dishesQty.Add(dish.UID, dish.Quantity);
                    }
                }  // loop
            }  // loop

            AppEnv.SetAppProperty("dishesQty", dishesQty);
        }  // method


        public void Dispose()
        {
            if (_orders != null)
            {
                AppEnv.WriteLogTraceMessage("dispose class OrdersModel");
                foreach (OrderModel modelOrder in _orders.Values) modelOrder.Dispose();
                _orders.Clear();
                _orders = null;
            }
        }

        // класс для чтения с помощью EF подчиненного запроса с условием
        //private class OrderAndDishesAsField
        //{
        //    public Order Order { get; set; }
        //    public IEnumerable<OrderDish> Dishes { get; set; }
        //}

    }  // class
}
