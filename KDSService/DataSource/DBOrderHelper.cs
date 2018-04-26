using IntegraLib;
using KDSService.Lib;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KDSService.AppModel;

namespace KDSService.DataSource
{
    internal class DBOrderHelper
    {
        // MS SQL data type Datetime: January 1, 1753, through December 31, 9999
        private static DateTime sqlMinDate = new DateTime(1753, 1, 1);

        private static object _locker = new object();
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

        private static string _errMsg;
        public static string ErrorMessage { get { return _errMsg; } }


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
            if (_errMsg != null) _errMsg = null;
            // блюда заказа
            try
            {
                _dbOrders.Clear();
                tmpDishes.Clear();

                // прочитать блюда из БД и положить в tmpDishes
                DateTime dt1 = DateTime.Now;
                List<OrderDish> dbOrderDish = getDbOrderDish();
                AppLib.WriteLogTraceMessage(" - rows {0} - {1}", (dbOrderDish==null? 0: dbOrderDish.Count), (DateTime.Now - dt1).ToString());

                if ((_errMsg == null) && (dbOrderDish != null) && (dbOrderDish.Count > 0))
                {
                    tmpDishes.AddRange(dbOrderDish);
                    processDBOrderDishes();
                }
            }

            catch (Exception ex)
            {
                _errMsg =  ex.ToString();
            }
        }  // method

        private static void processDBOrderDishes()
        {
            string sqlText; DateTime dt1;

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
                dt1 = DateTime.Now;
                List<Order> dbOrders = getDbOrdersList(sqlText);
                AppLib.WriteLogTraceMessage(" - rows {0} - {1}", (dbOrders == null ? 0 : dbOrders.Count), (DateTime.Now - dt1).ToString());
                if ((_errMsg != null) || (dbOrders.Count == 0)) return;

                _dbOrders.AddRange(dbOrders);

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
                    else if (idStatus == 2)  // если все блюда Готовы, то менять и QueueStatusId
                        sqlText = string.Format("UPDATE [Order] SET OrderStatusId = 2, QueueStatusId = 1 WHERE (Id={0})", order.Id);
                    else if (idStatus == 3)  // если все блюда Выданы, то менять и QueueStatusId
                        sqlText = string.Format("UPDATE [Order] SET OrderStatusId = 3, QueueStatusId = 2 WHERE (Id={0})", order.Id);
                    else
                        sqlText = string.Format("UPDATE [Order] SET OrderStatusId = {0} WHERE (Id={1})", idStatus, order.Id);

                    if (sqlText != null)
                    {
                        AppLib.WriteLogTraceMessage("   заказ {0}/{1} из статуса {2} переведен в {3}, т.к. все его блюда находятся в этом состоянии.", order.Id, order.Number, order.OrderStatusId, idStatus);

                        dt1 = DateTime.Now;
                        int iAffected = DBContext.ExecuteCommandAsync(sqlText);
                        AppLib.WriteLogTraceMessage(" - affected {0} - {1}", iAffected.ToString(), (DateTime.Now - dt1).ToString());
                    }
                }
            }  // foreach (Order order in _dbOrders)
        }

        private static List<Order> getDbOrdersList(string sqlText)
        {
            lock (_locker)
            {
                List<Order> retVal = new List<Order>();

                if (_errMsg != null) _errMsg = null;
                DataTable dt = null;
                try
                {
                    DBContext db = new DBContext();
                    dt = db.GetQueryTable(sqlText, IsolationLevel.Unspecified);
                    _errMsg = db.ErrMsg;
                    db.Dispose();
                }
                catch (Exception ex)
                {
                    _errMsg = ex.ToString();
                }
                if (dt == null) return retVal;

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
                dt.Dispose();

                return retVal;
            }
        }

        // отобрать сразу все БЛЮДА с максимальными условиями (кол-во порций, допустимые статусы, неотображаемые цеха)
        // разницей в днях между текущей датой и CreateDate блюда
        private static List<OrderDish> getDbOrderDish()
        {
            lock (_locker)
            {
                DateTime dtFrom = DateTime.Today;
                if (_midnightShiftHour != 0) dtFrom = dtFrom.AddHours(-_midnightShiftHour);
                string sqlText = string.Format("SELECT * FROM OrderDish WHERE ({0} AND (CreateDate >= {1}))", _sDishWhere, dtFrom.ToSQLExpr());

                List<OrderDish> retVal = new List<OrderDish>();

                if (_errMsg != null) _errMsg = null;
                DataTable dt = null;
                try
                {
                    DBContext db = new DBContext();
                    dt = db.GetQueryTable(sqlText, IsolationLevel.Unspecified);
                    _errMsg = db.ErrMsg;
                    db.Dispose();
                }
                catch (Exception ex)
                {
                    _errMsg = ex.ToString();
                }
                if (dt == null) return retVal;

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
                dt.Dispose();

                return retVal;
            }
        }

        // получить общий статус всех блюд
        private static int getStatusAllDishesInt(ICollection<OrderDish> dishes)
        {
            if ((dishes == null) || (dishes.Count == 0)) return -1;

            lock (_locker)
            {
                int retVal = -1;
                foreach (OrderDish dish in dishes)
                {
                    // получить статус из первого объекта
                    if (retVal == -1)
                    {
                        retVal = dish.DishStatusId;
                    }
                    // для последующих
                    else
                    {
                        if (retVal != dish.DishStatusId)
                        {
                            retVal = -1;
                            break;
                        };
                    }
                }

                return retVal;
            }
        }

        #region DB methods
        internal static bool CheckAppDBTable()
        {
            StringBuilder sb = new StringBuilder();
            _errMsg = null;

            bool b1 = checkDBTable("Department");
            if (b1 == false) sb.Append(_errMsg);

            bool b2 = checkDBTable("OrderStatus");
            if (b2 == false)
            {
                if (sb.Length > 0) sb.Append(" ");
                sb.Append(_errMsg);
            }

            if (sb.Length > 0) _errMsg = sb.ToString();
            return b1 && b2;
        }

        private static bool checkDBTable(string tableName)
        {
            bool retVal = false;
            string sqlText = string.Format("SELECT Count(*) FROM [{0}]", tableName);

            int iResult = Convert.ToInt32(DBContext.ExecuteScalarAsync(sqlText));
            if (DBContext.LastErrorText.IsNull() == false)
            {
                _errMsg = string.Format("В БД [{0}] отсутствует или пустая таблица [{1}].", DBContext.GetDBName(), tableName);
                AppLib.WriteLogErrorMessage(" - таблица [{0}] ОТСУТСТВУЕТ!!", tableName);
            }
            else
            {
                AppLib.WriteLogInfoMessage(" - таблица [{0}] содержит {1} записей.", tableName, iResult.ToString());
                retVal = true;
            }

            return retVal;
        }

        #endregion

        #region справочники
        // СПРАВОЧНИК СТАТУСОВ ЗАКАЗА/БЛЮДА
        public static List<OrderStatusModel> GetOrderStatusesList()
        {
            List<OrderStatusModel> retVal = dbTableToList<OrderStatusModel>("OrderStatus", getStatusModelObjectFromDataRow);
            return retVal;
        }
        // СПРАВОЧНИК ОТДЕЛОВ
        public static List<DepartmentModel> GetDepartmentsList()
        {
            List<DepartmentModel> retVal = dbTableToList<DepartmentModel>("Department", getDepModelObjectFromDataRow);
            return retVal;
        }

        private static OrderStatusModel getStatusModelObjectFromDataRow(DataRow dtRow)
        {
            OrderStatusModel os = new OrderStatusModel()
            {
                Id = dtRow.ToInt("Id"),
                Name = System.Convert.ToString(dtRow["Name"]),
                AppName = System.Convert.ToString(dtRow["AppName"]),
                Description = System.Convert.ToString(dtRow["Description"])
            };
            return os;
        }
        private static DepartmentModel getDepModelObjectFromDataRow(DataRow dtRow)
        {
            DepartmentModel dm = new DepartmentModel()
            {
                Id = dtRow.ToInt("Id"),
                IsAutoStart = dtRow.ToBool("IsAutoStart"),
                Name = System.Convert.ToString(dtRow["Name"]),
                UID = System.Convert.ToString(dtRow["UID"]),
                DishQuantity = dtRow.ToInt("DishQuantity")
            };
            if (dm.IsAutoStart) dm.Name += "/автостарт";

            return dm;
        }

        // создание набора сущностей из БД, заполнение полей объекта производится в делегате delegateNewInstance
        private static List<T> dbTableToList<T>(string dbTableName, Func<DataRow, T> delegateNewInstance)
        {
            string sqlText = string.Format("SELECT * FROM [{0}]", dbTableName);

            DataTable dt = null;
            using (DBContext db = new DBContext())
            {
                dt = db.GetQueryTable(sqlText);
                _errMsg = db.ErrMsg;
            }
            if (dt == null) return null;

            List<T> retVal = new List<T>();
            try
            {
                foreach (DataRow dtRow in dt.Rows)
                {
                    T os = delegateNewInstance(dtRow);
                    retVal.Add(os);
                }
            }
            catch (Exception ex)
            {
                _errMsg = ErrorHelper.GetShortErrMessage(ex);
                retVal = null;
            }

            dt.Dispose();
            return retVal;
        }

        #endregion

    }  // class

}
