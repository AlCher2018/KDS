﻿using System;
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

namespace KDSService.AppModel
{
    // основной класс службы
    public class OrdersModel : IDisposable
    {
        private Dictionary<int, OrderModel> _orders;
        public Dictionary<int, OrderModel> Orders { get { return _orders; } }

        /// <summary>
        /// учитывать ли отмененные блюда при подсчете одновременно готовящихся блюд для автостарта готовки
        /// </summary>
        private bool _takeCancelledInAutostartCooking;
        private string _errorMsg;

        private bool _isTraceLog;
        private bool _changeStatusYesterdayOrders;

        // CONSTRUCTOR
        public OrdersModel()
        {
            _orders = new Dictionary<int, OrderModel>();

            // учитывать ли отмененные блюда при подсчете одновременно готовящихся блюд для автостарта готовки
            _takeCancelledInAutostartCooking = (bool)AppEnv.GetAppProperty("TakeCancelledInAutostartCooking", false);

            _isTraceLog = (bool)AppEnv.GetAppProperty("IsWriteTraceMessages", false);
            _changeStatusYesterdayOrders = (AppEnv.TimeOfAutoCloseYesterdayOrders != TimeSpan.Zero);
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
                AppEnv.WriteLogTraceMessage("orders count from DB: {0} (ids: {1})", dbOrders.Count, ids);
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

                    //// для последующех обработок удалить из заказов блюда с ненужными статусами
                    //foreach (Order dbOrder in dbOrders)
                    //{
                    //    // массив блюд для удаления
                    //    OrderDish[] dishesForDel = dbOrder.OrderDish.Where(d => isProcessingDishStatusId(d)==false).ToArray();
                    //    // удаление ненужных блюд
                    //    foreach (OrderDish delDish in dishesForDel) dbOrder.OrderDish.Remove(delDish);
                    //}

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
                AppEnv.WriteLogTraceMessage("   orders count for clients: {0} (ids: {1})", _orders.Count, ids);
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
        // процедуры проверки числ.значения статуса Заказа/Блюда (из БД) на обработку в КДС
        // статус: 0 - ожидает приготовления, 1 - готовится, 2 - готово, 3 - выдано, 4 - отмена, 5 - зафиксировано
        // дата создания: только текущая!
        private bool isProcessingOrderStatusId(Order order)
        {
            return ( 
                ((order.OrderStatus.AppName == OrderStatusEnum.WaitingCook.ToString())
                || (order.OrderStatus.AppName == OrderStatusEnum.Cooking.ToString())
                || (order.OrderStatus.AppName == OrderStatusEnum.Ready.ToString())
                || (order.OrderStatus.AppName == OrderStatusEnum.Cancelled.ToString())
                || (order.OrderStatus.AppName == OrderStatusEnum.Transferred.ToString())
                || (order.OrderStatus.AppName == OrderStatusEnum.ReadyConfirmed.ToString()))
                && (order.CreateDate.Date.Equals(DateTime.Today.Date))
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
                    sb.Append(string.Format("depId: {0}, qnt: {1}", item.Key, item.Value));
                }
                
                AppEnv.WriteLogTraceMessage("   обновляю словарь количества готовящихся блюд: " + sb.ToString());
            }

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
