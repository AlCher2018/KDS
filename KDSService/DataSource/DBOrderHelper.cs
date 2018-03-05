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
            if (_isBusy) throw new Exception("DbContext/KDSEntities is busy!");

            _isBusy = true;
            _dbOrders.Clear();
            tmpDishes.Clear();

            // отобрать сразу все БЛЮДА с максимальными условиями (кол-во порций, допустимые статусы, неотображаемые цеха)
            // разницей в днях между текущей датой и CreateDate блюда
            DateTime dtFrom = DateTime.Today;
            if (_midnightShiftHour != 0) dtFrom = dtFrom.AddHours(-_midnightShiftHour);

            string sqlText = string.Format("declare @dt datetime={1}; SELECT * FROM OrderDish WHERE ({0} AND (CreateDate >= @dt))", _sDishWhere, dtFrom.ToSQLExpr());

            // блюда заказа
            DateTime dt1 = DateTime.Now;

            List<OrderDish> dbOrderDish = getDbOrderDish(sqlText);
            if (dbOrderDish != null) tmpDishes.AddRange(dbOrderDish);
            TimeSpan ts1 = DateTime.Now - dt1;
            AppLib.WriteLogTraceMessage(" - rows {0} - {1}", tmpDishes.Count, ts1.ToString());

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
                    dt1 = DateTime.Now;
                    _dbOrders.AddRange(getDbOrdersList(sqlText));
                    AppLib.WriteLogTraceMessage(" - rows {0} - {1}", _dbOrders.Count, (DateTime.Now - dt1).ToString());

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
                            AppLib.WriteLogTraceMessage("   заказ {0}/{1} из статуса {2} переведен в {3}, т.к. все его блюда находятся в этом состоянии.", order.Id, order.Number, order.OrderStatusId, idStatus);

                            dt1 = DateTime.Now;
                            int iAffected = DBContext.ExecuteCommandAsync(sqlText);
                            AppLib.WriteLogTraceMessage(" - affected {0} - {1}", iAffected.ToString(), (DateTime.Now - dt1).ToString());
                        }
                    }
                }  // foreach (Order order in _dbOrders)
            }  // if (tmpDishes.Count != 0)

            _isBusy = false;
        }  // method


        #region Orders
        private static IEnumerable<Order> getDbOrdersList(string sqlText)
        {
            List<Order> retVal = new List<Order>();

            DataTable dt = null;
            using (DBContext db = new DBContext())
            {
                dt = db.GetQueryTable(sqlText);
                _errMsg = db.ErrMsg;
            }
            if (dt == null) return null;

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

        // первое поле в sql-запросе должно быть Id
        internal static List<int> getOrderIdsList(string sqlText)
        {
            List<int> retVal = new List<int>();

            DataTable dt = null;
            using (DBContext db = new DBContext())
            {
                dt = db.GetQueryTable(sqlText);
                _errMsg = db.ErrMsg;
            }
            if (dt == null) return retVal;

            int id;
            foreach (DataRow dtRow in dt.Rows)
            {
                id = System.Convert.ToInt32(dtRow[0]);
                if (id > 0) retVal.Add(id);
            }
            dt.Dispose();

            return retVal;
        }

        internal static OrderRunTime getOrderRunTimeRecord(int orderId)
        {
            OrderRunTime runtimeRecord = null;
            runtimeRecord = DBOrderHelper.getOrderRunTimeByOrderId(orderId);

            // если еще нет записи в БД, то добавить ее
            if (runtimeRecord == null)
            {
                runtimeRecord = DBOrderHelper.newOrderRunTime(orderId);
                if (runtimeRecord == null)
                {
                    _errMsg = string.Format("Ошибка создания записи в таблице OrderRunTime для заказа id {0}", orderId);
                    AppLib.WriteLogErrorMessage(_errMsg);
                    runtimeRecord = null;
                }
            }

            return runtimeRecord;
        }

        private static OrderRunTime getOrderRunTimeByOrderId(int orderId)
        {
            string sqlText = string.Format("SELECT * FROM [OrderRunTime] WHERE ([OrderId] = {0})", orderId.ToString());
            return getOrderRunTime(sqlText);
        }
        private static OrderRunTime getOrderRunTimeById(int id)
        {
            string sqlText = string.Format("SELECT * FROM [OrderRunTime] WHERE ([Id] = {0})", id.ToString());
            return getOrderRunTime(sqlText);
        }
        private static OrderRunTime getOrderRunTime(string sqlText)
        {
            DataTable dt = null;
            using (DBContext db = new DBContext())
            {
                dt = db.GetQueryTable(sqlText);
                _errMsg = db.ErrMsg;
            }
            if ((dt == null) || (dt.Rows.Count == 0)) return null;

            OrderRunTime retVal = new OrderRunTime();
            DataRow dtRow = dt.Rows[0];

            retVal.Id = dtRow.ToInt("Id");
            retVal.OrderId = dtRow.ToInt("OrderId");

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

            dt.Dispose();

            return retVal;
        }

        private static OrderRunTime newOrderRunTime(int orderId)
        {
            string sqlText = $"INSERT INTO [OrderRunTime] (OrderId) VALUES ({orderId})";

            int newId = DBContext.InsertRecordAndReturnNewId(sqlText);
            if (DBContext.LastErrorText.IsNull() == false)
            {
                return null;
            }

            // вернуть запись с данным Id
            OrderRunTime retVal = getOrderRunTimeById(newId);
            return retVal;
        }

        internal static bool updateOrderRunTime(OrderRunTime runTimeRecord)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("UPDATE [OrderRunTime] SET ");
            sb.AppendFormat("[OrderId] = {0}", runTimeRecord.OrderId.ToString());
            sb.AppendFormat(", [InitDate] = {0}", runTimeRecord.InitDate.ToSQLExpr());
            sb.AppendFormat(", [WaitingCookTS] = {0}", runTimeRecord.WaitingCookTS.ToString());
            sb.AppendFormat(", [CookingStartDate] = {0}", runTimeRecord.CookingStartDate.ToSQLExpr());
            sb.AppendFormat(", [CookingTS] = {0}", runTimeRecord.CookingTS.ToString());
            sb.AppendFormat(", [ReadyDate] = {0}", runTimeRecord.ReadyDate.ToSQLExpr());
            sb.AppendFormat(", [WaitingTakeTS] = {0}", runTimeRecord.WaitingTakeTS.ToString());
            sb.AppendFormat(", [TakeDate] = {0}", runTimeRecord.TakeDate.ToSQLExpr());
            sb.AppendFormat(", [WaitingCommitTS] = {0}", runTimeRecord.WaitingCommitTS.ToString());
            sb.AppendFormat(", [CommitDate] = {0}", runTimeRecord.CommitDate.ToSQLExpr());
            sb.AppendFormat(", [CancelDate] = {0}", runTimeRecord.CancelDate.ToSQLExpr());
            sb.AppendFormat(", [CancelConfirmedDate] = {0}", runTimeRecord.CancelConfirmedDate.ToSQLExpr());
            sb.AppendFormat(", [ReadyTS] = {0}", runTimeRecord.ReadyTS.ToString());
            sb.AppendFormat(", [ReadyConfirmedDate] = {0}", runTimeRecord.ReadyConfirmedDate.ToSQLExpr());
            sb.AppendFormat(" WHERE ([Id]={0})", runTimeRecord.Id.ToString());
            string sqlText = sb.ToString();
            sb = null;

            int result = DBContext.ExecuteDMLAndGetAffectedRowCount(sqlText);

            bool retVal = false;
            if (result < 1)
            {
                _errMsg = DBContext.LastErrorText;
            }
            else
                retVal = true;

            return retVal;
        }

        internal static bool updateOrderStatus(int orderId, OrderStatusEnum status, bool isUseReadyConfirmed)
        {
            // записать в поле QueueStatusId значение для очереди заказов
            string sqlSetQueueValueText = null;
            if (status == OrderStatusEnum.Cooking)
            {
                sqlSetQueueValueText  = "[QueueStatusId] = 0";
            }
            else if ((!isUseReadyConfirmed && (status == OrderStatusEnum.Ready))
                || (isUseReadyConfirmed && (status == OrderStatusEnum.ReadyConfirmed)))
            {
                sqlSetQueueValueText = "[QueueStatusId] = 1";
            }
                
            else if (status == OrderStatusEnum.Took)
            {
                sqlSetQueueValueText = "[QueueStatusId] = 2";
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("UPDATE [Order] SET [OrderStatusId] = {0}", ((int)status).ToString());
            if ((sqlSetQueueValueText.IsNull() == false) && (sqlSetQueueValueText.Length > 0))
                sb.Append(", " + sqlSetQueueValueText);
            sb.AppendFormat(" WHERE ([Id] = {0})", orderId.ToString());
            string sqlText = sb.ToString();

            int result = DBContext.ExecuteDMLAndGetAffectedRowCount(sqlText);
            bool retVal = false;
            if (result < 1)
            {
                _errMsg = DBContext.LastErrorText;
            }
            else
                retVal = true;

            return retVal;
        }

        #endregion


        #region OrderDishes

        private static List<OrderDish> getDbOrderDish(string sqlText)
        {
            DataTable dt = null;
            using (DBContext db = new DBContext())
            {
                dt = db.GetQueryTable(sqlText);
            }
            if (dt == null)
            {
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
            dt.Dispose();

            return retVal;
        }

        // получить общий статус всех блюд
        private static int getStatusAllDishesInt(ICollection<OrderDish> dishes)
        {
            if ((dishes == null) || (dishes.Count == 0)) return -1;

            int retVal = -1;
            foreach (OrderDish dish in dishes)
            {
                if (retVal == -1)
                    retVal = dish.DishStatusId;
                else 
                    if (retVal != dish.DishStatusId) return -1;
            }

            return retVal;
        }

        internal static OrderDishRunTime getOrderDishRunTimeRecord(int dishId)
        {
            OrderDishRunTime runtimeRecord = null;
            runtimeRecord = DBOrderHelper.getOrderDishRunTimeByOrderDishId(dishId);

            // если еще нет записи в БД, то добавить ее
            if (runtimeRecord == null)
            {
                runtimeRecord = DBOrderHelper.newOrderDishRunTime(dishId);
                if (runtimeRecord == null)
                {
                    _errMsg = string.Format("Ошибка создания записи в таблице OrderDishRunTime для блюда id {0}", dishId);
                    AppLib.WriteLogErrorMessage(_errMsg);
                    runtimeRecord = null;
                }
            }

            return runtimeRecord;
        }

        private static OrderDishRunTime getOrderDishRunTimeByOrderDishId(int orderDishId)
        {
            string sqlText = string.Format("SELECT * FROM [OrderDishRunTime] WHERE ([OrderDishId] = {0})", orderDishId.ToString());
            return getOrderDishRunTime(sqlText);
        }
        private static OrderDishRunTime getOrderDishRunTimeById(int id)
        {
            string sqlText = string.Format("SELECT * FROM [OrderDishRunTime] WHERE ([Id] = {0})", id.ToString());
            return getOrderDishRunTime(sqlText);
        }

        private static OrderDishRunTime getOrderDishRunTime(string sqlText)
        {
            DataTable dt = null;
            using (DBContext db = new DBContext())
            {
                dt = db.GetQueryTable(sqlText);
            }
            if ((dt == null) || (dt.Rows.Count == 0))
            {
                _errMsg = DBContext.LastErrorText;
                return null;
            }

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

            dt.Dispose();
            return retVal;
        }

        private static OrderDishRunTime newOrderDishRunTime(int dishId)
        {
            string sqlText = $"INSERT INTO [OrderDishRunTime] (OrderDishId) VALUES ({dishId})";

            int newId = DBContext.InsertRecordAndReturnNewId(sqlText);
            if (DBContext.LastErrorText.IsNull() == false)
            {
                return null;
            }

            // вернуть запись с данным Id
            OrderDishRunTime retVal = getOrderDishRunTimeById(newId);
            return retVal;
        }

        internal static bool updateOrderDishStatus(int dishId, OrderStatusEnum status)
        {
            string sqlText = $"UPDATE [OrderDish] SET [DishStatusId] = {((int)status).ToString()} WHERE ([Id] = {dishId})";

            int result = DBContext.ExecuteDMLAndGetAffectedRowCount(sqlText);
            bool retVal = false;
            if (result < 1)
            {
                _errMsg = DBContext.LastErrorText;
            }
            else
                retVal = true;

            return retVal;
        }

        internal static bool updateOrderDishRunTime(OrderDishRunTime runTimeRecord)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("UPDATE [OrderDishRunTime] SET ");
            sb.AppendFormat("[OrderDishId] = {0}", runTimeRecord.OrderDishId.ToString());
            sb.AppendFormat(", [InitDate] = {0}", runTimeRecord.InitDate.ToSQLExpr());
            sb.AppendFormat(", [WaitingCookTS] = {0}", runTimeRecord.WaitingCookTS.ToString());
            sb.AppendFormat(", [CookingStartDate] = {0}", runTimeRecord.CookingStartDate.ToSQLExpr());
            sb.AppendFormat(", [CookingTS] = {0}", runTimeRecord.CookingTS.ToString());
            sb.AppendFormat(", [ReadyDate] = {0}", runTimeRecord.ReadyDate.ToSQLExpr());
            sb.AppendFormat(", [WaitingTakeTS] = {0}", runTimeRecord.WaitingTakeTS.ToString());
            sb.AppendFormat(", [TakeDate] = {0}", runTimeRecord.TakeDate.ToSQLExpr());
            sb.AppendFormat(", [WaitingCommitTS] = {0}", runTimeRecord.WaitingCommitTS.ToString());
            sb.AppendFormat(", [CommitDate] = {0}", runTimeRecord.CommitDate.ToSQLExpr());
            sb.AppendFormat(", [CancelDate] = {0}", runTimeRecord.CancelDate.ToSQLExpr());
            sb.AppendFormat(", [CancelConfirmedDate] = {0}", runTimeRecord.CancelConfirmedDate.ToSQLExpr());
            sb.AppendFormat(", [ReadyTS] = {0}", runTimeRecord.ReadyTS.ToString());
            sb.AppendFormat(", [ReadyConfirmedDate] = {0}", runTimeRecord.ReadyConfirmedDate.ToSQLExpr());
            sb.AppendFormat(" WHERE ([Id]={0})", runTimeRecord.Id.ToString());
            string sqlText = sb.ToString();
            sb = null;

            int result = DBContext.ExecuteDMLAndGetAffectedRowCount(sqlText);

            bool retVal = false;
            if (result < 1)
            {
                _errMsg = DBContext.LastErrorText;
            }
            else
                retVal = true;

            return retVal;
        }

        internal static bool updateOrderDishReturnTime(int dishId, DateTime returnDate, int statusFrom, int statusFromTimeSpan, int statusTo)
        {
            string sqlText = string.Format("INSERT INTO [OrderDishReturnTime] ([OrderDishId], [ReturnDate], [StatusFrom], [StatusFromTimeSpan], [StatusTo]) VALUES ({0}, {1}, {2}, {3}, {4})",
                dishId.ToString(), returnDate.ToSQLExpr(), statusFrom.ToString(), statusFromTimeSpan.ToString(), statusTo.ToString());

            int result = DBContext.ExecuteDMLAndGetAffectedRowCount(sqlText);

            bool retVal = false;
            if (result < 1)
            {
                _errMsg = DBContext.LastErrorText;
            }
            else
                retVal = true;

            return retVal;
        }

        #endregion

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

    public static class DBTypesExtensions
    {
        public static int ToInt(this DataRow source, string fieldName)
        {
            if (source.IsNull(fieldName))
                return 0;
            else
                return System.Convert.ToInt32(source[fieldName]);
        }

        public static decimal ToDecimal(this DataRow source, string fieldName)
        {
            if (source.IsNull(fieldName))
                return 0m;
            else
                return System.Convert.ToDecimal(source[fieldName], System.Globalization.CultureInfo.InvariantCulture);
        }

        public static DateTime ToDateTime(this DataRow source, string fieldName)
        {
            if (source.IsNull(fieldName))
                return DateTime.MinValue;
            else
                return System.Convert.ToDateTime(source[fieldName], System.Globalization.CultureInfo.InvariantCulture);
        }

        public static bool ToBool(this DataRow source, string fieldName)
        {
            if (source.IsNull(fieldName))
                return false;
            else
                return System.Convert.ToBoolean(source[fieldName], System.Globalization.CultureInfo.InvariantCulture);
        }

    }  // class DBTypesExtensions

}
