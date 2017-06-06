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
using System.ServiceModel;

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
                string retVal = "***";
                if ((_curTimer != null) && _curTimer.Enabled)
                {
                    // время из текущего таймера
                    TimeSpan tsTimerValue = TimeSpan.Zero;
                    try
                    {
                        tsTimerValue = TimeSpan.FromSeconds(_curTimer.ValueTS);
                    }
                    catch (Exception ex)
                    {
                        throw new FaultException("Ошибка получения периода времени от таймера: " + ex.Message, new FaultCode("TimeCounter class"));
                    }

                    // для блюда сохраним значение текущего таймера
                    if (_parentDish == null)
                    {
                        // состояние "Ожидание готовки" - на клиенте, т.к. в этот состоянии все равно таймер не используется

                        // состояние "В процессе" и есть время приготовления - отображаем время приготовления по убыванию
                        if (Status == OrderStatusEnum.Cooking)
                        {
                            tsTimerValue = _tsCookingEstimated - tsTimerValue;
                        }

                        // состояние "ГОТОВО": проверить период ExpectedTake, в течение которого официант должен забрать блюдо
                        else if (Status == OrderStatusEnum.Ready)
                        {
                            int expTake = (int)AppEnv.GetAppProperty("ExpectedTake");
                            if (expTake > 0)
                            {
                                tsTimerValue = TimeSpan.FromSeconds(expTake) - tsTimerValue;
                            }
                        }

                        _timerValue = tsTimerValue;
                    }
                    // для зависимого ингредиента взять значение из родительского блюда
                    else
                    {
                        if (Status == OrderStatusEnum.WaitingCook)
                            tsTimerValue = TimeSpan.Zero;
                        else
                            tsTimerValue = _parentDish._timerValue;
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
        private TimeSpan _tsCookingEstimated;   // время приготовления

        // накопительные счетчики нахождения в конкретном состоянии
        private Dictionary<OrderStatusEnum, TimeCounter> _tsTimersDict; // словарь накопительных счетчиков для различных состояний
        private TimeCounter _curTimer;  // текущий таймер для выдачи клиенту значения таймера

        // записи БД для сохранения блюда
        private OrderDishRunTime _dbRunTimeRecord = null;         // запись дат/времени прямого пути 
        private string _serviceErrorMessage;

        // ссылка на родительский заказ для обратных вызовов
        private OrderModel _modelOrder;

        private bool _isDish;
        private bool _isInrgIndepend;
        private OrderDishModel _parentDish;
        private TimeSpan _timerValue;

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

            _isDish = ParentUid.IsNull();
            _isInrgIndepend = (bool)AppEnv.GetAppProperty("IsIngredientsIndependent");
            _parentDish = null;

            // объект отдела взять из справочника
            _department = ModelDicts.GetDepartmentById(dbDish.DepartmentId);
            _tsCookingEstimated = TimeSpan.FromSeconds(this.EstimatedTime);

            // словарь дат входа в состояние
            _dtEnterStatusDict = new Dictionary<OrderStatusEnum, DateTime>();
            foreach (var item in Enum.GetValues(typeof(OrderStatusEnum))) _dtEnterStatusDict.Add((OrderStatusEnum)item, DateTime.MinValue);

            // создать словарь накопительных счетчиков
            _tsTimersDict = new Dictionary<OrderStatusEnum, TimeCounter>();
            // таймер ожидания начала приготовления
            _tsTimersDict.Add(OrderStatusEnum.WaitingCook, new TimeCounter() { Name= OrderStatusEnum.WaitingCook.ToString() });
            // таймер времени приготовления
            _tsTimersDict.Add(OrderStatusEnum.Cooking, new TimeCounter() { Name= OrderStatusEnum.Cooking.ToString() });
            // таймер времени ожидания выдачи, нахождение в состоянии Готов
            _tsTimersDict.Add(OrderStatusEnum.Ready, new TimeCounter() { Name = OrderStatusEnum.Ready.ToString() });
            // таймер времени ожидания фиксации заказа, нахождение в состоянии Выдано
            _tsTimersDict.Add(OrderStatusEnum.Took, new TimeCounter() { Name= OrderStatusEnum.Took.ToString()});

            // получить запись из таблицы состояний
            _dbRunTimeRecord = getDBRunTimeRecord(dbDish.Id);

            // стартануть таймер для блюда или независимого ингредиента
            if (_isDish || (!_isDish && _isInrgIndepend))
            {
                UpdateFromDBEntity(dbDish);  // для новой записи DTS не сохранен

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
            }

            // а для ЗАВИСИМОГО ингредиента - по родительскому блюду
            else
            {
                // найти уже существующее блюдо для данного ингредиента от текущей позиции и выше
                List<OrderDishModel> dishes = this._modelOrder.Dishes.Values.ToList();
                int idx = dishes.Count;  // индекс ингредиента
                for (int i = idx - 1; i >= 0; i--)
                {
                    // нашли родительское блюдо
                    if ((dishes[i].ParentUid.IsNull()) && (dishes[i].Uid == this.Uid))
                    {
                        _parentDish = dishes[i]; break;
                    }
                }
                updateIngredientByParentDish();
            }

        }  // constructor


        // обновить из БД
        internal void UpdateFromDBEntity(OrderDish dbDish)
        {
            lock (this)
            {
                // и для блюда, и для ингредиента
                if (Uid != dbDish.UID) Uid = dbDish.UID;
                if (CreateDate != dbDish.CreateDate) CreateDate = dbDish.CreateDate;
                if (Name != dbDish.DishName) Name = dbDish.DishName;
                if (FilingNumber != dbDish.FilingNumber) FilingNumber = dbDish.FilingNumber;
                if (ParentUid != dbDish.ParentUid) ParentUid = dbDish.ParentUid;
                if (Comment != dbDish.Comment) Comment = dbDish.Comment;
                if (Quantity != dbDish.Quantity) Quantity = dbDish.Quantity;

                // ожидаемое время начала приготовления для автоматического перехода в состояние приготовления
                if (DelayedStartTime != dbDish.DelayedStartTime) DelayedStartTime = dbDish.DelayedStartTime;
                // время приготовления
                if (EstimatedTime != dbDish.EstimatedTime)
                {
                    EstimatedTime = dbDish.EstimatedTime;
                    _tsCookingEstimated = TimeSpan.FromSeconds(EstimatedTime);
                }

                OrderStatusEnum newStatus = AppLib.GetStatusEnumFromNullableInt(dbDish.DishStatusId);

                // обновление состояния для блюда или независимого ингредиента
                if (_isDish || (!_isDish && _isInrgIndepend))
                {
                    // проверяем условие автоматического перехода в режим приготовления
                    if ((newStatus <= OrderStatusEnum.WaitingCook) && canAutoPassToCookingStatus())
                        newStatus = OrderStatusEnum.Cooking;

                    // если поменялся отдел
                    if (_department.Id != dbDish.DepartmentId)
                    {
                        // объект отдела взять из справочника
                        _department = ModelDicts.GetDepartmentById(dbDish.DepartmentId);
                    }

                    UpdateStatus(newStatus, false);
                }  // для БЛЮДА

            }  // lock

        }  // method


        // ******************************************
        //    ОСНОВНАЯ ПРОЦЕДУРА БИЗНЕС-ЛОГИКИ
        //    ПРИ ИЗМЕНЕНИИ СТАТУСА БЛЮДА или НЕЗАВИСИМОГО ИНГРЕДИЕНТА
        // ******************************************
        // команды на изменение статуса блюда могут приходить как от КДС, так и из FrontOffice (при чтении из БД)
        // состояния и даты сохраняются в БД при каждом изменении
        public bool UpdateStatus(OrderStatusEnum newStatus, bool isUpdateParentOrder,  
            DateTime dtEnterState = default(DateTime), int preStateTS = 0)
        {
            // если статус не поменялся для существующей записи, то ничего не делать
            if (this.Status == newStatus) return false; 

            bool isUpdSuccess = false;
            // здесь тоже лочить, т.к. вызовы могут быть как циклческие (ингр.для блюд), так и из заказа / КДС-а
            lock (this)
            {
                // время нахождения в ПРЕДЫДУЩЕМ состоянии, в секундах
                int secondsInPrevState = 0;
                if (_curTimer != null)   // если есть таймер предыдущего состояния
                {
                    _curTimer.Stop(); // остановить таймер состояния
                    // получить время нахождения в состоянии с момента последнего входа
                    secondsInPrevState = _curTimer.IncrementTS;
                    // Debug.Print("secondsInPrevState {0}", secondsInPrevState);
                }
                if (preStateTS != 0) secondsInPrevState = preStateTS;

                // дата входа в новое состояние: или то, что передано, или текущую
                DateTime dtEnterToNewStatus = DateTime.Now;
                // если переданы данные из родительского объекта (заказ или блюдо для ингредиентов)
                if (!dtEnterState.IsZero()) dtEnterToNewStatus = dtEnterState;

                // сохранить новый статус ОБЪЕКТА в БД
                if (saveStatusToDB(newStatus))
                {
                    // изменить статус в ОБЪЕКТЕ
                    OrderStatusEnum preStatus = this.Status;
                    this.Status = newStatus;
                    this.DishStatusId = (int)newStatus;

                    // **** запись или в RunTimeRecord или в ReturnTable
                    StatusDTS statusDTS = getStatusRunTimeDTS(this.Status);
                    if (statusDTS.DateEntered.IsZero())
                    {
                        // сохраняем дату входа в новое состояние
                        setStatusRunTimeDTS(this.Status, dtEnterToNewStatus, -1);
                        // сохраняем в записи RunTimeRecord время нахождения в предыдущем состоянии
                        setStatusRunTimeDTS(preStatus, DateTime.MinValue, secondsInPrevState);

                        saveRunTimeRecord();
                    }
                    // возврат в предыдущие состояния, создать новую запись в Return table
                    else
                    {
                        saveReturnTimeRecord(preStatus, newStatus, dtEnterToNewStatus, secondsInPrevState);
                        // при возврате из Ready в Cooking обнулять в RunTime-record дату входа в состояние Ready
                        // чтобы при следующем входе в Ready таймер ожидания выноса начал считаться с периода ExpectedTake
                        if ((preStatus == OrderStatusEnum.Ready) && (newStatus == OrderStatusEnum.Cooking))
                        {
                            _dbRunTimeRecord.ReadyDate = DateTime.MinValue;
                        }
                    }

                    // запуск таймера для нового состояния
                    statusDTS = getStatusRunTimeDTS(this.Status);
                    startStatusTimer(statusDTS);

                    // если это блюдо

                    if (_isDish)
                    {
                        if (!_isInrgIndepend) updateChildIngredients(dtEnterToNewStatus, secondsInPrevState);
                    }

                    // попытка обновить статус Заказа проверкой состояний всех блюд/ингредиентов
                    if (isUpdateParentOrder) _modelOrder.UpdateStatusByVerificationDishes();

                    isUpdSuccess = true;
                }
            }

            return isUpdSuccess;
        }  // method UpdateStatus


        // запуск таймера для текущего состояния
        private void startStatusTimer(StatusDTS statusDTS)
        {
            DateTime dtEnterToStatus = statusDTS.DateEntered;
            // сохранить дату входа в состояние во внутреннем словаре
            _dtEnterStatusDict[this.Status] = dtEnterToStatus;

            if (_tsTimersDict.ContainsKey(this.Status))
            {
                _curTimer = _tsTimersDict[this.Status];
                _curTimer.Start(dtEnterToStatus);
            }
        }


        #region обновление ЗАВИСИМЫХ ингредиентов
        // обновить добавленный к списку зависимый ингредиент по родительскому блюду
        // т.е. для нового объекта в заказе
        // возвращает признак того, что состояние ингр.было обновлено из DTS был сохранен в БД
        private void updateIngredientByParentDish()
        {
            // для ингредиента состояние берем из родительского блюда
            if (_parentDish != null) 
            {
                StatusDTS dtsBase = _parentDish.getStatusRunTimeDTS(_parentDish.Status);
                // и если оно поменялось, то обновляем статус ингредиента
                if (this.DishStatusId != _parentDish.DishStatusId)
                {
                    StatusDTS preDtsBase = _parentDish.getStatusRunTimeDTS(this.Status);
                    UpdateStatus(_parentDish.Status, false, dtsBase.DateEntered, preDtsBase.TimeStanding);
                }
                else
                {
                    setStatusRunTimeDTS(this.Status, dtsBase.DateEntered, -1);
                    saveRunTimeRecord();

                    startStatusTimer(dtsBase);
                }
            }
        }

        // обновить уже существующие ингредиенты, зависимые от текущего блюда, находящиеся ДАЛЬШЕ ПО СПИСКУ блюд, 
        // чтобы не захватить ингредиенты у такого же блюда, перед или после данного в списке блюд!!!
        private void updateChildIngredients(DateTime dtEnterToNewStatus, int secondsInPrevState)
        {
            List<OrderDishModel> dishes = this._modelOrder.Dishes.Values.ToList();
            OrderDishModel probIngr;
            int idx = dishes.IndexOf(this);  // индекс текущего блюда
                                             // поиск последующих ингредиентов
            for (int i = idx + 1; i < dishes.Count; i++)
            {
                probIngr = dishes[i];
                // поменять статус и запустить таймеры для ингредиентов данного блюда
                if ((probIngr.Uid == this.Uid) && (probIngr.ParentUid == this.Uid))
                {
                    probIngr.UpdateStatus(this.Status, false, dtEnterToNewStatus, secondsInPrevState);
                }
                // ингр.кончились - выйти из цикла
                else break;
            }
        }

        #endregion

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
                    // для данного состояния накапливаем время нахождения в нем
                    if (timeStanding >= 0)
                    {
                        _dbRunTimeRecord.CookingTS = (_dbRunTimeRecord.CookingTS ?? 0) + timeStanding;
                    }

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
            bool retVal = (this.Department.IsAutoStart 
                && (DateTime.Now >= this.CreateDate.AddSeconds(this.DelayedStartTime)));

            // 2. проверяем общее кол-во такого блюда в заказах, если установлено кол-во одновременно готовящихся блюд
            if (retVal == true)
            {
                Dictionary<string, decimal> dishesQtyDict = (Dictionary<string, decimal>)AppEnv.GetAppProperty("dishesQty");
                if ((dishesQtyDict != null) && (dishesQtyDict.ContainsKey(this.Uid)))
                {
                    retVal = ((dishesQtyDict[this.Uid] + this.Quantity) <= _department.DishQuantity);
                    // обновить кол-во в словаре, пока он не обновился из БД
                    if (retVal) dishesQtyDict[this.Uid] += this.Quantity;
                }
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
            if (_tsTimersDict != null) _tsTimersDict.Clear();
        }  // dispose

    }  // class OrderDishModel
}
