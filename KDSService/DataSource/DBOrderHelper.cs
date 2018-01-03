using IntegraLib;
using KDSService.Lib;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace KDSService.DataSource
{
    internal class DBOrderHelper
    {
        private static bool _isBusy;
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
            if (_isBusy) throw new Exception("DbContext/KDSEntities is busy!");

            _isBusy = true;
            _dbOrders.Clear();
            tmpDishes.Clear();

            // отобрать сразу все БЛЮДА с максимальными условиями (кол-во порций, допустимые статусы, неотображаемые цеха)
            // разницей в днях между текущей датой и CreateDate блюда
            string sqlText = string.Format("SELECT * FROM OrderDish WHERE ({0} AND (CreateDate >= {1}))", _sDishWhere, getDateFrom());

            // блюда заказа
            DateTime dt1 = DateTime.Now;
            AppEnv.WriteLogMSSQL(sqlText);

            List<OrderDish> dbOrderDish = getDbOrderDish(sqlText);
            if (dbOrderDish != null) tmpDishes.AddRange(dbOrderDish);
            TimeSpan ts1 = DateTime.Now - dt1;
            AppEnv.WriteLogMSSQL(" - rows {0} - {1}", tmpDishes.Count, ts1.ToString());

            if (tmpDishes.Count != 0)
            {
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
                    AppEnv.WriteLogMSSQL(sqlText);
                    dt1 = DateTime.Now;
                    _dbOrders.AddRange(getDbOrders(sqlText));
                    AppEnv.WriteLogMSSQL(" - rows {0} - {1}", _dbOrders.Count, (DateTime.Now - dt1).ToString());

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

                            AppEnv.WriteLogMSSQL(sqlText);
                            dt1 = DateTime.Now;
                            int iAffected = DBContext.Execute(sqlText);
                            AppEnv.WriteLogMSSQL(" - affected {0} - {1}", iAffected.ToString(), (DateTime.Now - dt1).ToString());
                        }
                    }
                }  // foreach (Order order in _dbOrders)
            }  // if (tmpDishes.Count != 0)

            _isBusy = false;
        }  // method


        private static List<OrderDish> getDbOrderDish(string sqlText)
        {
            DataTable dt = DBContext.GetQueryTable(sqlText);
            if (dt == null)
            {
                AppEnv.WriteLogMSSQL(DBContext.ErrorMessage);
                return null;
            }

            List<OrderDish> retVal = new List<OrderDish>();
            foreach (DataRow dtRow in dt.Rows)
            {
                OrderDish od = new OrderDish();
                od.Id = dtRow.ToInt("Id");
                od.OrderId = dtRow.ToInt("OrderId");
                od.DishStatusId = dtRow.ToInt("DishStatusId");
                od.DepartmentId = dtRow.ToInt("DepartmentId");
                od.UID = System.Convert.ToString(dtRow["UID"]);
                od.DishName = System.Convert.ToString(dtRow["DishName"]);
                od.FilingNumber = dtRow.ToInt("FilingNumber");
                od.Quantity = dtRow.ToDecimal("Quantity");
                od.ParentUid = System.Convert.ToString(dtRow["ParentUid"]);
                od.EstimatedTime = dtRow.ToInt("EstimatedTime");
                od.Comment = System.Convert.ToString(dtRow["Comment"]);
                od.CreateDate = dtRow.ToDateTime("CreateDate");
                od.StartDate = dtRow.ToDateTime("StartDate");
                od.UID1C = System.Convert.ToString(dtRow["UID1C"]);
                od.DelayedStartTime = dtRow.ToInt("DelayedStartTime");

                retVal.Add(od);
            }
            return retVal;
        }
        private static IEnumerable<Order> getDbOrders(string sqlText)
        {
            List<Order> retVal = new List<Order>();

            DataTable dt = DBContext.GetQueryTable(sqlText);
            foreach (DataRow dtRow in dt.Rows)
            {
                Order ord = new Order();
                ord.Id = dtRow.ToInt("Id");
                ord.OrderStatusId = dtRow.ToInt("OrderStatusId");
                ord.DepartmentId = dtRow.ToInt("DepartmentId");
                ord.UID = System.Convert.ToString(dtRow["UID"]);
                ord.Number = dtRow.ToInt("Number");
                ord.TableNumber = System.Convert.ToString(dtRow["TableNumber"]);
                ord.CreateDate = dtRow.ToDateTime("CreateDate");
                ord.RoomNumber = System.Convert.ToString(dtRow["RoomNumber"]);
                ord.StartDate = dtRow.ToDateTime("StartDate");
                ord.Waiter = System.Convert.ToString(dtRow["Waiter"]);
                ord.QueueStatusId = dtRow.ToInt("QueueStatusId");
                ord.LanguageTypeId = dtRow.ToInt("LanguageTypeId");
                ord.DivisionColorRGB = System.Convert.ToString(dtRow["DivisionColorRGB"]);

                retVal.Add(ord);
            }

            return retVal;
        }

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

        internal static List<int> getOrderIdsForAutoStateChange(string sqlText)
        {
            List<int> retVal = new List<int>();

            DataTable dt = DBContext.GetQueryTable(sqlText);
            if (dt == null) return retVal;

            // первое поле - Id
            int id;
            foreach (DataRow dtRow in dt.Rows)
            {
                id = System.Convert.ToInt32(dtRow[0]);
                if (id > 0) retVal.Add(id);
            }

            return retVal;
        }


        internal static OrderDishRunTime getOrderDishRunTimeByOrderDishId(int orderDishId)
        {
            string sqlText = string.Format("SELECT * FROM [OrderDishRunTime] WHERE ([OrderDishId] = {0})", orderDishId.ToString());
            return getOrderDishRunTime(sqlText);
        }
        internal static OrderDishRunTime getOrderDishRunTimeById(int id)
        {
            string sqlText = string.Format("SELECT * FROM [OrderDishRunTime] WHERE ([Id] = {0})", id.ToString());
            return getOrderDishRunTime(sqlText);
        }

        internal static OrderDishRunTime getOrderDishRunTime(string sqlText)
        {
            DataTable dt = DBContext.GetQueryTable(sqlText);
            if ((dt == null) || (dt.Rows.Count == 0)) return null;

            OrderDishRunTime retVal = new OrderDishRunTime();
            DataRow dtRow = dt.Rows[0];

            retVal.Id = dtRow.ToInt("Id");
            retVal.OrderDishId = dtRow.ToInt("OrderDishId");

            retVal.InitDate = dtRow.ToDateTime("InitDate");
            retVal.WaitingCookTS = dtRow.ToInt("WaitingCookTS");

            retVal.CookingStartDate = dtRow.ToDateTime("CookingStartDate");
            retVal.CookingTS = dtRow.ToInt("CookingTS");

            retVal.ReadyDate = dtRow.ToDateTime("ReadyDate");
            retVal.WaitingTakeTS = dtRow.ToInt("WaitingTakeTS");

            retVal.TakeDate = dtRow.ToDateTime("TakeDate");
            retVal.WaitingCommitTS = dtRow.ToInt("WaitingCommitTS");

            retVal.CommitDate = dtRow.ToDateTime("CommitDate");
            retVal.CancelDate = dtRow.ToDateTime("CancelDate");
            retVal.CancelConfirmedDate = dtRow.ToDateTime("CancelConfirmedDate");

            retVal.ReadyTS = dtRow.ToInt("ReadyTS");
            retVal.ReadyConfirmedDate = dtRow.ToDateTime("ReadyConfirmedDate");

            return retVal;
        }

        internal static OrderDishRunTime newOrderDishRunTime(int dishId)
        {
            string sqlText = string.Format("INSERT INTO [OrderDishRunTime] (OrderDishId) VALUES ({0}); SELECT @@IDENTITY", dishId.ToString());

            DataTable dt = DBContext.GetQueryTable(sqlText);
            if (dt == null)
            {
                AppEnv.WriteLogMSSQL("Error: " + DBContext.ErrorMessage);
                return null;
            }

            // запись создана и получен ее Id
            int newId = Convert.ToInt32(dt.Rows[0][0]);
            // вернуть запись с данным Id
            return getOrderDishRunTimeById(newId);
        }

    }  // class

    public static class DBTypesExtensions
    {
        public static int ToInt(this DataRow source, string intFieldName)
        {
            if (source.IsNull(intFieldName))
                return 0;
            else
                return System.Convert.ToInt32(source[intFieldName]);
        }

        public static decimal ToDecimal(this DataRow source, string decimalFieldName)
        {
            if (source.IsNull(decimalFieldName))
                return 0m;
            else
                return System.Convert.ToDecimal(source[decimalFieldName], System.Globalization.CultureInfo.InvariantCulture);
        }

        public static DateTime ToDateTime(this DataRow source, string datetimeFieldName)
        {
            if (source.IsNull(datetimeFieldName))
                return DateTime.MinValue;
            else
                return System.Convert.ToDateTime(source[datetimeFieldName], System.Globalization.CultureInfo.InvariantCulture);
        }

    }  // class DBTypesExtensions

}
