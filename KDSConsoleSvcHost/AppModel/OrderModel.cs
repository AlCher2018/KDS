using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using KDSConsoleSvcHost;
using KDSService.Lib;
using System.ServiceModel;

namespace KDSService.AppModel
{
    [DataContract]
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
        // FIELDS
        // накопительные счетчики нахождения в конкретном состоянии
        private Dictionary<OrderStatusEnum, TimeCounter> _tsTimersDict; // словарь накопительных счетчиков для различных состояний
        private TimeCounter _curTimer;  // текущий таймер для выдачи клиенту значения таймера

        // записи БД для сохранения блюда
        private OrderRunTime _dbRunTimeRecord = null;   // запись дат/времени прямого пути 
        private string _serviceErrorMessage;

        private bool _isUpdStatusFromDishes;
        private bool _isUseReadyConfirmed;
        #endregion


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

            _isUseReadyConfirmed = (bool)AppEnv.GetAppProperty("UseReadyConfirmedState");

            _dishesDict = new Dictionary<int, OrderDishModel>();
            // получить отсоединенную RunTime запись из таблицы состояний
            _dbRunTimeRecord = getDBRunTimeRecord(dbOrder.Id);

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
                OrderStatusEnum eStatusAllDishes = AppEnv.GetStatusAllDishes(dbOrder.OrderDish);
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
            List<OrderDish> dList = dbOrder.OrderDish.Where(d => d.ParentUid.IsNull()).ToList();
            List<OrderDish> dAll = new List<OrderDish>();
            foreach (OrderDish dish in dList)
            {
                dAll.Add(dish);
                List<OrderDish> dIngr = dbOrder.OrderDish.Where(d => d.ParentUid==dish.UID).ToList();
                foreach (var ingr in dIngr) dAll.Add(ingr);
            }

            foreach (OrderDish dbDish in dAll)   // dbOrder.OrderDish
            {
                OrderDishModel newDish = new OrderDishModel(dbDish, this);
                if (this._dishesDict.ContainsKey(newDish.Id) == false) this._dishesDict.Add(newDish.Id, newDish);
            }

        }  // ctor

        // копия только значений, без ссылок
        public OrderModel Copy()
        {
            OrderModel retVal = (OrderModel)this.MemberwiseClone();

            // удалить все ссылки
            retVal._dishesDict = null;  // без ссылки на блюда
            retVal._tsTimersDict = null;
            retVal._curTimer = null;
            retVal._dbRunTimeRecord = null;
            retVal.Dishes = null;

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
                // удалить блюда из внутр.модели заказа, которых уже нет в БД
                List<int> idDishList = dbOrder.OrderDish.Select(d => d.Id).ToList();  // все Id блюд из БД
                List<int> idForRemove = _dishesDict.Keys.Except(idDishList).ToList();  // Id блюд для удаления
                foreach (int idDish in idForRemove)
                {
                    _dishesDict[idDish].Dispose(); _dishesDict.Remove(idDish);

                }

                _isUpdStatusFromDishes = false;
                // обновить состояние или добавить блюда
                foreach (OrderDish dbDish in dbOrder.OrderDish)
                {
                    if (this._dishesDict.ContainsKey(dbDish.Id))  // есть такое блюдо во внут.словаре - обновить из БД
                    {
                        // обновлять состояние только БЛЮДА, т.к. состояние ингредиентов уже должно быть обновлено из блюда
                        //if (dbDish.ParentUid.IsNull())  // это блюдо
                        //{
                            this._dishesDict[dbDish.Id].UpdateFromDBEntity(dbDish);
                        //}
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
        public void UpdateStatus(OrderStatusEnum newStatus, bool isUpdateDishStatus)
        {
            // если статус не поменялся, то попытаться обновить только статус блюд
            if (this.Status == newStatus) return;

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
            if (saveStatusToDB(newStatus))
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
                            if (modelDish.ParentUid.IsNull())  // только для блюд
                            {
                                // дату входа в состояние берем из заказа, а время нахожд.в предыд.состоянии из самого блюда
                                dishUpdSuccess &= modelDish.UpdateStatus(newStatus, false, statusDTS.DateEntered);
                            }

                        }
                    }
                    catch (Exception ex)
                    {
                        AppEnv.WriteLogErrorMessage("Ошибка обновления статуса блюд при обновлении статуса заказа {0} с {1} на {2}: {3}", this.Id, this.Status, newStatus, ex.Message);
                        dishUpdSuccess = false;
                    }
                }

            }
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
            int iLen = Enum.GetValues(typeof(OrderStatusEnum)).Length;
            int[] statArray = new int[iLen];

            int iStatus, iDishesCount = _dishesDict.Count;
            foreach (OrderDishModel modelDish in _dishesDict.Values)
            {
                iStatus = modelDish.DishStatusId;
                statArray[iStatus]++;
            }

            // в состояние 0 заказ автоматом переходить не должен
            for(int i=1; i < iLen; i++)
            {
                if ((i == 1) && (statArray[i] > 0))
                    UpdateStatus(OrderStatusEnum.Cooking, false);
                else if (statArray[i] == iDishesCount)
                {
                    OrderStatusEnum statDishes = AppLib.GetStatusEnumFromNullableInt(i);
                    if (this.Status != statDishes)
                    {
                        UpdateStatus(statDishes, false);
                        _isUpdStatusFromDishes = true;
                    }
                    break;
                }
            }
        }  // method

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

        #region DB funcs
        //   DB FUNCS
        private OrderRunTime getDBRunTimeRecord(int id)
        {
            OrderRunTime runtimeRecord = null;
            using (KDSEntities db = new KDSEntities())
            {
                runtimeRecord = db.OrderRunTime.FirstOrDefault(rec => rec.OrderId == id);
                if (runtimeRecord == null)
                {
                    runtimeRecord = new OrderRunTime() { OrderId = id };
                    db.OrderRunTime.Add(runtimeRecord);
                    try
                    {
                        db.SaveChanges();
                    }
                    catch (Exception ex)
                    {
                        _serviceErrorMessage = string.Format("Ошибка создания записи в таблице OrderRunTime для блюда id {0}", id);
                        AppEnv.WriteLogErrorMessage("Ошибка создания записи в таблице OrderRunTime для блюда id {0}: {1}", id, ex.ToString());
                        runtimeRecord = null;
                    }
                }
            }
            return runtimeRecord;
        }


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

        private bool saveRunTimeRecord()
        {
            bool retVal = false;
            // приаттачить и сохранить в DB-контексте два поля из RunTimeRecord
            using (KDSEntities db = new KDSEntities())
            {
                try
                {
                    db.OrderRunTime.Attach(_dbRunTimeRecord);
                    // указать, что запись изменилась
                    db.Entry<OrderRunTime>(_dbRunTimeRecord).State = System.Data.Entity.EntityState.Modified;
                    db.SaveChanges();
                    retVal = true;
                }
                catch (Exception ex)
                {
                    writeDBException(ex, "обновления");
                }
            }
            return retVal;
        }

        private bool saveStatusToDB(OrderStatusEnum status)
        {
            bool retVal = false;
            using (KDSEntities db = new KDSEntities())
            {
                try
                {
                    Order dbOrder = db.Order.Find(this.Id);
                    if (dbOrder != null)
                    {
                        dbOrder.OrderStatusId = (int)status;

                        // записать в поле QueueStatusId значение для очереди заказов
                        if (status == OrderStatusEnum.Cooking)
                        {
                            if (dbOrder.QueueStatusId != 0) dbOrder.QueueStatusId = 0;
                        }
                        else if ((!_isUseReadyConfirmed && (status == OrderStatusEnum.Ready))
                            || (_isUseReadyConfirmed && (status == OrderStatusEnum.ReadyConfirmed)))
                            dbOrder.QueueStatusId = 1;
                        else if (status == OrderStatusEnum.Took)
                            dbOrder.QueueStatusId = 2;
                        else
                            dbOrder.QueueStatusId = dbOrder.OrderStatusId-1;

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
            AppEnv.WriteLogErrorMessage("DB Error (dish id {0}): {1}", this.Id, ex.Message);
        }

        #endregion

        public void Dispose()
        {
#if DEBUG
            AppEnv.WriteLogTraceMessage("   dispose class OrderModel id {0}", this.Id);
#endif
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
