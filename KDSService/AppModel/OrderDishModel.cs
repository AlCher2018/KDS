using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Diagnostics;
using System.ServiceModel;
using KDSService.DataSource;
using IntegraLib;
using KDSService.Lib;
using System.Data;

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
    [Serializable]
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
        public int DepartmentId { get; set; }

        [DataMember]
        public string UID1C { get; set; }

        [DataMember]
        public string ServiceErrorMessage { get { return _serviceErrorMessage; } set { } }

        [DataMember]
        public string GroupedDishIds { get; set; }

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
        private TimeSpan _tsCookingEstimated;   // время приготовления

        // накопительные счетчики нахождения в конкретном состоянии
        private Dictionary<OrderStatusEnum, TimeCounter> _tsTimersDict; // словарь накопительных счетчиков для различных состояний
        private TimeCounter _curTimer;  // текущий таймер для выдачи клиенту значения таймера

        // записи БД для сохранения блюда
        [NonSerialized]
        private OrderDishRunTime _dbRunTimeRecord = null;         // запись дат/времени прямого пути 
        private string _serviceErrorMessage;

        // ссылка на родительский заказ для обратных вызовов
        private OrderModel _modelOrder;

        private bool _isDish, _isUseReadyConfirmed;
        private int _autoGotoReadyConfirmPeriod;
        private DateTime _dtReadyStatusInput;
        private OrderDishModel _parentDish;

        public bool IsOutToOrderman = false;
        #endregion

        // for serialization
        public OrderDishModel()
        {
        }

        // ctor
        // ДЛЯ НОВОГО БЛЮДА
        public OrderDishModel(OrderDish dbDish, OrderModel modelOrder)
        {
            _modelOrder = modelOrder;

            Id = dbDish.Id; Uid = dbDish.UID;
            DepartmentId = dbDish.DepartmentId;
            CreateDate = dbDish.CreateDate;
            Name = dbDish.DishName;
            FilingNumber = dbDish.FilingNumber;
            ParentUid = dbDish.ParentUid;
            Comment = dbDish.Comment;
            Quantity = dbDish.Quantity;
            DelayedStartTime = dbDish.DelayedStartTime;
            UID1C = dbDish.UID1C;

            // свойства объекта с зависимыми полями

            EstimatedTime = dbDish.EstimatedTime;
            _tsCookingEstimated = TimeSpan.FromSeconds(this.EstimatedTime);

            DishStatusId = dbDish.DishStatusId;
            Status = AppLib.GetStatusEnumFromNullableInt(dbDish.DishStatusId);

            // получить запись из таблицы состояний
            _dbRunTimeRecord = getOrderDishRunTimeRecord(dbDish.Id);

            _isDish = ParentUid.IsNull();
            _isUseReadyConfirmed = AppProperties.GetBoolProperty("UseReadyConfirmedState");
            _autoGotoReadyConfirmPeriod = AppProperties.GetIntProperty("AutoGotoReadyConfirmPeriod");

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
                    UpdateStatus(OrderStatusEnum.Cancelled);
                }
                else
                    startStatusTimerAtFirst();
            }
            else
            {
                UpdateFromDBEntity(dbDish);  // для новой записи DTS не сохранен
                startStatusTimerAtFirst();
            }

        }  // constructor

        internal int GetInstanceSize()
        {
            int retVal = 0;

            int szInt = sizeof(int), szDate = 8, szDecimal = 16;
            // Id, Uid, CreateDate, Name, FilingNumber, Quantity, ParentUid, Comment,
            // EstimatedTime, DelayedStartTime, DishStatusId, DepartmentId,
            // ServiceErrorMessage
            // WaitingTimerString - 14
            retVal = szInt + (Uid.IsNull() ? 0 : Uid.Length) + szDate
                + (Name.IsNull() ? 0 : Name.Length)
                + szInt + szDecimal
                + (ParentUid.IsNull() ? 0 : ParentUid.Length)
                + (Comment.IsNull() ? 0 : Comment.Length)
                + szInt+ szInt + szInt+ szInt
                + (ServiceErrorMessage.IsNull() ? 0 : ServiceErrorMessage.Length)
                + (WaitingTimerString.IsNull() ? 0 : WaitingTimerString.Length);

            return retVal;
        }

        // обновить из БД
        internal void UpdateFromDBEntity(OrderDish dbDish)
        {
            lock (this)
            {
                // и для блюда, и для ингредиента
                if (DepartmentId != dbDish.DepartmentId) DepartmentId = dbDish.DepartmentId;
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

                // автоматическая установка состояний
                OrderStatusEnum newStatus = AppLib.GetStatusEnumFromNullableInt(dbDish.DishStatusId);
                // отмененное блюдо/ингредиент
                if ((Quantity < 0) && (newStatus != OrderStatusEnum.Cancelled))
                {
                    newStatus = OrderStatusEnum.Cancelled;
                }
                // автоматический переход из Готово в ПодтвГотово
                else if ((_isUseReadyConfirmed == true) && (_autoGotoReadyConfirmPeriod > 0)
                    && (newStatus == OrderStatusEnum.Ready) && (_dtReadyStatusInput.IsZero() == false)
                    && ((DateTime.Now - _dtReadyStatusInput).TotalSeconds >= _autoGotoReadyConfirmPeriod)
                    )
                {
                    _dtReadyStatusInput.SetZero();
                    newStatus = OrderStatusEnum.ReadyConfirmed;
                }

                UpdateStatus(newStatus);

            }  // lock

        }  // method


        // ******************************************
        //    ОСНОВНАЯ ПРОЦЕДУРА БИЗНЕС-ЛОГИКИ
        //    ПРИ ИЗМЕНЕНИИ СТАТУСА БЛЮДА или НЕЗАВИСИМОГО ИНГРЕДИЕНТА
        // ******************************************
        // команды на изменение статуса блюда могут приходить как от КДС, так и из FrontOffice (при чтении из БД)
        // состояния и даты сохраняются в БД при каждом изменении
        //  isUpdateParentOrder = true, если запрос на изменение состояния пришел от КДС, иначе запрос из внутренней логики, напр. автоматическое изменение статуса из ожидания в готовку
        public bool UpdateStatus(OrderStatusEnum newStatus,  
            DateTime dtEnterState = default(DateTime), int preStateTS = 0, string machineName = null)
        {
            // если статус не поменялся для существующей записи, то ничего не делать
            if (this.Status == newStatus)
            {
                return false;
            }

            // автоматический переход из Готово в ПодтвГотово: вход в режим отслеживания нахождения в состоянии Готов
            if ((_isUseReadyConfirmed == true) && (_autoGotoReadyConfirmPeriod > 0)
                && (newStatus == OrderStatusEnum.Ready) && (this.Status != OrderStatusEnum.Ready))
            {
                _dtReadyStatusInput = DateTime.Now;
            }

            string sLogMsg = string.Format(" - DISH.UpdateStatus() Id {0}/{1}, from {2} to {3}", this.Id, this.Name, this.Status.ToString(), newStatus.ToString());
            DateTime dtTmr = DateTime.Now; 
            if (machineName == null) AppLib.WriteLogOrderDetails(sLogMsg + " - START");
            else AppLib.WriteLogClientAction(machineName, sLogMsg + " - START");

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
                if (saveStatusToDB(newStatus, machineName))
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
                        if (_isUseReadyConfirmed && (preStatus == OrderStatusEnum.ReadyConfirmed) 
                            && ((newStatus == OrderStatusEnum.Cooking) || (newStatus == OrderStatusEnum.Ready)))
                        {
                            _dbRunTimeRecord.ReadyConfirmedDate = DateTime.MinValue;
                        }
                    }

                    // запуск таймера для нового состояния
                    statusDTS = getStatusRunTimeDTS(this.Status);
                    startStatusTimer(statusDTS);

                    // попытка обновить статус ЗАКАЗА проверкой состояний всех блюд/ингредиентов
                    _modelOrder.UpdateStatusByVerificationDishes(preStatus, newStatus);

                    isUpdSuccess = true;
                }
            }

            sLogMsg += " - FINISH - " + (DateTime.Now - dtTmr).ToString();
            if (machineName == null) AppLib.WriteLogOrderDetails(sLogMsg);
            else AppLib.WriteLogClientAction(machineName, sLogMsg);

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
                            parentDish.UpdateStatus(statDishes);
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
                if (this.DishStatusId != _parentDish.DishStatusId)
                {
                    StatusDTS preDtsBase = _parentDish.getStatusRunTimeDTS(this.Status);
                    UpdateStatus(_parentDish.Status, dtsBase.DateEntered, preDtsBase.TimeStanding);
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
                if ((this.DishStatusId != ingr.DishStatusId))
                {
                    // поменять статус и запустить таймеры для 
                    ingr.UpdateStatus(this.Status, dtEnterToNewStatus, secondsInPrevState);
                }
            }
        }

        #endregion

        #region DB FUNCS
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
                        if (_dbRunTimeRecord.InitDate.IsZero()) setStatusRunTimeDTS(OrderStatusEnum.WaitingCook, dateEntered, 0);
                    }
                    // для данного состояния накапливаем время нахождения в нем
                    if (timeStanding >= 0)
                    {
                        _dbRunTimeRecord.CookingTS = _dbRunTimeRecord.CookingTS + timeStanding;
                    }

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
                    if (dateEntered.IsZero() == false)
                    {
                        _dbRunTimeRecord.TakeDate = dateEntered;
                        // если предыдущие DTS пустые, то заполнить начальными значениями
                        if (_dbRunTimeRecord.InitDate.IsZero()) setStatusRunTimeDTS(OrderStatusEnum.WaitingCook, dateEntered, 0);
                        if (_dbRunTimeRecord.CookingStartDate.IsZero()) setStatusRunTimeDTS(OrderStatusEnum.Cooking, dateEntered, 0);
                        if (_dbRunTimeRecord.ReadyDate.IsZero()) setStatusRunTimeDTS(OrderStatusEnum.Ready, dateEntered, 0);
                    }
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


        // сохранить в БД запись из _dbRunTimeRecord
        private void saveRunTimeRecord()
        {
            AppLib.WriteLogTraceMessage(" - updating sql-table OrderDishRunTime..");

            string sqlText = getSQLUpdStringRunTimeRecord(_dbRunTimeRecord);

            int result = 0; string dbError = null;
            using (DBContext db = new DBContext())
            {
                result = db.ExecuteCommand(sqlText);
                dbError = db.ErrMsg;
            }

            if (result == 1)
            {
                AppLib.WriteLogTraceMessage(" - updating sql-table OrderDishRunTime.. - Ok");
            }
            else if (dbError != null)
            {
                AppLib.WriteLogTraceMessage(" - updating sql-table OrderDishRunTime.. - Error: " + dbError);
            }
        }

        private string getSQLUpdStringRunTimeRecord(OrderDishRunTime runTimeRecord)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("UPDATE [OrderDishRunTime] SET ");
            sb.AppendFormat("[OrderDishId] = {0}", _dbRunTimeRecord.OrderDishId.ToString());
            sb.AppendFormat(", [InitDate] = {0}", _dbRunTimeRecord.InitDate.ToSQLExpr());
            sb.AppendFormat(", [WaitingCookTS] = {0}", _dbRunTimeRecord.WaitingCookTS.ToString());
            sb.AppendFormat(", [CookingStartDate] = {0}", _dbRunTimeRecord.CookingStartDate.ToSQLExpr());
            sb.AppendFormat(", [CookingTS] = {0}", _dbRunTimeRecord.CookingTS.ToString());
            sb.AppendFormat(", [ReadyDate] = {0}", _dbRunTimeRecord.ReadyDate.ToSQLExpr());
            sb.AppendFormat(", [WaitingTakeTS] = {0}", _dbRunTimeRecord.WaitingTakeTS.ToString());
            sb.AppendFormat(", [TakeDate] = {0}", _dbRunTimeRecord.TakeDate.ToSQLExpr());
            sb.AppendFormat(", [WaitingCommitTS] = {0}", _dbRunTimeRecord.WaitingCommitTS.ToString());
            sb.AppendFormat(", [CommitDate] = {0}", _dbRunTimeRecord.CommitDate.ToSQLExpr());
            sb.AppendFormat(", [CancelDate] = {0}", _dbRunTimeRecord.CancelDate.ToSQLExpr());
            sb.AppendFormat(", [CancelConfirmedDate] = {0}", _dbRunTimeRecord.CancelConfirmedDate.ToSQLExpr());
            sb.AppendFormat(", [ReadyTS] = {0}", _dbRunTimeRecord.ReadyTS.ToString());
            sb.AppendFormat(", [ReadyConfirmedDate] = {0}", _dbRunTimeRecord.ReadyConfirmedDate.ToSQLExpr());
            sb.AppendFormat(" WHERE ([Id]={0})", _dbRunTimeRecord.Id.ToString());
            string sqlText = sb.ToString();
            sb = null;

            return sqlText;
        }


        private void saveReturnTimeRecord(OrderStatusEnum statusFrom, OrderStatusEnum statusTo, DateTime dtEnterToNewStatus, int secondsInPrevState)
        {
            string sLogMsg = " - updating sql-table OrderDishReturnTime..";
            AppLib.WriteLogTraceMessage(sLogMsg);

            string sqlText = string.Format("INSERT INTO [OrderDishReturnTime] ([OrderDishId], [ReturnDate], [StatusFrom], [StatusFromTimeSpan], [StatusTo]) VALUES ({0}, {1}, {2}, {3}, {4})",
                this.Id.ToString(), dtEnterToNewStatus.ToSQLExpr(), ((int)statusFrom).ToString(), secondsInPrevState.ToString(), ((int)statusTo).ToString());

            int result = 0; string dbError = null;
            using (DBContext db = new DBContext())
            {
                result = db.ExecuteCommand(sqlText);
                dbError = db.ErrMsg;
            }

            if (result == 1)
            {
                sLogMsg += " - Ok";
            }
            else if (dbError != null)
            {
                sLogMsg += " - error: " + dbError;
                _serviceErrorMessage = string.Format("Ошибка записи в БД: {0}", dbError);
            }
            AppLib.WriteLogTraceMessage(sLogMsg);
        }


        private bool saveStatusToDB(OrderStatusEnum status, string machineName = null)
        {
            int iStatus = (int)status;

            string sLogMsg = string.Format("   - save DISH {0}/{1}, status = {2}", this.Id, this.Name, status.ToString());
            DateTime dtTmr = DateTime.Now;
            if (machineName == null) AppLib.WriteLogOrderDetails(sLogMsg + " - START");
            else AppLib.WriteLogClientAction(machineName, sLogMsg);

            string sqlText = $"UPDATE [OrderDish] SET [DishStatusId] = {((int)status).ToString()} WHERE ([Id] = {this.Id})";

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
            }
            else if (dbError != null)
            {
                sLogMsg += ". Error: " + dbError;
                _serviceErrorMessage = string.Format("Ошибка записи в БД: {0}", dbError);
            }

            if (machineName == null) AppLib.WriteLogOrderDetails(sLogMsg);
            else AppLib.WriteLogClientAction(machineName, sLogMsg);

            return retVal;
        }

        private OrderDishRunTime getOrderDishRunTimeRecord(int dishId)
        {
            OrderDishRunTime runtimeRecord = null;
            runtimeRecord = getOrderDishRunTimeByOrderDishId(dishId);

            // если еще нет записи в БД, то добавить ее
            if (runtimeRecord == null)
            {
                runtimeRecord = newOrderDishRunTime(dishId);
                if (runtimeRecord == null)
                {
                    string _errMsg = string.Format("Ошибка создания записи в таблице OrderDishRunTime для блюда id {0}", dishId);
                    AppLib.WriteLogErrorMessage(_errMsg);
                    runtimeRecord = null;
                }
            }

            return runtimeRecord;
        }

        private OrderDishRunTime newOrderDishRunTime(int dishId)
        {
            string sqlText = $"INSERT INTO [OrderDishRunTime] (OrderDishId) VALUES ({dishId}); SELECT @@IDENTITY";

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
            OrderDishRunTime retVal = getOrderDishRunTimeById(newId);
            return retVal;
        }

        private OrderDishRunTime getOrderDishRunTimeByOrderDishId(int orderDishId)
        {
            string sqlText = string.Format("SELECT * FROM [OrderDishRunTime] WHERE ([OrderDishId] = {0})", orderDishId.ToString());
            return getOrderDishRunTime(sqlText);
        }
        private OrderDishRunTime getOrderDishRunTimeById(int id)
        {
            string sqlText = string.Format("SELECT * FROM [OrderDishRunTime] WHERE ([Id] = {0})", id.ToString());
            return getOrderDishRunTime(sqlText);
        }

        private OrderDishRunTime getOrderDishRunTime(string sqlText)
        {
            DataTable dt = null;
            string dbError = null;
            using (DBContext db = new DBContext())
            {
                dt = db.GetQueryTable(sqlText);
                dbError = db.ErrMsg;
            }
            if ((dt == null) || (dt.Rows.Count == 0))
            {
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

        #endregion

        public OrderDishModel Copy()
        {
            OrderDishModel retVal = (OrderDishModel)this.MemberwiseClone();

            retVal._tsTimersDict = null;
            retVal._curTimer = this._curTimer;
            retVal._dbRunTimeRecord = null;

            return retVal;
        }

        public void Dispose()
        {
            AppLib.WriteLogTraceMessage("     dispose class OrderDishModel id {0}", this.Id);

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
