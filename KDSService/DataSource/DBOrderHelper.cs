﻿using IntegraLib;
using KDSConsoleSvcHost;
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

        private static List<int> _ids;
        private static List<OrderDish> tmpDishes;
        private static List<string> _dishUIDs;

        internal static HashSet<int> AllowedKDSStatuses
        {
            set
            {
                string sBuf = string.Join(",", value);
                if ((_sDishWhere.IsNull() == false) && (_sDishWhere.Contains("DishStatusId") == false))
                    _sDishWhere += " AND (DishStatusId In ("+ sBuf +"))";
            }
        }
        internal static HashSet<int> UnusedDeps
        {
            set
            {
                if (value != null)
                {
                    string sBuf = string.Join(",", value);
                    if ((_sDishWhere.IsNull() == false) && (_sDishWhere.Contains("DepartmentId") == false))
                        _sDishWhere += " AND NOT (DepartmentId In (" + sBuf + "))";
                }
            }
        }


        private static double _midnightShiftHour;
        // постоянные условия отбора блюд из БД
        private static string _sDishWhere;


        static DBOrderHelper()
        {
            _dbOrders = new List<Order>();
            tmpDishes = new List<OrderDish>();
            _ids = new List<int>();
            _dishUIDs = new List<string>();

            _midnightShiftHour = AppProperties.GetDoubleProperty("MidnightShiftShowYesterdayOrders");
            // постоянные условия отбора блюд из БД: 
            _sDishWhere = "(Quantity != 0)";
        }


        internal static void LoadDBOrders()
        {
            _dbOrders.Clear();
            tmpDishes.Clear();

            // отобрать сразу все БЛЮДА с максимальными условиями (кол-во порций, допустимые статусы, неотображаемые цеха)
            // разницей в днях между текущей датой и CreateDate блюда
            string sqlText = string.Format("SELECT * FROM OrderDish WHERE ({0} AND (CreateDate >= {1})) ORDER BY Id", _sDishWhere, getDateFrom());

            using (KDSEntities db = new KDSEntities())
            {
                // блюда заказа
                tmpDishes.AddRange(db.Database.SqlQuery<OrderDish>(sqlText));
                if (tmpDishes.Count == 0) return;

                // поиск "висячих" ингредиентов, т.е. блюда нет (по статусу от службы), а ингредиенты - есть
                // собрать Id заказов в набор
                _ids.Clear();
                _ids.AddRange(tmpDishes.Select(d => d.OrderId).Distinct());
                bool isDelLostIngr = false;
                foreach (int id in _ids)
                {
                    // блюда заказа
                    IEnumerable<OrderDish> ordDishes = (from d in tmpDishes where d.OrderId == id select d);
                    if (ordDishes.Count() > 0)
                    {
                        // UID-ы блюд данного заказа
                        _dishUIDs.Clear();
                        _dishUIDs.AddRange(ordDishes.Where(d => d.ParentUid.IsNull()).Select(d => d.UID));

                        if (_dishUIDs.Count > 0)
                        {
                            foreach (OrderDish dish in
                                (from d in ordDishes where !d.ParentUid.IsNull() && !_dishUIDs.Contains(d.ParentUid) select d).ToList())
                            {
                                tmpDishes.Remove(dish);
                                isDelLostIngr = true;
                            }
                        }
                    }
                }

                // после возможного удаления "висячих" ингредиентов, собрать Id заказов в набор
                if (isDelLostIngr == true)
                {
                    _ids.Clear();
                    _ids.AddRange(tmpDishes.Select(d => d.OrderId).Distinct());
                }

                if (_ids.Count > 0)
                {
                    // получить из БД заказы
                    sqlText = string.Format("SELECT * FROM [Order] WHERE (Id In ({0}))", string.Join(",", _ids));
                    _dbOrders.AddRange(db.Database.SqlQuery<Order>(sqlText));

                    // добавить к заказам блюда
                    foreach (Order order in _dbOrders)
                    {
                        foreach (OrderDish dish in (from d in tmpDishes where d.OrderId == order.Id select d))
                        {
                            order.Dishes.Add(dish);
                        }
                    }
                }

                // узнать общий статус всех (оставшихся) блюд
                int idStatus;
                foreach (Order order in _dbOrders)
                {
                    idStatus = getStatusAllDishesInt(order.Dishes);

                    if ((idStatus != -1) && (order.OrderStatusId != idStatus))
                    {
                        sqlText = null;
                        if (idStatus == 0)  // все блюда в ожидании, а заказ не в готовке
                        {
                            if (order.OrderStatusId != 1)
                                sqlText = string.Format("UPDATE [Order] SET OrderStatusId = 1 WHERE (Id={0})", order.Id);
                        }
                        else if (idStatus == 3)  // если Выдан, то менять и QueueStatusId
                            sqlText = string.Format("UPDATE [Order] SET OrderStatusId = 3, QueueStatusId = 2 WHERE (Id={0})", order.Id);
                        else
                            sqlText = string.Format("UPDATE [Order] SET OrderStatusId = {0} WHERE (Id={1})", idStatus, order.Id);

                        if (sqlText != null)
                        {
                            AppEnv.WriteLogTraceMessage("   заказ {0}/{1} из статуса {2} переведен в {3}, т.к. все его блюда находятся в этом состоянии.", order.Id, order.Number, order.OrderStatusId, idStatus);

                            db.Database.ExecuteSqlCommand(sqlText);
                        }
                    }
                }

            }  // using

        }  // method


        private static string getDateFrom()
        {
            DateTime dtFrom = DateTime.Today;
            if (_midnightShiftHour != 0) dtFrom = dtFrom.AddHours(-_midnightShiftHour);
            
            return dtFrom.ToSQLExpr();
        }


        // получить общий статус всех блюд
        private static int getStatusAllDishesInt(ICollection<OrderDish> dishes)
        {
            if ((dishes == null) || (dishes.Count == 0)) return -1;

            int retVal = -1;
            foreach (OrderDish dish in dishes)
            {
                if (retVal == -1)
                    retVal = dish.DishStatusId ?? -1;
                else 
                    if (retVal != dish.DishStatusId) return -1;
            }

            return retVal;
        }

    }  // class
}