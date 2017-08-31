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
using KDSWPFClient.Lib;

namespace KDSService.AppModel
{
    // основной класс службы
    public class OrdersModel : IDisposable
    {
        private Random rnd = new Random();

        private HashSet<int> allowedKDSStatuses;
        private Dictionary<int, bool> _lockedOrders;

        private Dictionary<int, OrderModel> _orders;
        public Dictionary<int, OrderModel> Orders { get { return _orders; } }

        /// <summary>
        /// учитывать ли отмененные блюда при подсчете одновременно готовящихся блюд для автостарта готовки
        /// </summary>
        private bool _takeCancelledInAutostartCooking;
        private string _errorMsg;

        private bool _isLogOrderDetails;
        private bool _changeStatusYesterdayOrdersCfg, _changeStatusYesterdayOrdersCurrent;
        private DateTime _currenDate;

        List<Order> _delOrders;
        List<OrderDish> _delDishes;
        List<string> _dishUIDs;
        private HashSet<int> _unUsedDeps;

        private string _returnedOrderIds;
        
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
            _takeCancelledInAutostartCooking = AppProperties.GetBoolProperty("TakeCancelledInAutostartCooking");
            // используется, если для детального лога нужно еще чего-то сделать
            _isLogOrderDetails = (AppProperties.GetBoolProperty("IsWriteTraceMessages") && AppProperties.GetBoolProperty("TraceOrdersDetails"));

            _changeStatusYesterdayOrdersCfg = (AppEnv.TimeOfAutoCloseYesterdayOrders != TimeSpan.Zero);
            _changeStatusYesterdayOrdersCurrent = _changeStatusYesterdayOrdersCfg;
            _currenDate = DateTime.Now.Date;

            _delOrders = new List<Order>();
            _delDishes = new List<OrderDish>();
            _dishUIDs = new List<string>();
            _unUsedDeps = (HashSet<int>)AppProperties.GetProperty("UnusedDepartments");
        }

        //**************************************
        // ГЛАВНАЯ ПРОЦЕДУРА ОБНОВЛЕНИЯ ЗАКАЗОВ
        //
        // здесь пишем в лог при включенном флаге TraceOrdersDetails
        //**************************************
        public string UpdateOrders()
        {
            string sLog;

            // автосброс вчерашних заказов
            if (_changeStatusYesterdayOrdersCfg)
            {
                if (_changeStatusYesterdayOrdersCurrent)
                {
                    if (updateYesterdayOrdersStatus())
                    {
                        // для одноразового прохода в течение дня
                        _changeStatusYesterdayOrdersCurrent = false;
                    }
                }
                // текущий флаг обновления вчерашних заказов сброшен и переползли в новые сутки
                else if (_currenDate != DateTime.Today)
                {
                    // устанавливаем флажок, чтобы при следующем проходе, попытаться обновить статус вчерашних заказов
                    _changeStatusYesterdayOrdersCurrent = true;
                }
            }

            AppEnv.WriteLogOrderDetails("GET ORDERS FROM DB - START");
            Console.WriteLine("getting orders ..."); DebugTimer.Init(" - get orders from DB");

            // получить заказы из БД
            List<Order> dbOrders = null;
            try
            {
                using (KDSEntities db = new KDSEntities())
                {
                    // отобрать заказы за 5 дней, включая сегодня
                    // в запрос включить блюда, отделы и группы отделов
                    IEnumerable<Order> ordTmp = db.Order
                        .Include("OrderDish")
                        .Include("OrderDish.Department")
                        .Where(dbDateFilter);

                    // отобрать заказы с определенными статусами (HashSet<int> allowedKDSStatuses)
                    // группировка и сортировка осуществляется на клиенте
                    // здесь статус заказа может быть изменен в методе <isProcessingOrderStatusId>
                    if (_returnedOrderIds != "") _returnedOrderIds = "";
                    dbOrders = ordTmp.Where(isProcessingOrderStatusId).ToList();

                    if (db.ChangeTracker.HasChanges())
                    {
                        AppEnv.WriteLogOrderDetails(" - returned Orders to status 1 (id/Num): " + _returnedOrderIds);
                        db.SaveChanges();
                    }
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }

            // получено заказов из БД
            if (_isLogOrderDetails)
            {
                string ids = string.Join(",", dbOrders.Select(o => o.Id.ToString()+"/"+o.Number.ToString()));
                AppEnv.WriteLogOrderDetails(" - from DB {0} id/Num: {1}", dbOrders.Count, ids);
            }

            // цикл по полученным из БД заказам
            if (dbOrders != null)
            {
                lock (dbOrders)
                {
                    // для последующих обработок удалить из заказов блюда с ненужными статусами и неотображаемые на КДСах
                    AppEnv.WriteLogOrderDetails(" - delete not allowed dishes...");
                    _delOrders.Clear();
                    foreach (Order dbOrder in dbOrders)
                    {
                        OrderStatusEnum dishesStatus = AppEnv.GetStatusAllDishes(dbOrder.OrderDish);
                        // все блюда (активных НП) Выданы, а заказ - не выдан, изменить статус заказа
                        if ((dishesStatus != OrderStatusEnum.None) 
                            && ((int)dishesStatus != dbOrder.OrderStatusId) && (dishesStatus == OrderStatusEnum.Took))
                        {
                            dbOrder.OrderStatusId = 3;
                            dbOrder.QueueStatusId = 2;
                            sLog = string.Format("   изменен статус заказа {0}/{1} на {2} согласно общему статусу всех блюд - ", dbOrder.Id, dbOrder.Number, dbOrder.OrderStatusId);
                            using (KDSEntities db = new KDSEntities())
                            {
                                try
                                {
                                    db.Order.Attach(dbOrder);
                                    //var manager = ((System.Data.Entity.Infrastructure.IObjectContextAdapter)db).ObjectContext.ObjectStateManager;
                                    //manager.ChangeObjectState(dbOrder, System.Data.Entity.EntityState.Modified);
                                    db.Entry(dbOrder).State = System.Data.Entity.EntityState.Modified;
                                    db.SaveChanges();

                                    sLog += "Ok";
                                }
                                catch (Exception ex)
                                {
                                    sLog += ex.ToString();
                                }
                                AppEnv.WriteLogOrderDetails(sLog);
                            }
                        }

                        // фильтр БЛЮД: разрешенные статусы И количество != 0 (положительные - готовятся, отрицательные - отмененные)
                        // И цех (напр.печати) - отображаемый
                        //bool retVal = allowedKDSStatuses.Contains(dish.DishStatusId??-1)
                        //    && (dish.Quantity != 0m)
                        //    && (!_unUsedDeps.Contains(dish.DepartmentId));
                        _delDishes.Clear();
                        _delDishes = dbOrder.OrderDish.Where(
                            d =>
                            {
                                bool result = false;
                                if (_unUsedDeps == null)
                                    result = allowedKDSStatuses.Contains(d.DishStatusId ?? -1) && (d.Quantity != 0m);
                                else
                                    result = allowedKDSStatuses.Contains(d.DishStatusId ?? -1) && (d.Quantity != 0m) 
                                    && (!_unUsedDeps.Contains(d.DepartmentId));

                                return !result;
                            }
                        ).ToList();
                        // удалить ненужные блюда
                        foreach (OrderDish delDish in _delDishes) dbOrder.OrderDish.Remove(delDish);

                        // поиск "висячих" ингредиентов, т.е. блюда нет (по статусу от службы), а ингредиенты - есть
                        _delDishes.Clear();
                        _dishUIDs = dbOrder.OrderDish.Where(d => d.ParentUid.IsNull()).Select(d => d.UID).ToList();
                        _delDishes = dbOrder.OrderDish.Where(d => (!d.ParentUid.IsNull() && (!_dishUIDs.Contains(d.ParentUid)))).ToList();
                        if (_delDishes.Count > 0)
                        {
                            // удалить ненужные блюда
                            foreach (OrderDish delDish in _delDishes) dbOrder.OrderDish.Remove(delDish);
                        }

                        // коллекция заказов с нулевым количеством блюд - клиентам не передавать
                        if (dbOrder.OrderDish.Count == 0) _delOrders.Add(dbOrder);
                    }

                    // удалить заказы, у которых нет разрешенных блюд
                    if (_delOrders.Count == 0)
                    {
                        AppEnv.WriteLogOrderDetails("   - result: 0");
                    }
                    else
                    {
                        string s1 = string.Join(", ",_delOrders.Select(o => o.Id.ToString()));
                        AppEnv.WriteLogOrderDetails("   - result: order Ids without allowed dishes: " + s1);
                        _delOrders.ForEach(o => dbOrders.Remove(o));
                    }

                    // обновить словарь блюд с их количеством, которые ожидают готовки или уже готовятся
                    sLog = "   updateDishesQuantityDict()...";
                    AppEnv.WriteLogOrderDetails(sLog);
                    try
                    {
                        updateDishesQuantityDict(dbOrders);
                        if (_isLogOrderDetails)
                        {
                            Dictionary<int, decimal> dishesQty = (Dictionary<int, decimal>)AppProperties.GetProperty("dishesQty");
                            StringBuilder sb = new StringBuilder();
                            foreach (KeyValuePair<int, decimal> item in dishesQty)
                            {
                                if (sb.Length > 0) sb.Append("; ");
                                sb.Append(string.Format("{0}/{1}", item.Key, item.Value));
                            }
                            AppEnv.WriteLogOrderDetails("   - result (depId/count): " + sb.ToString());
                        }
                    }
                    catch (Exception ex)
                    {
                        if (_isLogOrderDetails) AppEnv.WriteLogOrderDetails("   error: " + ex.ToString());
                        else AppEnv.WriteLogErrorMessage("Ошибка обновления словаря количества готовящихся блюд по цехам: " + ex.ToString());
                    }

                    // *** ОБНОВЛЕНИЕ ВНУТРЕННЕГО СЛОВАРЯ ЗАКАЗОВ ***
                    // 1. удалить
                    int[] delIds = dbOrders.Select(o => o.Id).ToArray();
                    delIds = _orders.Keys.Except(delIds).ToArray();
                    if (_isLogOrderDetails)
                    {
                        string s1 = ""; if (delIds.Length > 0) s1 = string.Join(",", delIds);
                        if (s1 != "") AppEnv.WriteLogOrderDetails("   appModel: remove order Ids {0}", s1);
                    }
                    foreach (int id in delIds)
                    {
                        _orders[id].Dispose();
                        _orders.Remove(id);
                    }

                    // 2. обновить или добавить
                    _lockedOrders = (Dictionary<int, bool>)AppProperties.GetProperty("lockedOrders");
                    foreach (Order dbOrder in dbOrders)
                    {
                        // пропустить, если заказ заблокирован от изменений по таймеру
                        if ((_lockedOrders != null) && _lockedOrders.ContainsKey(dbOrder.Id))
                        {
                            AppEnv.WriteLogOrderDetails("   appModel: locked order Id " + dbOrder.Id.ToString());
                            continue;
                        }

                        if (_orders.ContainsKey(dbOrder.Id))
                        {
                            sLog = string.Format("   appModel: update id {0}, num {1}", dbOrder.Id, dbOrder.Number);
                            AppEnv.WriteLogOrderDetails(sLog + " - START");
                            try
                            {
                                _orders[dbOrder.Id].UpdateFromDBEntity(dbOrder);
                            }
                            catch (Exception ex)
                            {
                                AppEnv.WriteLogErrorMessage("Ошибка обновления служебного словаря для OrderId = {1}: {0}", ex.ToString(), dbOrder.Id);
                            }
                            AppEnv.WriteLogOrderDetails(sLog + " - FINISH");
                        }
                        // добавление заказа в словарь
                        else
                        {
                            sLog = string.Format("   appModel: add order id {0}, num {1}", dbOrder.Id, dbOrder.Number);
                            AppEnv.WriteLogOrderDetails(sLog + " - START");
                            try
                            {
                                OrderModel newOrder = new OrderModel(dbOrder);
                                _orders.Add(dbOrder.Id, newOrder);
                            }
                            catch (Exception ex)
                            {
                                AppEnv.WriteLogErrorMessage("Ошибка добавления в служебный словарь заказа OrderId = {1}: {0}", ex.ToString(), dbOrder.Id);
                            }
                            AppEnv.WriteLogOrderDetails(sLog + " - FINISH");
                        }  //curOrder

                    }  // foreach

                }  // lock
                dbOrders = null; GC.Collect(0, GCCollectionMode.Optimized);
            }

            if (_isLogOrderDetails)
            {
                string ids = "";
                if (_orders.Count > 0) ids = string.Join(",", _orders.Values.Select(o => o.Id.ToString()+"/"+o.Number.ToString()));
                AppEnv.WriteLogOrderDetails(" - to clients {0} id/Num: {1}", _orders.Count, ids);
            }

            // очистить словарь заблокированных заказов
            _lockedOrders = (Dictionary<int, bool>)AppProperties.GetProperty("lockedOrders");
            if ((_lockedOrders != null) && (_lockedOrders.Count > 0)) _lockedOrders.Clear();

            AppEnv.WriteLogOrderDetails("get orders from DB - FINISH" + " - " + DebugTimer.GetInterval());
            Console.WriteLine("... " + _orders.Count.ToString() + "  " + DebugTimer.GetInterval()); 

            return null;
        }  // method


        // обновить статус вчерашних заказов
        private bool updateYesterdayOrdersStatus()
        {
            // дата/время, когда необходимо обновить статус вчерашних заказов
            DateTime dtUpdate = _currenDate.Add(AppEnv.TimeOfAutoCloseYesterdayOrders);
            if (DateTime.Now < dtUpdate) return false;

            AppEnv.WriteLogOrderDetails(" - обновить статус вчерашних заказов... - START");
            using (KDSEntities db = new KDSEntities())
            {
                // вчерашние заказы, точнее заказы, у которых CreateDate меньше начала текущего дня (полночь)
                // с учетом смещения от полуночи назад, созданные в течение которого вчерашние заказы будут отображаться на КДСе
                double d1 = AppProperties.GetDoubleProperty("MidnightShiftShowYesterdayOrders");
                dtUpdate = DateTime.Today.AddHours(-d1);
                List<Order> yesterdayOrders = db.Order
                    .Where(o => (o.OrderStatusId < 3) && (o.CreateDate < dtUpdate))
                    .ToList();
                // обновить статус у объектов
                foreach (Order item in yesterdayOrders)
                {
                    item.OrderStatusId = (int)OrderStatusEnum.YesterdayNotTook;
                }
                //    и в БД
                bool retVal = false;
                try
                {
                    int processed = db.SaveChanges();
                    AppEnv.WriteLogOrderDetails(" - обновлено заказов {0} - FINISH", processed.ToString());
                    retVal = true;
                }
                catch (Exception ex)
                {
                    AppEnv.WriteLogOrderDetails(" - ошибка обновления заказов: {0} - FINISH", AppEnv.GetShortErrMessage(ex));
                }

                return retVal;
            }
        }

        private bool dbDateFilter(Order order)
        {
            return (DateTime.Today - order.CreateDate.Date).TotalDays <= 5;
        }

        // процедуры проверки числ.значения статуса ЗАКАЗА (из БД) на обработку в КДС
        // статус: 0 - ожидает приготовления, 1 - готовится, 2 - готово, 3 - выдано, 4 - отмена, 5 - зафиксировано
        // дата создания заказа: сегодня или назад от полуночи на MidnightShiftShowYesterdayOrders часов.
        private bool isProcessingOrderStatusId(Order order)
        {
            bool bDate, bStatus;

            // ПРОВЕРКА ПО СТАТУСУ
            bStatus = allowedKDSStatuses.Contains(order.OrderStatusId);
            // статус заказа не разрешен - проверяем по блюдам
            if (bStatus == false)
            {
                int iDishStatus;
                foreach (OrderDish dish in order.OrderDish)
                {
                    iDishStatus = (dish.DishStatusId ?? 0);
                    // а блюдо Ожидает или Готовится, то заказ переводим в статус Готовится
                    if ((iDishStatus == 0) || (iDishStatus == 1))
                    {
                        _returnedOrderIds += ((_returnedOrderIds.Length == 0) ? "" : "; ") + order.Id.ToString() + "/" + order.Number;
                        bStatus = true; order.OrderStatusId = 1; break;
                    }
                }
            }
            if (bStatus == false) return false;

            // ПРОВЕРКА ПО ДАТЕ
            // заказ сегодняшний?
            bDate = (DateTime.Today == order.CreateDate.Date);
            // вчерашний заказ, проверить дату/время, после которого вчерашние заказы будут отображаться на КДСе
            // 2017-08-31 проверка и по заказу, и по БЛЮДАМ
            if (bDate == false)
            {
                double d1 = AppProperties.GetDoubleProperty("MidnightShiftShowYesterdayOrders");
                DateTime dtWider = DateTime.Today.AddHours(-d1);
                if (order.CreateDate >= dtWider) bDate = true;

                // по блюдам
                if (bDate == false)
                {
                    foreach (OrderDish dish in order.OrderDish)
                    {
                        if (dish.CreateDate >= dtWider) { bDate = true; break; }
                    }
                }
            }
            return bDate;
        }


        // блюда, которые ожидают готовки или уже готовятся, с их кол-вом в заказах для каждого НП
        // словарь хранится в свойствах приложения
        private void updateDishesQuantityDict(List<Order> dbOrders)
        {
            // получить или создать словарь по отделам (направлениям печати)
            Dictionary<int, decimal> dishesQty = null;
            var v1 = AppProperties.GetProperty("dishesQty");
            if (v1 == null) dishesQty = new Dictionary<int, decimal>();
            else dishesQty = (Dictionary<int, decimal>)v1;

            // очистить кол-во
            List<int> keys = dishesQty.Keys.ToList();
            foreach (int key in keys) dishesQty[key] = 0m;

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

            AppProperties.SetProperty("dishesQty", dishesQty);
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
