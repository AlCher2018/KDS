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
    public class OrdersModel
    {
        private Dictionary<int, OrderModel> _orders;
        public Dictionary<int, OrderModel> Orders { get { return _orders; } }

        private string _errorMsg;

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
                    // в запрос включить блюда, отделы и группы отделов
                    // отсортированные по порядке появления в таблице
                    dbOrders = db.Order
                        .Include("OrderDish")
                        .Include("OrderDish.Department")
                        .Include("OrderDish.Department.DepartmentDepartmentGroup")
                        .Where(o => (o.OrderStatusId < 2))
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
                    // сохранить в свойствах приложения словарь блюд с их количеством, 
                    // которые ожидают готовки или уже готовятся
                    Dictionary<int, decimal> dishesQty = getDishesQty(dbOrders);
                    AppEnv.SetAppProperty("dishesQty", dishesQty);

                    // удалить из внутр.словаря заказы, которых уже нет в БД
                    IEnumerable<int> delIds = _orders.Keys.Except(dbOrders.Select(o => o.Id));
                    foreach (int id in delIds)
                    {
                        _orders[id].Dispose(); _orders.Remove(id);
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

        private Dictionary<int, decimal> getDishesQty(List<Order> dbOrders)
        {
            Dictionary<int, decimal> retVal = new Dictionary<int, decimal>();
            foreach (Order order in dbOrders)
            {
                foreach (OrderDish dish in order.OrderDish)
                {
                    if (retVal.ContainsKey(dish.Id))
                        retVal[dish.Id] += dish.Quantity;
                    else
                        retVal.Add(dish.Id, dish.Quantity);
                }
            }

            return (retVal.Count == 0) ? null : retVal;
        }  // method

    }  // class

}
