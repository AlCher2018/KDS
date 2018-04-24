using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using KDSService.DataSource;
using IntegraLib;
using KDSService.Lib;
using System.Data;

namespace KDSService.AppModel
{
    [DataContract]
    [Serializable]
    public class OrderModel : IDisposable
    {
        [DataMember]
        public int Id { get; set; }

        [DataMember]
        public int Number { get; set; }

        [DataMember]
        public string Uid { get; set; }

        [DataMember]
        public DateTime CreateDate { get; set; }

        [DataMember]
        public string HallName { get; set; }

        [DataMember]
        public string TableName { get; set; }

        [DataMember]
        public string Waiter { get; set; }

        [DataMember]
        public string DivisionColorRGB { get; set; }

        [DataMember]
        public int OrderStatusId { get; set; }

        private OrderStatusEnum Status;

        private Dictionary<int, OrderDishModel> _dishesDict;
        [DataMember]
        public Dictionary<int, OrderDishModel> Dishes
        {
            get { return _dishesDict; }
            set { _dishesDict = value; }
        }

        // форматированное представление временного промежутка для внешних клиентов
        [DataMember]
        public string WaitingTimerString
        {
            get
            {
                string retVal = "***";
                if ((_curTimer != null) && _curTimer.Enabled)
                {
                    TimeSpan tsTimerValue = TimeSpan.Zero;
                    try
                    {
                          tsTimerValue = TimeSpan.FromSeconds(_curTimer.ValueTS);
                    }
                    catch (Exception ex)
                    {
                        throw new FaultException("Ошибка получения периода времени от таймера: " + ex.Message, new FaultCode("TimeCounter class"));
                    }

                    retVal = (tsTimerValue.Days > 0) ? tsTimerValue.ToString(@"d\.hh\:mm\:ss") : tsTimerValue.ToString(@"hh\:mm\:ss");
                    // отрицательное время
                    if (tsTimerValue.Ticks < 0) retVal = "-" + retVal;
                }
                return retVal;
            }
            set { }  // необходимо для DataMember
        }

        #region Fields
        // MS SQL data type Datetime: January 1, 1753, through December 31, 9999
        private DateTime sqlMinDate = new DateTime(1753, 1, 1);

        // FIELDS
        // накопительные счетчики нахождения в конкретном состоянии
        private Dictionary<OrderStatusEnum, TimeCounter> _tsTimersDict; // словарь накопительных счетчиков для различных состояний
        private TimeCounter _curTimer;  // текущий таймер для выдачи клиенту значения таймера

        // записи БД для сохранения блюда
        [NonSerialized]
        private OrderRunTime _dbRunTimeRecord = null;   // запись дат/времени прямого пути 
        private string _serviceErrorMessage;

        private bool _isUpdStatusFromDishes = false;
        private bool _isUseReadyConfirmed;
        #endregion

        // for serialization
        public OrderModel()
        {
        }

        // *** CONSTRUCTOR  ***
        public OrderModel(Order dbOrder)
        {
            Id = dbOrder.Id; Uid = dbOrder.UID; Number = dbOrder.Number;
            TableName = dbOrder.TableNumber;
            CreateDate = dbOrder.CreateDate;
            HallName = dbOrder.RoomNumber;
            Waiter = dbOrder.Waiter;
            DivisionColorRGB = dbOrder.DivisionColorRGB;

            OrderStatusId = dbOrder.OrderStatusId;
            Status = (OrderStatusEnum)dbOrder.OrderStatusId; //AppLib.GetStatusEnumFromNullableInt(dbOrder.OrderStatusId);

            _isUseReadyConfirmed = AppProperties.GetBoolProperty("UseReadyConfirmedState");

            _dishesDict = new Dictionary<int, OrderDishModel>();
            // получить отсоединенную RunTime запись из таблицы состояний
            _dbRunTimeRecord = getOrderRunTimeRecord(dbOrder.Id);

            // создать словарь накопительных счетчиков
            _tsTimersDict = new Dictionary<OrderStatusEnum, TimeCounter>();
            // таймер времени приготовления
            _tsTimersDict.Add(OrderStatusEnum.Cooking, new TimeCounter() { Name = OrderStatusEnum.Cooking.ToString() });
            // таймер времени ожидания выдачи, нахождение в состоянии Готов
            _tsTimersDict.Add(OrderStatusEnum.Ready, new TimeCounter() { Name = OrderStatusEnum.Ready.ToString() });
            if (_isUseReadyConfirmed)
                _tsTimersDict.Add(OrderStatusEnum.ReadyConfirmed, new TimeCounter() { Name = OrderStatusEnum.ReadyConfirmed.ToString() });
            // таймер времени ожидания фиксации заказа, нахождение в состоянии Выдано
            _tsTimersDict.Add(OrderStatusEnum.Took, new TimeCounter() { Name = OrderStatusEnum.Took.ToString() });
            // таймер нахождения в состоянии отмены
            _tsTimersDict.Add(OrderStatusEnum.Cancelled, new TimeCounter() { Name = OrderStatusEnum.Cancelled.ToString() });

            // для нового объекта статус по умолчанию - В ПРОЦЕССЕ ГОТОВКИ
            if (this.OrderStatusId < 1)
            {
                OrderStatusEnum newStatus = OrderStatusEnum.Cooking;
                UpdateStatus(newStatus, false);
            }
            else
            {
                // обновить статус заказа по статусам всех блюд
                OrderStatusEnum eStatusAllDishes = AppLib.GetStatusAllDishes(dbOrder.Dishes);
                if ((eStatusAllDishes != OrderStatusEnum.None) 
                    && (this.Status != eStatusAllDishes) 
                    && ((int)this.Status < (int)eStatusAllDishes))
                {
                    UpdateStatus(eStatusAllDishes, false);
                }
            }

            StatusDTS statusDTS = getStatusRunTimeDTS(this.Status);
            DateTime dtEnterState = statusDTS.DateEntered;
            if (dtEnterState.IsZero())
            {
                dtEnterState = DateTime.Now;
                setStatusRunTimeDTS(this.Status, dtEnterState, -1);
                saveRunTimeRecord();
                statusDTS = getStatusRunTimeDTS(this.Status);
            }
            startStatusTimer(statusDTS);

            // добавить блюда к заказу
            //   расставить сначала блюдо, потом его ингредиенты, т.к. ингр.могут идти ПЕРЕД блюдом
            List<OrderDish> dishParentList = dbOrder.Dishes.Where(d => d.ParentUid.IsNull()).ToList();
            Dictionary<int, OrderDish> dAll = new Dictionary<int,OrderDish>();
            foreach (OrderDish dishParent in dishParentList)
            {
                if (dAll.ContainsKey(dishParent.Id) == false)
                {
                    dAll.Add(dishParent.Id, dishParent);

                    // отобрать ингредиенты
                    List<OrderDish> ingrList = dbOrder.Dishes.Where(ingr => (ingr.ParentUid == dishParent.UID) && (ingr.Id != dishParent.Id)).ToList();
                    foreach (OrderDish ingr in ingrList)
                        if (dAll.ContainsKey(ingr.Id) == false) dAll.Add(ingr.Id, ingr);
                }
            }

            foreach (OrderDish dbDish in dAll.Values)   // dbOrder.OrderDish
            {
                if (this._dishesDict.ContainsKey(dbDish.Id) == false)
                {
                    OrderDishModel newDish = new OrderDishModel(dbDish, this);
                    this._dishesDict.Add(newDish.Id, newDish);
                }
            }

        }  // ctor

        // копия только значений, без ссылок
        public OrderModel Copy()
        {
            OrderModel retVal = (OrderModel)this.MemberwiseClone();

            retVal._dishesDict = new Dictionary<int, OrderDishModel>();

            retVal._tsTimersDict = null;
            retVal._curTimer = this._curTimer;
            retVal._dbRunTimeRecord = null;

            return retVal;
        }

        // ПОЛНОЕ ОБНОВЛЕНИЕ заказа из БД-сущности (вместе с блюдами)
        public void UpdateFromDBEntity(Order dbOrder)
        {
            lock (this)
            {
                if (Uid != dbOrder.UID) Uid = dbOrder.UID;
                if (Number != dbOrder.Number) Number = dbOrder.Number;
                if (TableName != dbOrder.TableNumber) TableName = dbOrder.TableNumber;
                if (CreateDate != dbOrder.CreateDate) CreateDate = dbOrder.CreateDate;
                if (HallName != dbOrder.RoomNumber) HallName = dbOrder.RoomNumber;
                if (Waiter != dbOrder.Waiter) Waiter = dbOrder.Waiter;
                if (DivisionColorRGB != dbOrder.DivisionColorRGB) DivisionColorRGB = dbOrder.DivisionColorRGB;

                OrderStatusEnum newStatus = AppLib.GetStatusEnumFromNullableInt(dbOrder.OrderStatusId);

                if (dbOrder.OrderStatusId < 1)
                {
                    newStatus = OrderStatusEnum.Cooking;
                    saveStatusToDB(newStatus);
                }

                UpdateStatus(newStatus, false);

                // *** СЛОВАРЬ БЛЮД  ***
                // удалить блюда из внутр.модели заказа, которых уже нет в БД и которые НЕЗАБЛОКИРОВАНЫ
                List<int> idDishList = dbOrder.Dishes.Select(d => d.Id).ToList();  // все Id блюд из БД
                List<int> idForRemove = _dishesDict.Keys.Except(idDishList).ToList();  // Id блюд для удаления
                foreach (int idDish in idForRemove)
                {
                    if (OrderLocker.IsLockDish(idDish)) continue;
                    _dishesDict[idDish].Dispose(); _dishesDict.Remove(idDish);
                }

                _isUpdStatusFromDishes = false;

                // обновить состояние или добавить блюда
                foreach (OrderDish dbDish in dbOrder.Dishes)
                {
                    // пропустить, если блюдо находится в словаре заблокированных от изменения по таймеру
                    if (OrderLocker.IsLockDish(dbDish.Id)) continue;

                    if (this._dishesDict.ContainsKey(dbDish.Id))  // есть такое блюдо во внут.словаре - обновить из БД
                    {
                        this._dishesDict[dbDish.Id].UpdateFromDBEntity(dbDish);
                    }
                    // иначе - добавить блюдо/ингр
                    else
                    {
                        OrderDishModel newDish = new OrderDishModel(dbDish, this);
                        this._dishesDict.Add(newDish.Id, newDish);
                    }
                }

            }  // lock
        }  // method UpdateFromDBEntity


        // внешнее обновление СОСТОЯНИЯ заказа
        // параметр isUpdateDishStatus = true, если заказ БЫЛ обновлен ИЗВНЕ (из БД/КДС), то в этом случае дату входа в новое состояние для блюд берем из заказа
        //          isUpdateDishStatus = false, если заказ БУДЕТ обновлен по общему состоянию всех блюд
        public bool UpdateStatus(OrderStatusEnum newStatus, bool isUpdateDishStatus, string machineName = null)
        {
            // если статус не поменялся, то попытаться обновить только статус блюд
            if (this.Status == newStatus) return false;

            bool retVal = false;
            string sLogMsg = string.Format(" - ORDER.UpdateStatus() Id/Num {0}/{1}, from {2} to {3}", this.Id, this.Number, this.Status.ToString(), newStatus.ToString());
            DateTime dtTmr = DateTime.Now;
            if (machineName == null) AppLib.WriteLogOrderDetails(sLogMsg + " - START");
            else AppLib.WriteLogClientAction(machineName, sLogMsg + " - START");

            // время нахождения в ПРЕДЫДУЩЕМ состоянии, в секундах
            int secondsInPrevState = 0;
            if (_curTimer != null)   // если есть таймер предыдущего состояния
            {
                _curTimer.Stop(); // остановить таймер состояния
                // получить время нахождения в состоянии с момента последнего входа
                secondsInPrevState = _curTimer.IncrementTS;
            }
            // дата входа в новое состояние
            DateTime dtEnterToNewStatus = DateTime.Now;
            // обновление заказа по ПОСЛЕДНЕМУ состоянию блюд, если они есть
            if ((isUpdateDishStatus == false) && (_dishesDict.Values.Count > 0))
                dtEnterToNewStatus = getMaxDishEnterStateDate(newStatus);

            // сохранить новый статус ОБЪЕКТА в БД
            if (saveStatusToDB(newStatus, machineName))
            {
                // изменить статус в ОБЪЕКТЕ
                OrderStatusEnum preStatus = this.Status;
                Status = newStatus;
                OrderStatusId = (int)Status;

                // сохраняем в записи RunTimeRecord дату входа в новое состояние
                setStatusRunTimeDTS(this.Status, dtEnterToNewStatus, -1);
                //  и время нахождения в предыдущем состоянии
                setStatusRunTimeDTS(preStatus, DateTime.MinValue, secondsInPrevState);
                // и в БД
                saveRunTimeRecord();

                // запуск таймера для нового состояния
                StatusDTS statusDTS = getStatusRunTimeDTS(this.Status);
                startStatusTimer(statusDTS);

                // обновить уже существующие блюда при внешнем изменении статуса заказа
                if (isUpdateDishStatus)
                {
                    bool dishUpdSuccess = true;  // для получения результата обновления через AND
                    try
                    {
                        foreach (OrderDishModel modelDish in _dishesDict.Values)
                        {
                            // только для блюд
                            if (modelDish.ParentUid.IsNull())
                            {
                                // дату входа в состояние берем из заказа, а время нахожд.в предыд.состоянии из самого блюда
                                dishUpdSuccess &= modelDish.UpdateStatus(newStatus, statusDTS.DateEntered);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLib.WriteLogErrorMessage("Ошибка обновления статуса блюд при обновлении статуса заказа {0}/{1} с {2} на {3}: {4}", this.Id, this.Number, this.Status, newStatus, ex.ToString());
                        dishUpdSuccess = false;
                    }
                }
                retVal = true;
            }

            sLogMsg += " - FINISH - " + (DateTime.Now - dtTmr).ToString();
            if (machineName == null) AppLib.WriteLogOrderDetails(sLogMsg);
            else AppLib.WriteLogClientAction(machineName, sLogMsg);

            return retVal;
        }  // method

        private void startStatusTimer(StatusDTS statusDTS)
        {
            DateTime dtEnterToStatus = statusDTS.DateEntered;

            if (_tsTimersDict.ContainsKey(this.Status) 
                && ((_curTimer == null) || (_curTimer.Enabled == false) || (_curTimer != _tsTimersDict[this.Status]) 
                    || (_curTimer.StartDT != _tsTimersDict[this.Status].StartDT))
                )
            {
                _curTimer = _tsTimersDict[this.Status];
                _curTimer.Start(dtEnterToStatus);
            }
        }


        // обновление состояния заказа проверкой состояний всех блюд
        // установить сост.заказа в 0,2,3,4,5 если ВСЕ блюда наход.в этом состоянии
        // установить сост.заказа в 1, если ХОТЬ одно блюдо наход.в сост. 1
        public void UpdateStatusByVerificationDishes()
        {
            int iStat = -1;
            HashSet<int> unUsedDeps = (HashSet<int>)AppProperties.GetProperty("UnusedDepartments");

            foreach (OrderDishModel dish in _dishesDict.Values)
            {
                // пропуск блюд, входящих в неотображаемые цеха
                // или имеющие отрицательное количество - отмененные
                if ((unUsedDeps != null) && (unUsedDeps.Contains(dish.DepartmentId))) continue;
                if (dish.Quantity < 0) continue;

                // первое доступное блюдо
                if (iStat == -1) iStat = dish.DishStatusId;

                // если хотя бы одно блюдо в состояние Готовится, то и заказ переводим в это состояние, если он был не в этом состоянии
                if ((dish.DishStatusId == (int)OrderStatusEnum.Cooking)
                    && (this.OrderStatusId != (int)OrderStatusEnum.Cooking))
                {
                    AppLib.WriteLogOrderDetails(string.Format("   изменение статуса заказа {0}/{1} с {2} на 'Готовится', т.к. есть блюдо в состоянии 'Готовится'"
                        , this.Id, this.Number
                        , ((OrderStatusEnum)this.OrderStatusId).ToString()));

                    UpdateStatus(OrderStatusEnum.Cooking, false);
                    _isUpdStatusFromDishes = true;
                    return;
                }

                // есть неодинаковый статус - выйти
                if (iStat != dish.DishStatusId) return;
            }

            if ((iStat != -1) && (this.OrderStatusId != iStat))
            {
                string sLog = string.Format("   изменение статуса заказа {0}/{1} на {2} согласно общему статусу всех блюд ПРИ ОБНОВЛЕНИИ БЛЮДА...", this.Id, this.Number, iStat);
                AppLib.WriteLogOrderDetails(sLog);

                OrderStatusEnum statDishes = AppLib.GetStatusEnumFromNullableInt(iStat);
                UpdateStatus(statDishes, false);
                _isUpdStatusFromDishes = true;
            }
        }  // method

        internal int GetInstanceSize()
        {
            int retVal = 0;

            int szInt = sizeof(int), szDate = 8;
            // Id, Number, Uid, CreateDate, HallName, TableName, Waiter, DivisionColorRGB, OrderStatusId,
            // WaitingTimerString
            retVal = szInt + szInt + (Uid.IsNull()?0:Uid.Length) + szDate 
                + (HallName.IsNull() ? 0 : HallName.Length) 
                + (TableName.IsNull() ? 0 : TableName.Length) 
                + (Waiter.IsNull() ? 0 : Waiter.Length)
                + (DivisionColorRGB.IsNull() ? 0 : DivisionColorRGB.Length)
                + szInt
                + (WaitingTimerString.IsNull() ? 0 : WaitingTimerString.Length);

            int szDishes = 0;
            foreach (OrderDishModel dish in this.Dishes.Values) szDishes += dish.GetInstanceSize();
            retVal += szDishes;

            return retVal;
        }

        // получить последнюю дату входа в состояние из блюд
        private DateTime getMaxDishEnterStateDate(OrderStatusEnum newStatus)
        {
            DateTime retVal = default(DateTime), dt;

            foreach (OrderDishModel modelDish in _dishesDict.Values)
            {
                dt = modelDish.EnterStatusDict[newStatus];  // словарь в блюде дат входов в состояния
                if (dt > retVal) retVal = dt;
            }

            return retVal;
        }

        #region run time funcs
        // метод, который возвращает значения полей даты/времени состояния
        private StatusDTS getStatusRunTimeDTS(OrderStatusEnum status)
        {
            StatusDTS retVal = new StatusDTS();
            if (_dbRunTimeRecord == null) return retVal;

            switch (status)
            {
                case OrderStatusEnum.None:
                    break;

                case OrderStatusEnum.WaitingCook:
                    retVal.DateEntered = Convert.ToDateTime(_dbRunTimeRecord.InitDate);
                    retVal.TimeStanding = Convert.ToInt32(_dbRunTimeRecord.WaitingCookTS);
                    break;

                case OrderStatusEnum.Cooking:
                    retVal.DateEntered = Convert.ToDateTime(_dbRunTimeRecord.CookingStartDate);
                    retVal.TimeStanding = Convert.ToInt32(_dbRunTimeRecord.CookingTS);
                    break;

                case OrderStatusEnum.Ready:
                    retVal.DateEntered = Convert.ToDateTime(_dbRunTimeRecord.ReadyDate);
                    if (_isUseReadyConfirmed)
                        retVal.TimeStanding = Convert.ToInt32(_dbRunTimeRecord.ReadyTS);
                    else
                        retVal.TimeStanding = Convert.ToInt32(_dbRunTimeRecord.WaitingTakeTS);
                    break;

                case OrderStatusEnum.ReadyConfirmed:
                    retVal.DateEntered = Convert.ToDateTime(_dbRunTimeRecord.ReadyConfirmedDate);
                    retVal.TimeStanding = Convert.ToInt32(_dbRunTimeRecord.WaitingTakeTS);
                    break;

                case OrderStatusEnum.Took:
                    retVal.DateEntered = Convert.ToDateTime(_dbRunTimeRecord.TakeDate);
                    retVal.TimeStanding = Convert.ToInt32(_dbRunTimeRecord.WaitingCommitTS);
                    break;

                case OrderStatusEnum.Cancelled:
                    retVal.DateEntered = Convert.ToDateTime(_dbRunTimeRecord.CancelDate);
                    break;

                case OrderStatusEnum.Commit:
                    retVal.DateEntered = Convert.ToDateTime(_dbRunTimeRecord.CommitDate);
                    break;

                case OrderStatusEnum.CancelConfirmed:
                    retVal.DateEntered = Convert.ToDateTime(_dbRunTimeRecord.CancelConfirmedDate);
                    break;

                default:
                    break;
            }
            return retVal;
        }

        // записать в поля RunTimeRecord дату входа и время нахождения (seconds) в состоянии status
        // метод, который устанавливает значения полей даты/времени состояния
        private void setStatusRunTimeDTS(OrderStatusEnum status, DateTime dateEntered, int timeStanding)
        {
            switch (status)
            {
                case OrderStatusEnum.None:
                    break;

                case OrderStatusEnum.WaitingCook:
                    if (dateEntered.IsZero() == false) _dbRunTimeRecord.InitDate = dateEntered;
                    if (timeStanding >= 0) _dbRunTimeRecord.WaitingCookTS = timeStanding;
                    break;

                case OrderStatusEnum.Cooking:
                    if (dateEntered.IsZero() == false)
                    {
                        _dbRunTimeRecord.CookingStartDate = dateEntered;
                        // если предыдущие DTS пустые, то заполнить начальными значениями
                        if (_dbRunTimeRecord.InitDate.IsZero()) setStatusRunTimeDTS(OrderStatusEnum.WaitingCook, dateEntered, 0);
                    }
                    if (timeStanding >= 0) _dbRunTimeRecord.CookingTS = timeStanding;
                    break;

                case OrderStatusEnum.Ready:
                    if (dateEntered.IsZero() == false)
                    {
                        _dbRunTimeRecord.ReadyDate = dateEntered;
                        // если предыдущие DTS пустые, то заполнить начальными значениями
                        if (_dbRunTimeRecord.InitDate.IsZero()) setStatusRunTimeDTS(OrderStatusEnum.WaitingCook, dateEntered, 0);
                        if (_dbRunTimeRecord.CookingStartDate.IsZero()) setStatusRunTimeDTS(OrderStatusEnum.Cooking, dateEntered, 0);
                    }
                    if (timeStanding >= 0)
                    {
                        if (_isUseReadyConfirmed)
                            _dbRunTimeRecord.ReadyTS = timeStanding;
                        else
                            _dbRunTimeRecord.WaitingTakeTS = timeStanding;
                    }
                    break;

                case OrderStatusEnum.ReadyConfirmed:
                    if (dateEntered.IsZero() == false)
                    {
                        _dbRunTimeRecord.ReadyConfirmedDate = dateEntered;
                        // если предыдущие DTS пустые, то заполнить начальными значениями
                        if (_dbRunTimeRecord.InitDate.IsZero()) setStatusRunTimeDTS(OrderStatusEnum.WaitingCook, dateEntered, 0);
                        if (_dbRunTimeRecord.CookingStartDate.IsZero()) setStatusRunTimeDTS(OrderStatusEnum.Cooking, dateEntered, 0);
                        if (_dbRunTimeRecord.ReadyDate.IsZero()) setStatusRunTimeDTS(OrderStatusEnum.Ready, dateEntered, 0);
                    }
                    if (timeStanding >= 0) _dbRunTimeRecord.WaitingTakeTS = timeStanding;
                    break;

                case OrderStatusEnum.Took:
                    if (dateEntered.IsZero() == false) _dbRunTimeRecord.TakeDate = dateEntered;
                    if (timeStanding >= 0) _dbRunTimeRecord.WaitingCommitTS = timeStanding;
                    break;

                case OrderStatusEnum.Cancelled:
                    if (dateEntered.IsZero() == false) _dbRunTimeRecord.CancelDate = dateEntered;
                    break;

                case OrderStatusEnum.Commit:
                    if (dateEntered.IsZero() == false) _dbRunTimeRecord.CommitDate = dateEntered;
                    break;

                case OrderStatusEnum.CancelConfirmed:
                    if (dateEntered.IsZero() == false) _dbRunTimeRecord.CancelConfirmedDate = dateEntered;
                    break;

                default:
                    break;
            }
        }
        #endregion

        #region db funcs
        private OrderRunTime getOrderRunTimeRecord(int orderId)
        {
            OrderRunTime runtimeRecord = null;
            runtimeRecord = getOrderRunTimeByOrderId(orderId);

            // если еще нет записи в БД, то добавить ее
            if (runtimeRecord == null)
            {
                runtimeRecord = newOrderRunTime(orderId);
                if (runtimeRecord == null)
                {
                    string _errMsg = string.Format("Ошибка создания записи в таблице OrderRunTime для заказа id {0}", orderId);
                    _serviceErrorMessage = _errMsg;
                    AppLib.WriteLogErrorMessage(_errMsg);
                    runtimeRecord = null;
                }
            }

            return runtimeRecord;
        }

        private OrderRunTime getOrderRunTimeById(int id)
        {
            string sqlText = string.Format("SELECT * FROM [OrderRunTime] WHERE ([Id] = {0})", id.ToString());
            return getOrderRunTime(sqlText);
        }

        private OrderRunTime getOrderRunTimeByOrderId(int orderId)
        {
            string sqlText = string.Format("SELECT * FROM [OrderRunTime] WHERE ([OrderId] = {0})", orderId.ToString());
            return getOrderRunTime(sqlText);
        }

        private OrderRunTime newOrderRunTime(int orderId)
        {
            string sqlText = $"INSERT INTO [OrderRunTime] (OrderId) VALUES ({orderId}); SELECT @@IDENTITY";

            int newId = 0; string dbError = null;
            using (DBContext db = new DBContext())
            {
                var result = db.ExecuteScalar(sqlText);
                if (result != null) newId = Convert.ToInt32(result);
                dbError = db.ErrMsg;
            }

            if ((newId == 0) || (dbError != null))
            {
                _serviceErrorMessage = dbError;
                return null;
            }

            // вернуть запись с данным Id
            OrderRunTime retVal = getOrderRunTimeById(newId);
            return retVal;
        }

        private OrderRunTime getOrderRunTime(string sqlText)
        {
            DataTable dt = null;
            using (DBContext db = new DBContext())
            {
                dt = db.GetQueryTable(sqlText);
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


        private bool saveRunTimeRecord()
        {
            string sLogMsg = " - updating sql-table OrderRunTime..";
            AppLib.WriteLogTraceMessage(sLogMsg);

            string sqlText = getSQLUpdStringRunTimeRecord(_dbRunTimeRecord);

            int result = 0; string dbError = null;
            using (DBContext db = new DBContext())
            {
                result = db.ExecuteCommand(sqlText);
                dbError = db.ErrMsg;
            }

            bool retVal = false;
            if (result == 1)
            {
                retVal = true;
                sLogMsg += " - Ok";
            }
            else
            {
                sLogMsg += " - error: " + dbError;
                _serviceErrorMessage = dbError;
            }
            AppLib.WriteLogTraceMessage(sLogMsg);

            return retVal;
        }

        private string getSQLUpdStringRunTimeRecord(OrderRunTime runTimeRecord)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
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

            return sqlText;
        }

        private bool saveStatusToDB(OrderStatusEnum status, string machineName = null)
        {
            string sLogMsg = string.Format("   - save ORDER {0}/{1}, status = {2}", this.Id, this.Number.ToString(), status.ToString());
            DateTime dtTmr = DateTime.Now;
            if (machineName == null) AppLib.WriteLogOrderDetails(sLogMsg + " - START");
            else AppLib.WriteLogClientAction(machineName, sLogMsg);

            // записать в поле QueueStatusId значение для очереди заказов
            string sqlSetQueueValueText = null;
            if (status == OrderStatusEnum.Cooking)
            {
                sqlSetQueueValueText = "[QueueStatusId] = 0";
            }
            else if ((!_isUseReadyConfirmed && (status == OrderStatusEnum.Ready))
                || (_isUseReadyConfirmed && (status == OrderStatusEnum.ReadyConfirmed)))
            {
                sqlSetQueueValueText = "[QueueStatusId] = 1";
            }
            else if (status == OrderStatusEnum.Took)
            {
                sqlSetQueueValueText = "[QueueStatusId] = 2";
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendFormat("UPDATE [Order] SET [OrderStatusId] = {0}", ((int)status).ToString());
            if ((sqlSetQueueValueText.IsNull() == false) && (sqlSetQueueValueText.Length > 0))
                sb.Append(", " + sqlSetQueueValueText);
            sb.AppendFormat(" WHERE ([Id] = {0})", this.Id.ToString());
            string sqlText = sb.ToString();
            sb = null;

            int result = 0; string dbError = null;
            using (DBContext db = new DBContext())
            {
                result = db.ExecuteCommand(sqlText);
                dbError = db.ErrMsg;
            }
            sLogMsg += " - FINISH - " + (DateTime.Now - dtTmr).ToString();

            bool retVal = false;
            if (result == 1)
            {
                retVal = true;
                sLogMsg += " - Ok";
            }
            else
            {
                _serviceErrorMessage = string.Format("Ошибка записи в БД: {0}", dbError);
                sLogMsg += " - error: " + dbError;
            }

            if (machineName == null) AppLib.WriteLogOrderDetails(sLogMsg);
            else AppLib.WriteLogClientAction(machineName, sLogMsg);

            return retVal;
        }

        private void writeDBException(Exception ex, string subMsg1)
        {
            _serviceErrorMessage = string.Format("Ошибка {0} записи в БД", subMsg1);
            AppLib.WriteLogErrorMessage("   - DB Error ORDER id {0}: {1}", this.Id, ex.ToString());
        }

        #endregion

        public void Dispose()
        {
            AppLib.WriteLogTraceMessage("   dispose class OrderModel id {0}", this.Id);

            // сохраняем в записи RunTimeRecord время нахождения в текущем состоянии
            if ((_curTimer != null) && (_curTimer.Enabled))
            {
                _curTimer.Stop();
                if (_dbRunTimeRecord != null)
                {
                    setStatusRunTimeDTS(this.Status, DateTime.MinValue, _curTimer.ValueTS);
                    saveRunTimeRecord();
                }
            }

            // уничтожить таймеры
            if (_tsTimersDict != null) _tsTimersDict.Clear();

            // задиспозить блюда
            if (_dishesDict != null)
            {
                foreach (OrderDishModel modelDish in _dishesDict.Values) modelDish.Dispose();
                _dishesDict.Clear();
            }
        }

    }  // class OrderModel
}
