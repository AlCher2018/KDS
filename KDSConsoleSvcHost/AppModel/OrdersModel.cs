using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using KDSConsoleSvcHost;
using KDSService.Lib;
using KDSConsoleSvcHost.AppModel;


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
                    // отсортированные по порядке появления в таблице
                    dbOrders = db.Order
                        .Include("OrderDish")
                        .Include("OrderDish.Department")
                        .Include("OrderDish.Department.DepartmentDepartmentGroup")
                        .Where(isProcessingOrderStatusId)
                        .OrderBy(o => o.Id)
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
                lock (dbOrders)
                {
                    int[] delIds;
                    // для последующех обработок удалить из заказов блюда с ненужными статусами
                    foreach (Order dbOrder in dbOrders)
                    {
                        // массив блюд для удаления
                        OrderDish[] dishesForDel = dbOrder.OrderDish.Where(d => isProcessingDishStatusId(d)==false).ToArray();
                        // удаление ненужных блюд
                        foreach (OrderDish delDish in dishesForDel) dbOrder.OrderDish.Remove(delDish);
                    }

                    // сохранить в свойствах приложения словарь блюд с их количеством, 
                    // которые ожидают готовки или уже готовятся
                    Dictionary<int, decimal> dishesQty = getDishesQuantity(dbOrders);
                    AppEnv.SetAppProperty("dishesQty", dishesQty);

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
                            _orders[dbOrder.Id].UpdateFromDBEntity(dbOrder, false);
                        }
                        else
                        {
                            // добавление заказа в словарь
                            OrderModel newOrder = new OrderModel(dbOrder);
                            _orders.Add(dbOrder.Id, newOrder);
                        }
                        //curOrder
                    }
                }  // lock
            }

            return null;
        }  // method

        // перегруженные процедуры проверки числ.значения статуса Заказа/Блюда (из БД) на обработку в КДС
        // статус: 0 - ожидает приготовления, 1 - готовится, 2 - готово, 3 - выдано, 4 - отмена, 5 - зафиксировано
        private bool isProcessingOrderStatusId(Order order)
        {
            return (order.OrderStatusId <= 2) || (order.OrderStatusId == 4);
        }
        // статус: null - не указан, ... см.выше
        private bool isProcessingDishStatusId(OrderDish dish)
        {
            return (dish.DishStatusId == null) || (dish.DishStatusId <= 2) || (dish.DishStatusId == 4);
        }


        // блюда, которые ожидают готовки или уже готовятся, с их кол-вом в заказах
        private Dictionary<int, decimal> getDishesQuantity(List<Order> dbOrders)
        {
            Dictionary<int, decimal> retVal = new Dictionary<int, decimal>();
            foreach (Order order in dbOrders)   // orders loop
            {
                foreach (OrderDish dish in order.OrderDish)  //  dishes loop
                {
                    if ((dish.DishStatusId == null) || (dish.DishStatusId <= 2))
                    {
                        if (retVal.ContainsKey(dish.Id))
                            retVal[dish.Id] += dish.Quantity;
                        else
                            retVal.Add(dish.Id, dish.Quantity);
                    }
                }  // loop
            }  // loop

            return (retVal.Count == 0) ? null : retVal;
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
