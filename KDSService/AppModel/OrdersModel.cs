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

        private HashSet<int> _allowedKDSStatuses;
        private HashSet<int> _unUsedDeps;
        private Dictionary<int, bool> _lockedOrders;

        private List<Order> _dbOrders;
        private Dictionary<int, OrderModel> _orders;
        public Dictionary<int, OrderModel> Orders { get { return _orders; } }

        /// <summary>
        /// учитывать ли отмененные блюда при подсчете одновременно готовящихся блюд для автостарта готовки
        /// </summary>
        private bool _takeCancelledInAutostartCooking;
        private string _errorMsg;

        private bool _isLogOrderDetails;
        private bool _changeStatusYesterdayOrdersCfg, _changeStatusYesterdayOrdersCurrent;
        private DateTime _currentDate;

        List<Order> _delOrders;

       
        // CONSTRUCTOR
        public OrdersModel()
        {
            // статусы заказов, которые выбираются из БД для отображения на КДС (все НЕтерминальные)
            _allowedKDSStatuses = new HashSet<int>();
            _allowedKDSStatuses.Add((int)OrderStatusEnum.WaitingCook);
            _allowedKDSStatuses.Add((int)OrderStatusEnum.Cooking);
            _allowedKDSStatuses.Add((int)OrderStatusEnum.Ready);
            _allowedKDSStatuses.Add((int)OrderStatusEnum.Cancelled);
            _allowedKDSStatuses.Add((int)OrderStatusEnum.Transferred);
            _allowedKDSStatuses.Add((int)OrderStatusEnum.ReadyConfirmed);
            DBOrderHelper.AllowedKDSStatuses = _allowedKDSStatuses;

            // неиспользуемые отделы, отфильтровываются на службе
            _unUsedDeps = (HashSet<int>)AppProperties.GetProperty("UnusedDepartments");
            DBOrderHelper.UnusedDeps = _unUsedDeps;

            _dbOrders = DBOrderHelper.DBOrders;
            _orders = new Dictionary<int, OrderModel>();

            // учитывать ли отмененные блюда при подсчете одновременно готовящихся блюд для автостарта готовки
            _takeCancelledInAutostartCooking = AppProperties.GetBoolProperty("TakeCancelledInAutostartCooking");
            // используется, если для детального лога нужно еще чего-то сделать
            _isLogOrderDetails = (AppProperties.GetBoolProperty("IsWriteTraceMessages") && AppProperties.GetBoolProperty("TraceOrdersDetails"));

            _changeStatusYesterdayOrdersCfg = (AppEnv.TimeOfAutoCloseYesterdayOrders != TimeSpan.Zero);
            _changeStatusYesterdayOrdersCurrent = _changeStatusYesterdayOrdersCfg;
            _currentDate = DateTime.Now.Date;
            //_currentDate = _currentDate.AddDays(-1);

            
            _delOrders = new List<Order>();
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
                        if (_currentDate != DateTime.Today) _currentDate = DateTime.Today;
                    }
                }
                // текущий флаг обновления вчерашних заказов сброшен и переползли в новые сутки
                else if (_currentDate != DateTime.Today)
                {
                    // устанавливаем флажок, чтобы при следующем проходе, попытаться обновить статус вчерашних заказов
                    _changeStatusYesterdayOrdersCurrent = true;
                }
            }

            AppEnv.WriteLogOrderDetails("GET ORDERS FROM DB - START");
            Console.WriteLine("getting orders ...");
            DebugTimer.Init(" - get orders from DB", false);

            // получить заказы из БД
            _dbOrders.Clear();
            try
            {
//                DateTime dt1 = DateTime.Now;
                DBOrderHelper.LoadDBOrders();
                //DateTime dt1 = DateTime.Now;
                //Debug.Print(" -- load orders: " + (DateTime.Now - dt1).ToString());
            }
            catch (Exception ex)
            {
                return ex.Message;
            }

            // получено заказов из БД
            if (_isLogOrderDetails)
            {
                string ids = string.Join(",", _dbOrders.Select(o => o.Id.ToString()+"/"+o.Number.ToString()));
                AppEnv.WriteLogOrderDetails(" - from DB {0} id/Num: {1}", _dbOrders.Count, ids);
            }

            // цикл по полученным из БД заказам
            if (_dbOrders != null)
            {
                lock (_dbOrders)
                {
                    // обновить словарь блюд с их количеством, которые ожидают готовки или уже готовятся
                    sLog = "   updateDishesQuantityDict()...";
                    AppEnv.WriteLogOrderDetails(sLog);
                    try
                    {
                        updateDishesQuantityDict(_dbOrders);
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
                    int[] delIds = _dbOrders.Select(o => o.Id).ToArray();
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
                    foreach (Order dbOrder in _dbOrders)
                    {
                        // пропустить, если заказ заблокирован от изменений по таймеру
                        if ((_lockedOrders != null) && _lockedOrders.ContainsKey(dbOrder.Id))
                        {
                            AppEnv.WriteLogOrderDetails("   appModel: locked order Id " + dbOrder.Id.ToString());
                            continue;
                        }

                        if (_orders.ContainsKey(dbOrder.Id))
                        {
                            sLog = string.Format("   appModel: update {0}/{1}", dbOrder.Id, dbOrder.Number);
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
                            sLog = string.Format("   appModel: add new {0}/{1}", dbOrder.Id, dbOrder.Number);
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
            DateTime dtUpdate = _currentDate.Add(AppEnv.TimeOfAutoCloseYesterdayOrders);
            if (DateTime.Now < dtUpdate) return false;

            AppEnv.WriteLogOrderDetails(" - обновить статус вчерашних заказов... - START");
            using (KDSEntities db = new KDSEntities())
            {
                // вчерашние заказы, точнее заказы, у которых CreateDate меньше начала текущего дня (полночь)
                // с учетом смещения от полуночи назад, созданные в течение которого вчерашние заказы будут отображаться на КДСе
                double d1 = AppProperties.GetDoubleProperty("MidnightShiftShowYesterdayOrders");
                dtUpdate = DateTime.Today.AddHours(-d1);

                string sqlText = string.Format("SELECT Id FROM [Order] WHERE (OrderStatusId < 3) AND (CreateDate < {0})", dtUpdate.ToSQLExpr());
                int cntDishes = 0, cntOrders, iCnt;
                List<int> idList = db.Database.SqlQuery<int>(sqlText).ToList();
                cntOrders = idList.Count;  // кол-во заказов
                // обновить статус в БД
                bool retVal = false;
                try
                {
                    foreach (int orderId in idList)
                    {
                        // обновить статус блюд
                        sqlText = string.Format("UPDATE [OrderDish] SET DishStatusId = 9 WHERE (OrderId={0})", orderId.ToString());
                        iCnt = db.Database.ExecuteSqlCommand(sqlText);
                        cntDishes += iCnt;
                        // обновить статус заказа
                        sqlText = string.Format("UPDATE [Order] SET OrderStatusId = 9, QueueStatusId = 9 WHERE (Id={0})", orderId.ToString());
                        db.Database.ExecuteSqlCommand(sqlText);
                    }
                    AppEnv.WriteLogOrderDetails(" - обновлено заказов {0} (блюд {1}) - FINISH", cntOrders, cntDishes);
                    retVal = true;
                }
                catch (Exception ex)
                {
                    AppEnv.WriteLogOrderDetails(" - ошибка обновления заказов: {0} ({1}) - FINISH", AppEnv.GetShortErrMessage(ex), sqlText);
                }

                return retVal;
            }
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


    }  // class
}
