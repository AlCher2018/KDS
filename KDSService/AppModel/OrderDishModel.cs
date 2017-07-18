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
using KDSService.DataSource;

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
        // MS SQL data type Datetime: January 1, 1753, through December 31, 9999
        private DateTime sqlMinDate = new DateTime(1753, 1, 1);

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
        public int DepartmentId { get; set; }

        [DataMember]
        public string ServiceErrorMessage { get { return _serviceErrorMessage; } set { } }

        // форматированное представление временного промежутка для внешних клиентов
        // изменение значения таймера в зависимости от различных периодов ожидания и задержек, 
        // осуществляется на КЛИЕНТЕ!
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

        private bool _isDish, _isInrgIndepend, _isUseReadyConfirmed;
        private bool _isUpdateDependIngr;   // признак обновления ЗАВИСИМОГО ингредиента
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

            // свойства объекта с зависимыми полями
            DepartmentId = dbDish.DepartmentId;
            _department = ModelDicts.GetDepartmentById(DepartmentId); // объект отдела взять из справочника

            EstimatedTime = dbDish.EstimatedTime;
            _tsCookingEstimated = TimeSpan.FromSeconds(this.EstimatedTime);

            DishStatusId = dbDish.DishStatusId??0;
            Status = AppLib.GetStatusEnumFromNullableInt(dbDish.DishStatusId);

            // получить запись из таблицы состояний
            _dbRunTimeRecord = getDBRunTimeRecord(dbDish.Id);

            _isDish = ParentUid.IsNull();
            _isInrgIndepend = (bool)AppEnv.GetAppProperty("IsIngredientsIndependent");
            _isUseReadyConfirmed = (bool)AppEnv.GetAppProperty("UseReadyConfirmedState");
            _isUpdateDependIngr = false;

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
            if (_isUseReadyConfirmed)
                _tsTimersDict.Add(OrderStatusEnum.ReadyConfirmed, new TimeCounter() { Name = OrderStatusEnum.ReadyConfirmed.ToString() });
            // таймер времени ожидания фиксации заказа, нахождение в состоянии Выдано
            _tsTimersDict.Add(OrderStatusEnum.Took, new TimeCounter() { Name= OrderStatusEnum.Took.ToString()});
            // таймер нахождения в состоянии отмены
            _tsTimersDict.Add(OrderStatusEnum.Cancelled, new TimeCounter() { Name= OrderStatusEnum.Cancelled.ToString()});


            // отмененное блюдо/ингредиент
            // для новой записи сразу сохраняем в БД
            if (Quantity < 0)
            {
                if (Status != OrderStatusEnum.Cancelled)
                {
                    UpdateStatus(OrderStatusEnum.Cancelled, false);
                }
                else
                    startStatusTimerAtFirst();
            }
            else
            {
                // стартануть таймер для блюда или независимого ингредиента
                if (_isDish || (!_isDish && _isInrgIndepend))
                {
                    UpdateFromDBEntity(dbDish);  // для новой записи DTS не сохранен
                    startStatusTimerAtFirst();
                }
                // а для ЗАВИСИМОГО ингредиента - по родительскому блюду
                else updateIngredientByParentDish();
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
                // отмененное блюдо/ингредиент
                if ((Quantity < 0) && (newStatus != OrderStatusEnum.Cancelled)) newStatus = OrderStatusEnum.Cancelled;

                // обновление состояния для блюда или независимого ингредиента
                if (_isDish || (!_isDish && _isInrgIndepend))
                {
                    // проверяем условие автоматического перехода в режим приготовления
                    if ((newStatus <= OrderStatusEnum.WaitingCook) && canAutoPassToCookingStatus())
                    {
                        newStatus = OrderStatusEnum.Cooking;
                        _isUpdateDependIngr = true;
                    }

                    // если поменялся отдел, то объект отдела взять из справочника
                    if (DepartmentId != dbDish.DepartmentId) _department = ModelDicts.GetDepartmentById(dbDish.DepartmentId);

                    UpdateStatus(newStatus, false);
                }  // для БЛЮДА
                else
                {
                    UpdateStatus(newStatus, false);
                }

            }  // lock

        }  // method


        // ******************************************
        //    ОСНОВНАЯ ПРОЦЕДУРА БИЗНЕС-ЛОГИКИ
        //    ПРИ ИЗМЕНЕНИИ СТАТУСА БЛЮДА или НЕЗАВИСИМОГО ИНГРЕДИЕНТА
        // ******************************************
        // команды на изменение статуса блюда могут приходить как от КДС, так и из FrontOffice (при чтении из БД)
        // состояния и даты сохраняются в БД при каждом изменении
        //  isUpdateParentOrder = true, если запрос на изменение состояния пришел от КДС, иначе запрос из внутренней логики, напр. автоматическое изменение статуса из ожидания в готовку
        public bool UpdateStatus(OrderStatusEnum newStatus, bool isUpdateParentOrder,  
            DateTime dtEnterState = default(DateTime), int preStateTS = 0)
        {
            // если статус не поменялся для существующей записи, то ничего не делать
            if (this.Status == newStatus)
            {
                return false;
            }

            AppEnv.WriteLogTraceMessage("svc:  DISH.UpdateStatus() Id {0} ({1}), from {2} to {3} -- START", this.Id, this.Name, this.Status.ToString(), newStatus.ToString());

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
                        if (!_isUseReadyConfirmed && (preStatus == OrderStatusEnum.Ready) && (newStatus == OrderStatusEnum.Cooking))
                        {
                            _dbRunTimeRecord.ReadyDate = DateTime.MinValue;
                        }
                        if (_isUseReadyConfirmed && (preStatus == OrderStatusEnum.ReadyConfirmed) && (newStatus == OrderStatusEnum.Cooking))
                        {
                            _dbRunTimeRecord.ReadyConfirmedDate = DateTime.MinValue;
                        }
                    }

                    // запуск таймера для нового состояния
                    statusDTS = getStatusRunTimeDTS(this.Status);
                    startStatusTimer(statusDTS);

                    // если это блюдо
                    // и включен режим зависимости ингредиентов
                    if ((_isDish) && !_isInrgIndepend)
                    {
                        if (isUpdateParentOrder || _isUpdateDependIngr)
                            updateChildIngredients(dtEnterToNewStatus, secondsInPrevState);
                    }

                    // если это ингредиент, то попытаться обновить статус блюда по всем ингредиентам
                    //if (this.ParentUid.IsNull() == false) updateDishStatusByIngrStatuses();

                    // попытка обновить статус ЗАКАЗА проверкой состояний всех блюд/ингредиентов
                    if (isUpdateParentOrder) _modelOrder.UpdateStatusByVerificationDishes(preStatus, newStatus);

                    isUpdSuccess = true;
                }
            }

            AppEnv.WriteLogTraceMessage("svc:  DISH.UpdateStatus() Id {0} ({1}), from {2} to {3} -- FINISH", this.Id, this.Name, this.Status.ToString(), newStatus.ToString());

            return isUpdSuccess;
        }  // method UpdateStatus


        // первый (инициализирующий) старт таймера
        private void startStatusTimerAtFirst()
        {
            StatusDTS statusDTS = getStatusRunTimeDTS(this.Status);

            // установить дату входа в состояние
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

        // запуск таймера для обновленного состояния
        private void startStatusTimer(StatusDTS statusDTS)
        {
            DateTime dtEnterToStatus = statusDTS.DateEntered;
            // сохранить дату входа в состояние во внутреннем словаре
            _dtEnterStatusDict[this.Status] = dtEnterToStatus;

            if (_tsTimersDict.ContainsKey(this.Status)
                && ((_curTimer == null) || (_curTimer.Enabled == false) || (_curTimer != _tsTimersDict[this.Status])
                    || (_curTimer.StartDT != _tsTimersDict[this.Status].StartDT))
                )
            {
                _curTimer = _tsTimersDict[this.Status];
                _curTimer.Start(dtEnterToStatus);
            }
        }


        #region обновление ЗАВИСИМЫХ ингредиентов
        // для ингредиента попытаться обновить статус блюда по всем ингредиентам
        private void updateDishStatusByIngrStatuses()
        {
            OrderDishModel parentDish = getParentDish();
            if (parentDish != null)
            {
                List<OrderDishModel> ingrs = getIngredients();

                int iLen = Enum.GetValues(typeof(OrderStatusEnum)).Length;
                int[] statArray = new int[iLen];

                int iStatus, iDishesCount = ingrs.Count;
                foreach (OrderDishModel modelDish in ingrs)
                {
                    iStatus = modelDish.DishStatusId;
                    statArray[iStatus]++;
                }

                // в состояние 0 заказ автоматом переходить не должен
                for (int i = 1; i < iLen; i++)
                {
                    if (statArray[i] == iDishesCount)
                    {
                        OrderStatusEnum statDishes = AppLib.GetStatusEnumFromNullableInt(i);
                        if (parentDish.Status != statDishes)
                        {
                            parentDish.UpdateStatus(statDishes, false);
                        }
                        break;
                    }
                }
            }
        }

        // обновить добавленный к списку зависимый ингредиент по родительскому блюду
        // т.е. для нового объекта в заказе
        private void updateIngredientByParentDish()
        {
            // найти уже существующее блюдо для данного ингредиента
            _parentDish = getParentDish();
                
            if (_parentDish != null)
            {
                StatusDTS dtsBase = _parentDish.getStatusRunTimeDTS(_parentDish.Status);
                // и если оно поменялось, то обновляем статус ингредиента
                if ((this.DishStatusId != _parentDish.DishStatusId) && (_parentDish._isUpdateDependIngr))
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

        // получить родительское блюдо
        private OrderDishModel getParentDish()
        {
            return this._modelOrder.Dishes.Values.FirstOrDefault(
                d => d.ParentUid.IsNull() && (d.Uid == this.ParentUid) && (d.CreateDate == this.CreateDate)
                );
        }
        // получить список ингредиентов
        private List<OrderDishModel> getIngredients()
        {
            return this._modelOrder.Dishes.Values
                .Where(d => (d.Uid == this.Uid) && (d.ParentUid == this.Uid) && (d.CreateDate == this.CreateDate))
                .ToList();
        }

        // обновить уже существующие ингредиенты, зависимые от текущего блюда, по Uid блюда
        private void updateChildIngredients(DateTime dtEnterToNewStatus, int secondsInPrevState)
        {
            List<OrderDishModel> ingrs = getIngredients();
            foreach (OrderDishModel ingr in ingrs)
            {
                // TODO условие изменения статуса ингредиента - ТАКОЙ ЖЕ СТАТУС, КАК В БЛЮДЕ (в службе) или принадлежность отделу блюда (на клиенте)?
                if ((this.DishStatusId != ingr.DishStatusId))
                {
                    // поменять статус и запустить таймеры для 
                    ingr.UpdateStatus(this.Status, false, dtEnterToNewStatus, secondsInPrevState);
                }
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
                        if (_dbRunTimeRecord.InitDate == null) setStatusRunTimeDTS(OrderStatusEnum.WaitingCook, dateEntered, 0);
                        if (_dbRunTimeRecord.CookingStartDate == null) setStatusRunTimeDTS(OrderStatusEnum.Cooking, dateEntered, 0);
                        if (_dbRunTimeRecord.ReadyDate == null) setStatusRunTimeDTS(OrderStatusEnum.Ready, dateEntered, 0);
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
                    // в _dbRunTimeRecord могут быть даты 01.01.0001, которые не поддерживаются типом MS SQL DateTime, 
                    // который используется в БД !!!
                    checkDateFields(_dbRunTimeRecord);

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

        private void checkDateFields(OrderDishRunTime dbRec)
        {
            if ((dbRec.InitDate != null) 
                && ((dbRec.InitDate.Value.Ticks == 0) || (dbRec.InitDate.Value < sqlMinDate))) dbRec.InitDate = null;

            if ((dbRec.CookingStartDate != null)
                && ((dbRec.CookingStartDate.Value.Ticks == 0) || (dbRec.CookingStartDate.Value < sqlMinDate))) dbRec.CookingStartDate = null;

            if ((dbRec.ReadyDate != null)
                && ((dbRec.ReadyDate.Value.Ticks == 0) || (dbRec.ReadyDate.Value < sqlMinDate))) dbRec.ReadyDate = null;

            if ((dbRec.TakeDate != null)
                && ((dbRec.TakeDate.Value.Ticks == 0) || (dbRec.TakeDate.Value < sqlMinDate))) dbRec.TakeDate = null;

            if ((dbRec.CommitDate != null)
                && ((dbRec.CommitDate.Value.Ticks == 0) || (dbRec.CommitDate.Value < sqlMinDate))) dbRec.CommitDate = null;

            if ((dbRec.CancelDate != null)
                && ((dbRec.CancelDate.Value.Ticks == 0) || (dbRec.CancelDate.Value < sqlMinDate))) dbRec.CancelDate = null;

            if ((dbRec.CancelConfirmedDate != null)
                && ((dbRec.CancelConfirmedDate.Value.Ticks == 0) || (dbRec.CancelConfirmedDate.Value < sqlMinDate))) dbRec.CancelConfirmedDate = null;

            if ((dbRec.ReadyConfirmedDate != null)
                && ((dbRec.ReadyConfirmedDate.Value.Ticks == 0) || (dbRec.ReadyConfirmedDate.Value < sqlMinDate))) dbRec.ReadyConfirmedDate = null;
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
            int iStatus = (int)status;

            using (KDSEntities db = new KDSEntities())
            {
                try
                {
                    OrderDish dbDish = db.OrderDish.Find(this.Id);
                    if (dbDish != null)
                    {
                        if (dbDish.DishStatusId != iStatus)
                        {
                            dbDish.DishStatusId = iStatus;

                            AppEnv.WriteLogTraceMessage("   - save to db DISH id {0}, status = {1} - START", this.Id, status.ToString());
                            db.SaveChanges();
                            AppEnv.WriteLogTraceMessage("   - save to db DISH id {0}, status = {1} - FINISH", this.Id, status.ToString());
                        }
                        retVal = true;
                    }
                }
                catch (Exception ex)
                {
                    writeDBException(ex, "сохранения");
                }
            }

            //if (retVal)
            //{
            //    // убедиться, что в БД записан нужный статус
            //    DateTime dt = DateTime.Now;
            //    bool chkStat = false;
            //    while ((!chkStat) && ((DateTime.Now - dt).TotalMilliseconds <= 2000))
            //    {
            //        System.Threading.Thread.Sleep(100);  // тормознуться на 100 мс
            //        using (KDSEntities db = new KDSEntities())
            //        {
            //            try
            //            {
            //                OrderDish dbDish = db.OrderDish.Find(this.Id);
            //                chkStat = ((dbDish != null) && (dbDish.DishStatusId == iStatus));
            //            }
            //            catch (Exception ex)
            //            {
            //                AppEnv.WriteLogErrorMessage("Ошибка проверочного чтения после записи нового состояния в БД: {0}", AppEnv.GetShortErrMessage(ex));
            //            }
            //        }
            //    }
            //    // истекло время ожидания записи в БД
            //    if (!chkStat)
            //    {
            //        AppEnv.WriteLogErrorMessage("Истекло время ожидания проверочного чтения после записи нового состояния.");
            //    }
            //}

            return retVal;
        }

        private void writeDBException(Exception ex, string subMsg1)
        {
            _serviceErrorMessage = string.Format("Ошибка {0} записи в БД", subMsg1);
            AppEnv.WriteLogErrorMessage("   - DB Error DISH id {0}: {1}", this.Id, ex.ToString());
        }

        #endregion

        // проверка возможности АВТОМАТИЧЕСКОГО перехода в состояние Cooking
        private bool canAutoPassToCookingStatus()
        {
            DateTime n = DateTime.Now;
            // 1. для отдела установлен автоматический старт приготовления и текущая дата больше даты ожидаемого времени начала приготовления
            bool retVal = (_department.IsAutoStart 
                && (n >= this.CreateDate.AddSeconds(this.DelayedStartTime)));

            // 2. проверяем общее кол-во такого блюда в заказах, если установлено кол-во одновременно готовящихся блюд
            if (retVal == true)
            {
                Dictionary<int, decimal> dishesQtyDict = (Dictionary<int, decimal>)AppEnv.GetAppProperty("dishesQty");
                if ((dishesQtyDict != null) && (dishesQtyDict.ContainsKey(DepartmentId)))
                {
                    retVal = ((dishesQtyDict[DepartmentId] + this.Quantity) <= _department.DishQuantity);
                    // обновить кол-во в словаре, пока он не обновился из БД
                    if (retVal) dishesQtyDict[DepartmentId] += this.Quantity;
                }
            }

            return retVal;
        }

        public void Dispose()
        {
            AppEnv.WriteLogTraceMessage("     dispose class OrderDishModel id {0}", this.Id);

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
