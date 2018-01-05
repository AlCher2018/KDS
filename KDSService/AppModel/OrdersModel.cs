﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Diagnostics;
using System.Threading;
using KDSService.DataSource;
using KDSService.Lib;
using IntegraLib;


namespace KDSService.AppModel
{
    // основной класс службы
    public class OrdersModel : IDisposable
    {
        private Random rnd = new Random();

        private HashSet<int> _allowedKDSStatuses;
        private HashSet<int> _unUsedDeps;
        private Dictionary<int, bool> _lockedOrders;

        // буферы для хранения коллекций заказов
        private List<Order> _dbOrders;
        private Dictionary<int, OrderModel> _orders;
        public Dictionary<int, OrderModel> Orders { get { return _orders; } }

        /// <summary>
        /// учитывать ли отмененные блюда при подсчете одновременно готовящихся блюд для автостарта готовки
        /// </summary>
        private bool _takeCancelledInAutostartCooking;

        private bool _isLogOrderDetails;
        private bool _changeStatusYesterdayOrdersCfg, _changeStatusYesterdayOrdersCurrent;
        private DateTime _currentDate;

        private List<Order> _delOrders;

        private DateTime _tmpDT;

       
        // CONSTRUCTOR
        public OrdersModel()
        {
            // статусы заказов, которые выбираются из БД для отображения на КДС
            _allowedKDSStatuses = new HashSet<int>();
            _allowedKDSStatuses.Add((int)OrderStatusEnum.WaitingCook);
            _allowedKDSStatuses.Add((int)OrderStatusEnum.Cooking);
            _allowedKDSStatuses.Add((int)OrderStatusEnum.Ready);
            if (AppProperties.GetBoolProperty("IsReadTakenDishes")) _allowedKDSStatuses.Add((int)OrderStatusEnum.Took);
            _allowedKDSStatuses.Add((int)OrderStatusEnum.Cancelled);
            _allowedKDSStatuses.Add((int)OrderStatusEnum.Transferred);
            _allowedKDSStatuses.Add((int)OrderStatusEnum.ReadyConfirmed);
            DBOrderHelper.AllowedKDSStatuses = _allowedKDSStatuses;

            // неиспользуемые отделы, отфильтровываются на службе
            _unUsedDeps = (HashSet<int>)AppProperties.GetProperty("UnusedDepartments");
            DBOrderHelper.UnusedDeps = _unUsedDeps;

            // буферы для хранения коллекций заказов
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
            string sLog = "";

            #region автосброс вчерашних заказов
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
            #endregion

            AppEnv.WriteLogOrderDetails("GET ORDERS FROM DB - START");
            Console.WriteLine("getting orders ...");
            DebugTimer.Init(" - get orders from DB", false);

            // получить заказы из БД
            try
            {
                DBOrderHelper.LoadDBOrders();
            }
            catch (Exception ex)
            {
                return "MSSQLServer Error: " + ErrorHelper.GetShortErrMessage(ex);
            }

            // получено заказов из БД
            if (_isLogOrderDetails)
            {
                string ids = (_dbOrders.Count>50) ? "> 50" : getOrdersLogString(_dbOrders);
                AppEnv.WriteLogOrderDetails(" - from DB {0} id/Num/dishes: {1}", _dbOrders.Count, ids);
            }

            // цикл по полученным из БД заказам
            if (_dbOrders != null)
            {
                #region *** ОБНОВЛЕНИЕ ВНУТРЕННЕГО СЛОВАРЯ ЗАКАЗОВ _orders коллекцией из БД _dbOrders ***
                // заблокировать _orders
                lock (_orders)
                {
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
                    int iCnt = 0;
                    foreach (Order dbOrder in _dbOrders)
                    {
                        iCnt++;
                        // пропустить, если заказ заблокирован от изменений по таймеру при длительных операциях чтения из БД
                        if ((_lockedOrders != null) && _lockedOrders.ContainsKey(dbOrder.Id))
                        {
                            AppEnv.WriteLogOrderDetails("   appModel: locked order Id " + dbOrder.Id.ToString());
                            // если заказ стал неотображаемый, то удалить его из коллекции
                            if (_orders.ContainsKey(dbOrder.Id))
                            {
                                OrderModel om = _orders[dbOrder.Id];
                                if (_allowedKDSStatuses.Contains(om.OrderStatusId) == false)
                                {
                                    _orders.Remove(dbOrder.Id);
                                    AppEnv.WriteLogOrderDetails("             remove from appModel set");
                                }
                            }
                            continue;
                        }

                        if (_orders.ContainsKey(dbOrder.Id))
                        {
                            //sLog = string.Format("   appModel: update {0}/{1}", dbOrder.Id, dbOrder.Number);
                            //AppEnv.WriteLogOrderDetails(sLog + " - START");
                            //_tmpDT = DateTime.Now;

                            try
                            {
                                _orders[dbOrder.Id].UpdateFromDBEntity(dbOrder);
                            }
                            catch (Exception ex)
                            {
                                AppEnv.WriteLogErrorMessage("Ошибка обновления служебного словаря для OrderId = {1}: {0}", ex.ToString(), dbOrder.Id);
                            }

                            //AppEnv.WriteLogOrderDetails(sLog + " - FINISH - " + _tmpPeriod());
                        }
                        // добавление заказа в словарь
                        else
                        {
                            sLog = string.Format("   appModel: add new {0}/{1}", dbOrder.Id, dbOrder.Number);
                            AppEnv.WriteLogOrderDetails(sLog + " - START"); _tmpDT = DateTime.Now;
                            try
                            {
                                OrderModel newOrder = new OrderModel(dbOrder);
                                _orders.Add(dbOrder.Id, newOrder);
                            }
                            catch (Exception ex)
                            {
                                AppEnv.WriteLogErrorMessage("Ошибка добавления заказа в служебный словарь: {0}", ex.ToString());
                            }
                            AppEnv.WriteLogOrderDetails(sLog + " - FINISH - " + _tmpPeriod());
                        }  //curOrder
                    }  // foreach
                }  // lock _orders
                #endregion

                #region обновить словарь блюд с их количеством, которые ожидают готовки или уже готовятся
                sLog = "   updateDishesQuantityDict()...";
                AppEnv.WriteLogOrderDetails(sLog);
                try
                {
                    updateDishesQuantityDict(_orders);
                    if (_isLogOrderDetails) AppEnv.WriteLogOrderDetails("   - result (depId/count): " + getDishesQtyString());

                    // проверяем условие автоматического перехода в режим приготовления
                    checkAutoStartCooking(_orders);
                }
                catch (Exception ex)
                {
                    if (_isLogOrderDetails) AppEnv.WriteLogOrderDetails("   error: " + ex.ToString());
                    else AppEnv.WriteLogErrorMessage("Ошибка обновления словаря количества готовящихся блюд по цехам: " + ex.ToString());
                }
                #endregion
            }

            if (_isLogOrderDetails)
            {
                string ids = (_dbOrders.Count > 50) ? "> 50" : getOrdersLogString(_dbOrders);
                AppEnv.WriteLogOrderDetails(" - to clients {0} id/Num/dishes: {1}", _orders.Count, ids);
            }

            // очистить словарь заблокированных заказов
            _lockedOrders = (Dictionary<int, bool>)AppProperties.GetProperty("lockedOrders");
            if ((_lockedOrders != null) && (_lockedOrders.Count > 0)) _lockedOrders.Clear();

            AppEnv.WriteLogOrderDetails("get orders from DB - FINISH" + " - " + DebugTimer.GetInterval());
            Console.WriteLine("... " + _orders.Count.ToString() + "  " + DebugTimer.GetInterval()); 

            return null;
        }  // method


        private string getOrdersLogString(List<Order> orders)
        {
            return string.Join(",",
                orders.Select(o =>
                    string.Format("{0}/{1}/{2}", o.Id.ToString(), o.Number.ToString(), o.Dishes.Count.ToString()))
                );
        }


        // ОБНОВИТЬ СТАТУС "ВЧЕРАШНИХ" ЗАКАЗОВ
        // "вчерашние" заказы - это заказы, у которых CreateDate меньше начала текущего дня (полночь)
        // минус смещение от полуночи назад (MidnightShiftShowYesterdayOrders)
        // "Вчерашним" заказам статус устанавливается в 9 и они НЕ будут отображаться на КДСе. 
        private bool updateYesterdayOrdersStatus()
        {
            // дата/время, КОГДА необходимо обновить статус вчерашних заказов
            DateTime dtUpdate = _currentDate.Add(AppEnv.TimeOfAutoCloseYesterdayOrders);
            if (DateTime.Now < dtUpdate) return false;

            AppEnv.WriteLogOrderDetails(" - обновить статус вчерашних заказов... - START");
            _tmpDT = DateTime.Now;

            // дата/время, С КОТОРОГО заказы считаются "сегодяшними"
            double d1 = AppProperties.GetDoubleProperty("MidnightShiftShowYesterdayOrders");
            dtUpdate = DateTime.Today.AddHours(-d1);

            string sqlText = null;
            int cntDishes = 0, cntOrders = 0, iAffected;
            bool retVal = false;
            try
            {
                sqlText = string.Format("declare @dt datetime = {0}; SELECT Id FROM [Order] WHERE (OrderStatusId < 3) AND (CreateDate < @dt)", dtUpdate.ToSQLExpr());
                List<int> idList = DBOrderHelper.getOrderIdsList(sqlText);
                foreach (int orderId in idList)
                {
                    // обновить статус блюд
                    sqlText = string.Format("UPDATE [OrderDish] SET DishStatusId = 9 WHERE (OrderId={0})", orderId.ToString());
                    iAffected = DBContext.Execute(sqlText);
                    cntDishes += iAffected;
                    // обновить статус заказа
                    sqlText = string.Format("UPDATE [Order] SET OrderStatusId = 9, QueueStatusId = 9 WHERE (Id={0})", orderId.ToString());
                    iAffected = DBContext.Execute(sqlText);
                    cntOrders += iAffected;
                }
                retVal = true;
            }
            catch (Exception ex)
            {
                AppEnv.WriteLogErrorMessage(" - ошибка обновления заказов: {0} ({1})", ErrorHelper.GetShortErrMessage(ex), sqlText);
            }
            AppEnv.WriteLogOrderDetails(" - обновлено заказов {0} (блюд {1}) - FINISH - {2}", cntOrders, cntDishes, _tmpPeriod());

            return retVal;
        }

        private string _tmpPeriod()
        {
            if (_tmpDT.IsZero())
                return "";
            else
                return (DateTime.Now - _tmpDT).ToString();
        }


        #region работа с коллекцией количества готовящихся блюд по цехам (напр.печати), автостарт
        //  обновить коллекцию
        private void updateDishesQuantityDict(Dictionary<int, OrderModel> orders)
        {
            // получить словарь по отделам (направлениям печати)
            Dictionary<int, decimal> dishesQty = (Dictionary<int, decimal>)AppProperties.GetProperty("dishesQty");
            // очистить кол-во
            List<int> keys = dishesQty.Keys.ToList();
            foreach (int key in keys) dishesQty[key] = 0m;

            decimal qnt;
            OrderStatusEnum eStatus;
            bool isTakeQuantity, isTakeCancelled;
            foreach (OrderModel order in orders.Values)   // orders loop
            {
                foreach (OrderDishModel dish in order.Dishes.Values)  //  dishes loop
                {
                    eStatus = (OrderStatusEnum)dish.DishStatusId;
                    isTakeCancelled = (_takeCancelledInAutostartCooking && (eStatus == OrderStatusEnum.Cancelled));
                    isTakeQuantity = dish.ParentUid.IsNull()
                        && ((eStatus == OrderStatusEnum.Cooking) || isTakeCancelled);

                    // кол-во блюд для подсчета округляем до верхнего целого
                    qnt = Math.Ceiling(dish.Quantity);
                    qnt = isTakeQuantity ? (isTakeCancelled ? -qnt : qnt) : 0m;

                    if ((dishesQty.ContainsKey(dish.DepartmentId)) && (qnt != 0m)) dishesQty[dish.DepartmentId] += qnt;
                }  // loop
            }  // loop
        }  // method

        private string getDishesQtyString()
        {
            // получить словарь по отделам (направлениям печати)
            Dictionary<int, decimal> dishesQty = (Dictionary<int, decimal>)AppProperties.GetProperty("dishesQty");

            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<int, decimal> item in dishesQty)
            {
                if (sb.Length > 0) sb.Append("; ");
                sb.Append(string.Format("{0}/{1}", item.Key, item.Value));
            }
            return sb.ToString();
        }

        // проверка возможности АВТОМАТИЧЕСКОГО перехода в состояние Cooking
        private void checkAutoStartCooking(Dictionary<int, OrderModel> orders)
        {
            OrderStatusEnum eStatus;
            decimal depDepth, dishQnt;
            bool isStart;
            Dictionary<int, decimal> dishesQty = (Dictionary<int, decimal>)AppProperties.GetProperty("dishesQty");

            foreach (OrderModel order in orders.Values)   // orders loop
            {
                foreach (OrderDishModel dish in order.Dishes.Values)  //  dishes loop
                {
                    eStatus = (OrderStatusEnum)dish.DishStatusId;
                    if (eStatus <= OrderStatusEnum.WaitingCook)
                    {
                        // 1. для отдела установлен автоматический старт приготовления и текущая дата больше даты ожидаемого времени начала приготовления
                        DateTime n = DateTime.Now;
                        isStart = (ModelDicts.GetDepAutoStart(dish.DepartmentId)
                            && (n >= dish.CreateDate.AddSeconds(dish.DelayedStartTime)));

                        // 2. проверяем общее кол-во такого блюда в заказах, если установлено кол-во одновременно готовящихся блюд
                        if (isStart == true)
                        {
                            depDepth = ModelDicts.GetDepDepthCount(dish.DepartmentId);
                            if ((dishesQty != null) && (dishesQty.ContainsKey(dish.DepartmentId)) && (depDepth > 0))
                            {
                                dishQnt = Math.Ceiling(dish.Quantity);
                                if (dishesQty[dish.DepartmentId] < depDepth)
                                {
                                    isStart = true;
                                    // обновить кол-во готовящихся блюд в словаре
                                    dishesQty[dish.DepartmentId] += dishQnt;
                                }
                                else
                                {
                                    isStart = false;
                                }
                            }
                        }

                        // автостарт приготовления блюда
                        if (isStart)
                        {
                            DateTime dtTmr = DateTime.Now;
                            string sLogMsg = string.Format(" - AutoStart Cooking Id {0}/{1}", dish.Id, dish.Name);
                            AppEnv.WriteLogOrderDetails(sLogMsg + " - start");

                            dish.UpdateStatus(OrderStatusEnum.Cooking);

                            sLogMsg += " - finish - " + (DateTime.Now - dtTmr).ToString();
                            AppEnv.WriteLogOrderDetails(sLogMsg);
                        }
                    }
                }  // loop
            }  // loop
        }
        #endregion

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
