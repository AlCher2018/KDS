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

namespace KDSService.AppModel
{
    /// <summary>
    /// *** Service class OrderDishModel 
    /// </summary>
    // Изменение полей:
    // 1. Из БД, все поля - в конструкторе и по запросу из блюда на обновление из БД
    // 2. по внутреннему счетчику нахождения в состоянии
    // Чтение полей:
    // 1. Для внешнего клиента нужена строка счетчика нахождения в состоянии
    [DataContract]
    public class OrderDishModel: IDisposable
    {
        #region Service contract Properties
        [DataMember]
        public int Id { get; set; }

        [DataMember]
        public string Uid { get; set; }

        [DataMember]
        public DateTime CreateDate { get; set; }

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        // номер подачи
        public int FilingNumber { get; set; }

        [DataMember]
        public decimal Quantity { get; set; }

        // если есть, то это ингредиент к блюду ParentUid
        [DataMember]
        public string ParentUid { get; set; }

        [DataMember]
        public string Comment { get; set; }

        [DataMember]
        public int EstimatedTime { get; set; }

        // время (в сек) "Готовить позже"
        [DataMember]
        public int DelayedStartTime { get; set; }

        [DataMember]
        public int DishStatusId { get; set; }

        private OrderStatusEnum Status { get; set; }

        [DataMember]
        public DepartmentModel Department
        {
            get { return _department; }
            set { }
        }

        [DataMember]
        public string ServiceErrorMessage { get { return _serviceErrorMessage; } set { } }

        // форматированное представление временного промежутка для внешних клиентов
        [DataMember]
        public string WaitingTimerString
        {
            get
            {
                string retVal = null;
                if (_curTimer != null)
                {
                    // время из текущего таймера
                    TimeSpan tsTimerValue = TimeSpan.FromSeconds(_curTimer.ValueTS);

                    // состояние "Ожидание готовки"
                    if (Status == OrderStatusEnum.WaitingCook)
                    {
                        // если есть "Готовить через" - отображаем время начала автомат.перехода в сост."В процессе" по убыванию
                        if (DelayedStartTime != 0)
                            tsTimerValue = TimeSpan.FromSeconds(Convert.ToInt32((_dtCookingStartEstimated - DateTime.Now).TotalSeconds));
                        // иначе, если есть время приготовления, то отобразить время приготовления
                        else if (EstimatedTime != 0)
                            tsTimerValue = _tsCookingEstimated;
                        else
                            tsTimerValue = TimeSpan.Zero;
                    }
                    
                    // состояние "В процессе" и есть время приготовления - отображаем время приготовления по убыванию
                    else if ((Status == OrderStatusEnum.Cooking) && (EstimatedTime != 0))
                    {
                        tsTimerValue = _tsCookingEstimated - tsTimerValue;
                    }

                    // преобразование времени в строку
                    if (tsTimerValue == TimeSpan.Zero)
                    {
                        retVal = "";
                    }
                    else
                    {
                        retVal = (tsTimerValue.Days > 0d) ? tsTimerValue.ToString(@"d\.hh\:mm\:ss") : tsTimerValue.ToString(@"hh\:mm\:ss");
                        // отрицательное время
                        if (tsTimerValue.Ticks < 0) retVal = "-" + retVal;
                    }
                }

                return retVal;
            }
            set { }  // необходимо для DataMember
        }

        #endregion


        // словарь дат входа в состояние
        private Dictionary<OrderStatusEnum, DateTime> _dtEnterStatusDict;
        // нужен заказу, чтобы определять мин/макс дату по любому состоянию
        public Dictionary<OrderStatusEnum, DateTime> EnterStatusDict { get { return _dtEnterStatusDict; } }

        #region Fields
        private DepartmentModel _department;

        // поля дат состояний и временных промежутков
        private DateTime _dtCookingStartEstimated;   // ожидаемое начало приготовления
        private TimeSpan _tsCookingEstimated;   // время приготовления
        // накопительные счетчики нахождения в конкретном состоянии
        private Dictionary<OrderStatusEnum, IncrementalTimer> _tsTimersDict; // словарь накопительных счетчиков для различных состояний
        private IncrementalTimer _curTimer;  // текущий таймер для выдачи клиенту значения таймера

        // записи БД для сохранения блюда
        private OrderDishRunTime _dbRunTimeRecord = null;         // запись дат/времени прямого пути 
        private string _serviceErrorMessage;

        // ссылка на родительский заказ для обратных вызовов
        private OrderModel _modelOrder;
        #endregion


        // ctor
        // ДЛЯ НОВОГО БЛЮДА
        public OrderDishModel(OrderDish dbDish, OrderModel modelOrder)
        {
            _modelOrder = modelOrder;

            Id = dbDish.Id; Uid = dbDish.UID;
            CreateDate = dbDish.CreateDate;
            Name = dbDish.DishName;
            FilingNumber = dbDish.FilingNumber;
            ParentUid = dbDish.ParentUid;
            Comment = dbDish.Comment;
            Quantity = dbDish.Quantity;
            DelayedStartTime = dbDish.DelayedStartTime;
            EstimatedTime = dbDish.EstimatedTime;
            DishStatusId = dbDish.DishStatusId??0;
            Status = AppLib.GetStatusEnumFromNullableInt(dbDish.DishStatusId);

            // объект отдела взять из справочника
            _department = ModelDicts.GetDepartmentById(dbDish.DepartmentId);

            // ожидаемое время начала приготовления для автоматического перехода в состояние приготовления
            _dtCookingStartEstimated = CreateDate.AddSeconds(DelayedStartTime);
            //_dtCookingStartEstimated = DateTime.Now.AddSeconds(DelayedStartTime); // for debug

            // время приготовления
            _tsCookingEstimated = (EstimatedTime == 0) ? TimeSpan.Zero : TimeSpan.FromSeconds(EstimatedTime);

            // словарь дат входа в состояние
            _dtEnterStatusDict = new Dictionary<OrderStatusEnum, DateTime>();
            foreach (var item in Enum.GetValues(typeof(OrderStatusEnum))) _dtEnterStatusDict.Add((OrderStatusEnum)item, DateTime.MinValue);

            // создать словарь накопительных счетчиков
            _tsTimersDict = new Dictionary<OrderStatusEnum, IncrementalTimer>();
            // таймер ожидания начала приготовления
            _tsTimersDict.Add(OrderStatusEnum.WaitingCook, new IncrementalTimer(1000));
            // таймер времени приготовления
            _tsTimersDict.Add(OrderStatusEnum.Cooking, new IncrementalTimer(1000));
            // таймер времени ожидания выдачи, нахождение в состоянии Готов
            _tsTimersDict.Add(OrderStatusEnum.Ready, new IncrementalTimer(1000));
            // таймер времени ожидания фиксации заказа, нахождение в состоянии Выдано
            _tsTimersDict.Add(OrderStatusEnum.Took, new IncrementalTimer(1000));

            // получить запись из таблицы состояний
            _dbRunTimeRecord = getDBRunTimeRecord(dbDish.Id);

            // состояние в методе может поменяться
            UpdateFromDBEntity(dbDish);

            // и таймер состояния уже может быть запущен
            // если пустой, то стартуем
            if ((_curTimer == null) && (_tsTimersDict.ContainsKey(this.Status)))
            {
                _curTimer = _tsTimersDict[this.Status];

                // для сохраненного статуса берем дату и время в секундах из БД
                StatusDTS statusDTS = getStatusRunTimeDTS(this.Status);
                // для текущего статуса НЕТ сохраненной даты входа - стартуем с текущей даты (или с даты на TimeStanding секунд раньше) и сохраняем ее как дату входа в состояние
                if (statusDTS.DateEntered.IsZero())
                {
                    int tsSeconds = statusDTS.TimeStanding;
                    DateTime initDT = (tsSeconds == 0) ? DateTime.Now : DateTime.Now.AddSeconds(-tsSeconds);

                    _curTimer.InitDateTimeValue(initDT);

                    setStatusRunTimeDTS(this.Status, initDT, -1);
                    saveRunTimeRecord();
                }
                else
                {
                    _curTimer.InitDateTimeValue(statusDTS.DateEntered);
                }
                _curTimer.Start();    // стартуем таймер состояния
            }

        }  // constructor

        // обновить из БД
        internal void UpdateFromDBEntity(OrderDish dbDish)
        {
            lock (this)
            {
                // и для блюда, и для ингредиента
                if (Uid.IsNull() || (Uid != dbDish.UID)) Uid = dbDish.UID;
                if (CreateDate != dbDish.CreateDate) CreateDate = dbDish.CreateDate;
                if (Name.IsNull() || (Name != dbDish.DishName)) Name = dbDish.DishName;
                if (FilingNumber != dbDish.FilingNumber) FilingNumber = dbDish.FilingNumber;
                if (ParentUid.IsNull() || (ParentUid != dbDish.ParentUid)) ParentUid = dbDish.ParentUid;
                if (Comment.IsNull() || (Comment != dbDish.Comment)) Comment = dbDish.Comment;
                if (Quantity != dbDish.Quantity) Quantity = dbDish.Quantity;
                if (DelayedStartTime != dbDish.DelayedStartTime) DelayedStartTime = dbDish.DelayedStartTime;
                if (EstimatedTime != dbDish.EstimatedTime) EstimatedTime = dbDish.EstimatedTime;

                OrderStatusEnum statusFromDBObj = AppLib.GetStatusEnumFromNullableInt(dbDish.DishStatusId);

                // обновление состояния ТОЛЬКО ДЛЯ БЛЮДА !!!
                // т.к. состояние ингредиентов изменяется в самом блюде
                if (ParentUid.IsNull())
                {
                    // проверяем условие автоматического перехода в режим приготовления
                    if ((statusFromDBObj <= OrderStatusEnum.WaitingCook) && canAutoPassToCookingStatus()) statusFromDBObj = OrderStatusEnum.Cooking;

                    bool isNewStatus = (statusFromDBObj != Status);     // статус изменен

                    // если поменялся отдел
                    if (_department.Id != dbDish.DepartmentId)
                    {
                        // необходимо обновить статус
                        isNewStatus = true;
                        // объект отдела взять из справочника
                        _department = ModelDicts.GetDepartmentById(dbDish.DepartmentId);
                    }

                    if (isNewStatus) UpdateStatus(statusFromDBObj, false);

                }  // только для БЛЮДА
                
                // проверить состояние для ингредиента
                else
                {
                    // найти уже существующее блюдо для данного ингредиента
                    OrderDishModel baseDish = _modelOrder.Dishes.Values.FirstOrDefault(d => d.ParentUid.IsNull() && (d.Uid == this.Uid));
                    // для ингредиента состояние берем из родительского блюда
                    if ((baseDish != null) && (this.DishStatusId != baseDish.DishStatusId)) UpdateStatus(baseDish.Status, false);
                }

            }  // lock

        }  // method


        // ******************************************
        //    ОСНОВНАЯ ПРОЦЕДУРА БИЗНЕС-ЛОГИКИ
        //    ПРИ ИЗМЕНЕНИИ СТАТУСА БЛЮДА.
        //   Блюдо. Самостоятельное или ингредиент (подчиненное блюдо)
        // ******************************************
        // команды на изменение статуса блюда могут приходить как от КДС, так и из FrontOffice (при чтении из БД)
        // состояния и даты сохраняются в БД при каждом изменении
        public bool UpdateStatus(OrderStatusEnum newStatus, bool isUpdateParentOrder)
        {
            if (this.Status == newStatus) return false; // если статус не поменялся, то ничего не делать

            bool isUpdSuccess = false;
            // здесь тоже лочить, т.к. вызовы могут быть как циклческие (ингр.для блюд), так и из заказа / КДС-а
            lock (this)
            {
                // дата входа в НОВОЕ состояние
                DateTime dtEnterToNewStatus = DateTime.Now;
                // время нахождения в ПРЕДЫДУЩЕМ состоянии, в секундах
                int secondsInPrevState = 0;
                if (_curTimer != null)   // если есть таймер предыдущего состояния
                {
                    _curTimer.Stop(); // остановить таймер состояния
                    // получить время нахождения в состоянии с момента последнего входа
                    secondsInPrevState = _curTimer.ValueTS;
                   // Debug.Print("secondsInPrevState {0}", secondsInPrevState);
                }

                // сохранить новый статус в БД
                if (saveStatusToDB(newStatus))
                {
                    // **** запись или в RunTimeRecord или в ReturnTable
                    // сохранить дату входа в состояние во внутреннем словаре
                    _dtEnterStatusDict[newStatus] = dtEnterToNewStatus;
                    // и в БД
                    if (_dbRunTimeRecord != null)
                    {
                        // пустое ли поле в RunTimeRecord для даты входа текущего статуса?
                        if (getStatusRunTimeDTS(newStatus).DateEntered.IsZero())  // возв. true, если поле == null
                        {
                            // сохраняем дату входа в новое состояние
                            setStatusRunTimeDTS(newStatus, dtEnterToNewStatus, -1);
                            // сохраняем в записи RunTimeRecord время нахождения в предыдущем состоянии
                            setStatusRunTimeDTS(this.Status, DateTime.MinValue, secondsInPrevState);

                            //Debug.Print("status from {0} ts {1}; status to {2}, dt {3}", this.Status, secondsInPrevState, newStatus, dtEnterToNewStatus);
                            saveRunTimeRecord();
                        }
                        // создать новую запись в Return table
                        else
                        {
                            saveReturnTimeRecord(this.Status, newStatus, dtEnterToNewStatus, secondsInPrevState);
                        }
                    }
                    // ****

                    // запуск таймера для нового состояния, чтобы клиент мог получить значение таймера
                    _curTimer = null;
                    if (_tsTimersDict.ContainsKey(newStatus))
                    {
                        _curTimer = _tsTimersDict[newStatus];
                        _curTimer.Start();
                    }

                    // сохранить новый статус в объекте
                    Status = newStatus;
                    DishStatusId = (int)newStatus;

                    // если это блюдо
                    if (this.ParentUid.IsNull())
                    {
                        // то найти для него ингредиенты ДАЛЬШЕ ПО СПИСКУ блюд, 
                        // чтобы не захватить ингредиенты у такого же блюда, перед или после данного в списке блюд!!!
                        List<OrderDishModel> dishes = this._modelOrder.Dishes.Values.ToList();
                        OrderDishModel probIngr;
                        int idx = dishes.IndexOf(this);  // индекс блюда
                        // поиск последующих ингредиентов
                        for (int i = idx+1; i < dishes.Count; i++)
                        {
                            probIngr = dishes[i];
                            // поменять статус и запустить таймеры для ингредиентов данного блюда
                            if ((probIngr.Uid == this.Uid) && (probIngr.ParentUid == this.Uid)) probIngr.UpdateStatus(this.Status, false);
                            // ингр.кончились - выйти из цикла
                            else break;
                        }
                    }

                    // попытка обновить статус Заказа проверкой состояний всех блюд/ингредиентов
                    if (isUpdateParentOrder) _modelOrder.UpdateStatusByVerificationDishes();

                    isUpdSuccess = true;
                }
            }

            return isUpdSuccess;
        }  // method UpdateStatus


        #region DB FUNCS
        private OrderDishRunTime getDBRunTimeRecord(int id)
        {
            OrderDishRunTime runtimeRecord = null;
            using (KDSEntities db = new KDSEntities())
            {
                runtimeRecord = db.OrderDishRunTime.FirstOrDefault(rec => rec.OrderDishId == id);
                if (runtimeRecord == null)
                {
                    runtimeRecord = new OrderDishRunTime() { OrderDishId = id };
                    db.OrderDishRunTime.Add(runtimeRecord);
                    try
                    {
                        db.SaveChanges();
                    }
                    catch (Exception ex)
                    {
                        _serviceErrorMessage = string.Format("Ошибка создания записи в таблице OrderDishRunTime для блюда id {0}", id);
                        AppEnv.WriteLogErrorMessage("Ошибка создания записи в таблице OrderDishRunTime для блюда id {0}: {1}", id, ex.ToString());
                        runtimeRecord = null;
                    }
                }
            }
            return runtimeRecord;
        }  // method


        // метод, который возвращает значения полей даты/времени состояния
        private StatusDTS getStatusRunTimeDTS(OrderStatusEnum status)
        {
            StatusDTS retVal = new StatusDTS();
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
                        if (_dbRunTimeRecord.InitDate == null) setStatusRunTimeDTS(OrderStatusEnum.WaitingCook, dateEntered, 0);
                    }
                    if (timeStanding >= 0) _dbRunTimeRecord.CookingTS = timeStanding;
                    break;

                case OrderStatusEnum.Ready:
                    if (dateEntered.IsZero() == false)
                    {
                        _dbRunTimeRecord.ReadyDate = dateEntered;
                        // если предыдущие DTS пустые, то заполнить начальными значениями
                        if (_dbRunTimeRecord.InitDate == null) setStatusRunTimeDTS(OrderStatusEnum.WaitingCook, dateEntered, 0);
                        if (_dbRunTimeRecord.CookingStartDate == null) setStatusRunTimeDTS(OrderStatusEnum.Cooking, dateEntered, 0);
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


        private void saveRunTimeRecord()
        {
            // приаттачить и сохранить в DB-контексте два поля из RunTimeRecord
            using (KDSEntities db = new KDSEntities())
            {
                try
                {
                    db.OrderDishRunTime.Attach(_dbRunTimeRecord);
                    // указать, что запись изменилась
                    db.Entry<OrderDishRunTime>(_dbRunTimeRecord).State = System.Data.Entity.EntityState.Modified;
                    db.SaveChanges();
                }
                catch (Exception ex)
                {
                    writeDBException(ex, "обновления");
                }
            }
        }

        private void saveReturnTimeRecord(OrderStatusEnum statusFrom, OrderStatusEnum statusTo, DateTime dtEnterToNewStatus, int secondsInPrevState)
        {
            using (KDSEntities db = new KDSEntities())
            {
                db.OrderDishReturnTime.Add(new OrderDishReturnTime()
                {
                    OrderDishId = this.Id,
                    ReturnDate = dtEnterToNewStatus,
                    StatusFrom = (int)statusFrom,
                    StatusFromTimeSpan = secondsInPrevState,
                    StatusTo = (int)statusTo
                });
                try
                {
                    db.SaveChanges();
                }
                catch (Exception ex)
                {
                    writeDBException(ex, "добавления");
                }
            }
        }

        private bool saveStatusToDB(OrderStatusEnum status)
        {
            bool retVal = false;
            using (KDSEntities db = new KDSEntities())
            {
                try
                {
                    OrderDish dbDish = db.OrderDish.Find(this.Id);
                    if (dbDish != null)
                    {
                        dbDish.DishStatusId = (int)status;
                        db.SaveChanges();
                        retVal = true;
                    }
                }
                catch (Exception ex)
                {
                    writeDBException(ex, "сохранения");
                }
            }
            return retVal;
        }

        private void writeDBException(Exception ex, string subMsg1)
        {
            _serviceErrorMessage = string.Format("Ошибка {0} записи в БД", subMsg1);
            AppEnv.WriteLogErrorMessage("DB Error (dish id {0}): {1}, Source: {2}", this.Id, ex.Message, ex.Source);
        }

        #endregion

        // проверка возможности АВТОМАТИЧЕСКОГО перехода в состояние Cooking
        private bool canAutoPassToCookingStatus()
        {
            // 1. для отдела установлен автоматический старт приготовления и текущая дата больше даты ожидаемого времени начала приготовления
            bool retVal = (this.Department.IsAutoStart && (DateTime.Now >= _dtCookingStartEstimated));

            // 2. проверяем общее кол-во такого блюда в заказах, если установлено кол-во одновременно готовящихся блюд
            if (retVal == true)
            {
                Dictionary<int, decimal> dishesQtyDict = (Dictionary<int, decimal>)AppEnv.GetAppProperty("dishesQty");
                if ((dishesQtyDict != null) && (dishesQtyDict.ContainsKey(this.Id)))
                    retVal = (dishesQtyDict[this.Id] < _department.DishQuantity);
                else
                    retVal = false;
            }

            return retVal;
        }

        public void Dispose()
        {
            AppEnv.WriteLogTraceMessage("    dispose class OrderDishModel id {0}", this.Id);

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
            if (_tsTimersDict != null)
            {
                foreach (var statTimer in _tsTimersDict.Values) statTimer.Dispose();
                _tsTimersDict.Clear();
            }
        }  // dispose

    }  // class OrderDishModel
}
