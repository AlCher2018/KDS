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
using System.Threading;
using KDSService.DataSource;

namespace KDSService.AppModel
{
    // основной класс службы
    public class OrdersModel : IDisposable
    {
        private HashSet<int> allowedKDSStatuses;

        private Dictionary<int, OrderModel> _orders;
        public Dictionary<int, OrderModel> Orders { get { return _orders; } }

        /// <summary>
        /// учитывать ли отмененные блюда при подсчете одновременно готовящихся блюд для автостарта готовки
        /// </summary>
        private bool _takeCancelledInAutostartCooking;
        private string _errorMsg;

        private bool _isTraceLog;
        private bool _changeStatusYesterdayOrders;

        private HashSet<int> _unUsedDeps;


        // CONSTRUCTOR
        public OrdersModel()
        {
            // статусы заказов, которые выбираются из БД для отображения на КДС (все НЕтерминальные)
            allowedKDSStatuses = new HashSet<int>();
            allowedKDSStatuses.Add((int)OrderStatusEnum.WaitingCook);
            allowedKDSStatuses.Add((int)OrderStatusEnum.Cooking);
            allowedKDSStatuses.Add((int)OrderStatusEnum.Ready);
            allowedKDSStatuses.Add((int)OrderStatusEnum.Cancelled);
            allowedKDSStatuses.Add((int)OrderStatusEnum.Transferred);
            allowedKDSStatuses.Add((int)OrderStatusEnum.ReadyConfirmed);

            _orders = new Dictionary<int, OrderModel>();

            // учитывать ли отмененные блюда при подсчете одновременно готовящихся блюд для автостарта готовки
            _takeCancelledInAutostartCooking = (bool)AppEnv.GetAppProperty("TakeCancelledInAutostartCooking", false);

            _isTraceLog = (bool)AppEnv.GetAppProperty("IsWriteTraceMessages", false);
            _changeStatusYesterdayOrders = (AppEnv.TimeOfAutoCloseYesterdayOrders != TimeSpan.Zero);

            _unUsedDeps = (HashSet<int>)AppEnv.GetAppProperty("UnusedDepartments");
        }

        //**************************************
        // ГЛАВНАЯ ПРОЦЕДУРА ОБНОВЛЕНИЯ ЗАКАЗОВ
        //**************************************
        public string UpdateOrders()
        {
            // время автосброса вчерашних заказов/блюд
            if (_changeStatusYesterdayOrders)
            {
                ThreadStart tStart = new ThreadStart(updateYesterdayOrdersStatus);
                Thread thread = new Thread(tStart);
                thread.Start();
                thread.Join();
            }

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

            // получено заказов из БД
            if (_isTraceLog)
            {
                string ids = string.Join(",", dbOrders.Select(o => o.Id).ToArray());
                AppEnv.WriteLogTraceMessage("> DB orders from: {0} (ids: {1})", dbOrders.Count, ids);
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

                    // для последующих обработок удалить из заказов блюда с ненужными статусами и неотображаемые на КДСах
                    foreach (Order dbOrder in dbOrders)
                    {
                        OrderStatusEnum dishesStatus = AppEnv.GetStatusAllDishes(dbOrder.OrderDish);
                        // общий статус всех блюд НЕ совпадает со статусом заказа и для статуса блюд = 3 (Выданы)
                        if ((dishesStatus != OrderStatusEnum.None) 
                            && ((int)dishesStatus != dbOrder.OrderStatusId) && (dishesStatus == OrderStatusEnum.Took))
                        {
                            dbOrder.OrderStatusId = 3;
                            dbOrder.QueueStatusId = 2;
                            using (KDSEntities db = new KDSEntities())
                            {
                                try
                                {
                                    db.Order.Attach(dbOrder);
                                    //var manager = ((System.Data.Entity.Infrastructure.IObjectContextAdapter)db).ObjectContext.ObjectStateManager;
                                    //manager.ChangeObjectState(dbOrder, System.Data.Entity.EntityState.Modified);
                                    db.Entry(dbOrder).State = System.Data.Entity.EntityState.Modified;
                                    db.SaveChanges();
                                    AppEnv.WriteLogTraceMessage(string.Format("Изменен статус заказа {0} на {1} согласно общему статусу всех блюд", dbOrder.Id, dbOrder.OrderStatusId));
                                }
                                catch (Exception ex)
                                {
                                    AppEnv.WriteLogErrorMessage(string.Format("Ошибка изменения статуса заказа {0} на {1} согласно общему статусу всех блюд: {2}", dbOrder.Id, dbOrder.OrderStatusId, ex.ToString()));
                                }
                            }
                        }

                        // массив блюд для удаления
                        OrderDish[] dishesForDel = dbOrder.OrderDish.Where(d => isProcessingDishStatusId(d)==false).ToArray();
                        // удаление ненужных блюд
                        foreach (OrderDish delDish in dishesForDel) dbOrder.OrderDish.Remove(delDish);
                    }

                    // обновить словарь блюд с их количеством, которые ожидают готовки или уже готовятся
                    try
                    {
                        updateDishesQuantityDict(dbOrders);
                    }
                    catch (Exception ex)
                    {
                        AppEnv.WriteLogErrorMessage("Ошибка обновления словаря цехов с количеством готовящихся блюд: " + ex.ToString());
                    }

                    // удалить из внутр.словаря заказы, которых уже нет в БД
                    // причины две: или запись была удалена из БД, или запись получила статут, не входящий в условия отбора
                    //    словарь состояний заказов <id, status>
                    //Dictionary<int, OrderStatusEnum> ordersStatusDict = new Dictionary<int, OrderStatusEnum>();
                    //dbOrders.ForEach(o => ordersStatusDict.Add(o.Id, AppLib.GetStatusEnumFromNullableInt(o.OrderStatusId)));
                    // delIds = _orders.Keys.Except(ordersStatusDict.Keys).ToArray();
                    int[] delIds = dbOrders.Select(o => o.Id).ToArray();
                    delIds = _orders.Keys.Except(delIds).ToArray();
                    if (_isTraceLog)
                    {
                        string s1 = ""; if (delIds.Length > 0) s1 = string.Join(",", delIds);
                        if (s1 != "") AppEnv.WriteLogTraceMessage("   удалены заказы {0}", s1);
                    }
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
                            AppEnv.WriteDBCommandMsg("  ORDER UpdateFromDBEntity(id {0}) -- START", dbOrder.Id);

                            // обновить существующий заказ
                            if (_isTraceLog) AppEnv.WriteLogTraceMessage("   обновляю заказ {0}...", dbOrder.Id);
                            try
                            {
                                _orders[dbOrder.Id].UpdateFromDBEntity(dbOrder);
                                if (_isTraceLog) AppEnv.WriteLogTraceMessage("   обновляю заказ {0}... Ok", dbOrder.Id);
                            }
                            catch (Exception ex)
                            {
                                AppEnv.WriteLogErrorMessage("Ошибка обновления служебного словаря для OrderId = {1}: {0}", ex.ToString(), dbOrder.Id);
                            }

                            AppEnv.WriteDBCommandMsg("  ORDER UpdateFromDBEntity(id {0}) -- FINISH", dbOrder.Id);
                        }
                        else
                        {
                            // добавление заказа в словарь
                            if (_isTraceLog) AppEnv.WriteLogTraceMessage("   добавляю заказ {0}...", dbOrder.Id);
                            try
                            {
                                OrderModel newOrder = new OrderModel(dbOrder);
                                _orders.Add(dbOrder.Id, newOrder);
                                if (_isTraceLog) AppEnv.WriteLogTraceMessage("   добавляю заказ {0}... Ok", dbOrder.Id);
                            }
                            catch (Exception ex)
                            {
                                AppEnv.WriteLogErrorMessage("Ошибка добавления в служебный словарь заказа OrderId = {1}: {0}", ex.ToString(), dbOrder.Id);
                            }
                        }  //curOrder
                    }  // foreach

                }  // lock
                dbOrders = null; GC.Collect(0, GCCollectionMode.Optimized);
            }

            if (_isTraceLog)
            {
                string ids = "";
                if (_orders.Count > 0) ids = string.Join(",", _orders.Values.Select(o => o.Id).ToArray());
                AppEnv.WriteLogTraceMessage("< Clients orders to: {0} (ids: {1})", _orders.Count, ids);
            }

            return null;
        }  // method


        private void updateYesterdayOrdersStatus()
        {
                DateTime dt = DateTime.Today;
                // промежуток времени в течение 5 сек
                int secs = dt.Second - AppEnv.TimeOfAutoCloseYesterdayOrders.Seconds;
                //if ((AppEnv.TimeOfAutoCloseYesterdayOrders.Hours == dt.Hour)
                //    && (AppEnv.TimeOfAutoCloseYesterdayOrders.Minutes == dt.Minute)
                //    && (secs >= 0) && (secs <= 5))
                //{
                AppEnv.WriteLogInfoMessage("Обновить статус вчерашних заказов...");
                DateTime dtEstEndDay = new DateTime(dt.Year, dt.Month, dt.Day);
                using (KDSEntities db = new KDSEntities())
                {
                    List<Order> yesterdayOrders = db.Order.Where(o => (o.OrderStatusId < 3) && (o.CreateDate < dtEstEndDay)).ToList();
                    foreach (Order item in yesterdayOrders)
                    {
                        item.OrderStatusId = (int)OrderStatusEnum.YesterdayNotTook;
                    }
                    try
                    {
                        int processed = db.SaveChanges();
                        AppEnv.WriteLogInfoMessage(" - обновлено заказов {0}", processed.ToString());
                    }
                    catch (Exception ex)
                    {
                        AppEnv.WriteLogErrorMessage(" - ошибка обновления заказов: {0}", ex.Message + (ex.InnerException == null ? "" : " Inner exception: " + ex.InnerException.Message));
                    }
                }
                _changeStatusYesterdayOrders = false;
            //            }

        }
        
        // процедуры проверки числ.значения статуса ЗАКАЗА (из БД) на обработку в КДС
        // статус: 0 - ожидает приготовления, 1 - готовится, 2 - готово, 3 - выдано, 4 - отмена, 5 - зафиксировано
        // дата создания: только текущая!
        private bool isProcessingOrderStatusId(Order order)
        {
            bool retVal = allowedKDSStatuses.Contains(order.OrderStatusId) 
                && order.CreateDate.Date.Equals(DateTime.Today.Date);
            return retVal;
        }
        // фильтр БЛЮД
        // и количество != 0 (положительные - готовятся, отрицательные - отмененные)
        // и цех (напр.печати) - отображаемый
        private bool isProcessingDishStatusId(OrderDish dish)
        {
            bool retVal = allowedKDSStatuses.Contains(dish.DishStatusId??-1)
                && (dish.Quantity != 0m)
                && (!_unUsedDeps.Contains(dish.DepartmentId));

            return retVal;
        }


        // блюда, которые ожидают готовки или уже готовятся, с их кол-вом в заказах для каждого НП
        // словарь хранится в свойствах приложения
        private void updateDishesQuantityDict(List<Order> dbOrders)
        {
            // получить или создать словарь по отделам (направлениям печати)
            Dictionary<int, decimal> dishesQty = null;
            var v1 = AppEnv.GetAppProperty("dishesQty");
            if (v1 == null) dishesQty = new Dictionary<int, decimal>();
            else dishesQty = (Dictionary<int, decimal>)v1;

            // очистить кол-во
            List<int> keys = dishesQty.Keys.ToList();
            foreach (int key in keys) dishesQty[key] = 0m;

            if (_isTraceLog) AppEnv.WriteLogTraceMessage("   обновляю словарь количества готовящихся блюд по цехам...");

            decimal qnt;
            OrderStatusEnum eStatus;
            bool isTakeQuantity, isTakeCancelled;
            foreach (Order order in dbOrders)   // orders loop
            {
                foreach (OrderDish dish in order.OrderDish)  //  dishes loop
                {
                    eStatus = (OrderStatusEnum)(dish.DishStatusId ?? 0);
                    isTakeCancelled = (_takeCancelledInAutostartCooking && (eStatus == OrderStatusEnum.Cancelled));
                    isTakeQuantity = dish.ParentUid.IsNull()
                        && ((eStatus == OrderStatusEnum.Cooking) || isTakeCancelled);

                    // кол-во блюд для подсчета
                    qnt = isTakeQuantity ? (isTakeCancelled ? -dish.Quantity : dish.Quantity) : 0;

                    if (dishesQty.ContainsKey(dish.DepartmentId))
                        dishesQty[dish.DepartmentId] += qnt;
                    else
                        dishesQty.Add(dish.DepartmentId, qnt);
                }  // loop
            }  // loop

            AppEnv.SetAppProperty("dishesQty", dishesQty);

            if (_isTraceLog)
            {
                StringBuilder sb = new StringBuilder();
                foreach (KeyValuePair<int, decimal> item in dishesQty)
                {
                    if (sb.Length > 0) sb.Append("; ");
                    sb.Append(string.Format("depId {0} - {1}", item.Key, item.Value));
                }
                
                AppEnv.WriteLogTraceMessage("   result: " + sb.ToString());
            }

        }  // method


        public void Dispose()
        {
            if (_orders != null)
            {
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
