using KDSConsoleSvcHost;
using KDSService.AppModel;
using KDSService.Lib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDSService.DataSource
{
    internal class DBOrderHelper
    {
        private static List<Order> _dbOrders;
        internal static List<Order> DBOrders { get { return _dbOrders; } } 

        private static HashSet<int> allowedKDSStatuses;
        internal static HashSet<int> AllowedKDSStatuses { set { allowedKDSStatuses = value; } }
        private static HashSet<int> unUsedDeps;
        internal static HashSet<int> UnusedDeps { set { unUsedDeps = value; } }

        private static List<subOrder1> tmpOrders;
        private static HashSet<subOrder1> tmpSubOrd;  // удаляемые заказы

        private static List<subDish1> tmpDishes;
        private static HashSet<subDish1> tmpSubDsh;  // удаляемые блюда

        private static List<string> _dishUIDs;


        static DBOrderHelper()
        {
            _dbOrders = new List<Order>();
            tmpOrders = new List<subOrder1>();
            tmpSubOrd = new HashSet<subOrder1>();
            tmpDishes = new List<subDish1>();
            tmpSubDsh = new HashSet<subDish1>();

            _dishUIDs = new List<string>();
        }


        internal static void LoadDBOrders()
        {
            string sqlText;
            _dbOrders.Clear();
            tmpOrders.Clear();
            tmpSubOrd.Clear();

            using (KDSEntities db = new KDSEntities())
            {
                // отобрать заказы за 5 дней, включая сегодня
                sqlText = string.Format("SELECT Id, OrderStatusId, CreateDate, Number FROM [Order] WHERE (DATEDIFF(day, CreateDate, CONVERT(date, '{0}', 20)) <= 5)", DateTime.Today.ToString("yyyy-MM-dd"));
                tmpOrders.AddRange(db.Database.SqlQuery<subOrder1>(sqlText));

                foreach (subOrder1 subOrd in tmpOrders)
                {
                    tmpDishes.Clear();

                    // ПРОВЕРКА ПО СТАТУСУ
                    bool bStatus = allowedKDSStatuses.Contains(subOrd.OrderStatusId);
                    // статус заказа не разрешен - проверяем по блюдам
                    if (bStatus == false)
                    {
                        if (tmpDishes.Count == 0) getDishes(db, subOrd.Id);

                        foreach (subDish1 dishItem in tmpDishes)
                        {
                            // заказ уже не разрешен для показа на КДСе по статусу, но в нем есть разрешенные блюда
                            // со статусом Ожидает или Готовится - заказ переводим в статус Готовится
                            if ((dishItem.DishStatusId == 0) || (dishItem.DishStatusId == 1))
                            {
                                AppEnv.WriteLogTraceMessage("   заказ {0}/{1} из статуса {2} переведен в {3}, т.к. блюдо id {4} имеет статус {5}", subOrd.Id, subOrd.Number, subOrd.OrderStatusId, "1", dishItem.Id, dishItem.DishStatusId);
                                db.Database.ExecuteSqlCommand(string.Format("UPDATE [Order] SET OrderStatusId=1, QueueStatusId=0 WHERE (Id={0})", subOrd.Id));

                                bStatus = true;
                                break;
                            }
                        }
                    }
                    // 1. заказ не прошел по статусу - удалить из полученных от БД
                    if (bStatus == false) { tmpSubOrd.Add(subOrd); continue; }

                    // ПРОВЕРКА ПО ДАТЕ
                    // заказ сегодняшний?
                    bool bDate = (DateTime.Today == subOrd.CreateDate.Date);
                    // вчерашний заказ, проверить дату/время, после которого вчерашние заказы будут отображаться на КДСе
                    // 2017-08-31 проверка и по заказу, и по БЛЮДАМ
                    if (bDate == false)
                    {
                        double d1 = AppProperties.GetDoubleProperty("MidnightShiftShowYesterdayOrders");
                        DateTime dtWider = DateTime.Today.AddHours(-d1);
                        if (subOrd.CreateDate >= dtWider) bDate = true;

                        // по блюдам
                        if (bDate == false)
                        {
                            if (tmpDishes.Count == 0) getDishes(db, subOrd.Id);

                            foreach (subDish1 dishItem in tmpDishes)
                            {
                                if (dishItem.CreateDate >= dtWider) { bDate = true; break; }
                            }
                        }
                    }

                    // 2. заказ не прошел по дате - удалить из полученных
                    if (bDate == false) { tmpSubOrd.Add(subOrd); continue; }


                    if (tmpDishes.Count == 0) getDishes(db, subOrd.Id);
                    // блюд нет - заказ удалить
                    if (tmpDishes.Count == 0) { tmpSubOrd.Add(subOrd); continue; }

                    // узнать общий статус всех (оставшихся) блюд
                    int idStatus = getStatusAllDishesInt();
                    // все блюда Выданы, а заказ не выдан - изменить статус заказа
                    if ((idStatus == (int)OrderStatusEnum.Took) && (subOrd.OrderStatusId != idStatus))
                    {
                        AppEnv.WriteLogTraceMessage("   заказ {0}/{1} из статуса {2} переведен в {3}, т.к. все его блюда Выданы", subOrd.Id, subOrd.Number, subOrd.OrderStatusId, (int)OrderStatusEnum.Took);
                        db.Database.ExecuteSqlCommand(string.Format("UPDATE [Order] SET OrderStatusId = 3, QueueStatusId = 2 WHERE (Id={0})", subOrd.Id));
                    }
                }
                foreach (subOrder1 sbOrd in tmpSubOrd) tmpOrders.Remove(sbOrd);

                // обновить коллекцию заказов из БД
                if (tmpOrders.Count > 0)
                {
                    List<int> ids = tmpOrders.Select(t => t.Id).ToList();
                    _dbOrders.AddRange(db.Order.Include("OrderDish").Where(o => ids.Contains(o.Id)));
                }
            }
        }

        // получить блюда для заказа
        private static void getDishes(KDSEntities db, int id)
        {
            string sqlText = string.Format("SELECT Id, DishStatusId, CreateDate, DepartmentId, Quantity, UID, ParentUid FROM OrderDish WHERE (OrderId={0})", id);

            // блюда заказа
            tmpDishes.AddRange(db.Database.SqlQuery<subDish1>(sqlText));

            // удалить блюда с неотображаемым статусом или с нулевым количеством
            tmpDishes.RemoveAll(d => (allowedKDSStatuses.Contains(d.DishStatusId) == false)|| (d.Quantity == 0m));

            // удалить блюда, у которых неотображаемый цех
            if (unUsedDeps != null) tmpDishes.RemoveAll(d => unUsedDeps.Contains(d.DepartmentId));

            // поиск "висячих" ингредиентов, т.е. блюда нет (по статусу от службы), а ингредиенты - есть
            if (tmpDishes.Count > 0)
            {
                _dishUIDs.Clear();
                _dishUIDs.AddRange(tmpDishes.Where(d => d.ParentUid.IsNull()).Select(d => d.UID));

                // удалить "висячие" ингредиенты
                if (_dishUIDs.Count > 0) tmpDishes.RemoveAll(
                    d => (d.ParentUid.IsNull() == false) && (_dishUIDs.Contains(d.ParentUid) == false));
            }
        }

        // получить общий статус всех блюд
        private static int getStatusAllDishesInt()
        {
            if ((tmpDishes == null) || (tmpDishes.Count() == 0)) return -1;

            int retVal = -1;
            foreach (subDish1 dish in tmpDishes)
            {
                if (retVal == -1)
                    retVal = dish.DishStatusId;
                else 
                    if (retVal != dish.DishStatusId) return -1;
            }

            return retVal;
        }



        private class subOrder1
        {
            public int Id { get; set; }
            public int OrderStatusId { get; set; }
            public DateTime CreateDate { get; set; }
            public int Number { get; set; }
        }

        private class subDish1
        {
            public int Id { get; set; }
            public int DishStatusId { get; set; }
            public DateTime CreateDate { get; set; }
            public int DepartmentId { get; set; }
            public decimal Quantity { get; set; }
            public string UID { get; set; }
            public string ParentUid { get; set; }
        }

    }  // class
}
