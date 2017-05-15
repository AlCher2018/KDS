using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using KDSConsoleSvcHost;
using KDSService.Lib;
using KDSConsoleSvcHost.AppModel;


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
        public OrderStatusEnum Status { get; set; }

        private Dictionary<int, OrderDishModel> _dishesDict;
        [DataMember]
        public Dictionary<int, OrderDishModel> Dishes
        {
            get { return _dishesDict; }
            set { }
        }

        // форматированное представление временного промежутка для внешних клиентов
        [DataMember]
        public string WaitingTimerString
        {
            get
            {
                string retVal = null;
                if (_curTimer != null)
                {
                    TimeSpan tsTimerValue = TimeSpan.FromSeconds(_curTimer.ValueTS);

                    retVal = (tsTimerValue.Days > 0d) ? tsTimerValue.ToString(@"d\.hh\:mm\:ss") : tsTimerValue.ToString(@"hh\:mm\:ss");
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
        private Dictionary<OrderStatusEnum, IncrementalTimer> _tsTimersDict; // словарь накопительных счетчиков для различных состояний
        private IncrementalTimer _curTimer;  // текущий таймер для выдачи клиенту значения таймера

        // записи БД для сохранения блюда
        private OrderRunTime _dbRunTimeRecord = null;   // запись дат/времени прямого пути 
        private string _serviceErrorMessage;

        private bool _isUpdStatusFromDishes;
        #endregion


        // *** CONSTRUCTOR  ***
        public OrderModel(Order dbOrder)
        {
            _dishesDict = new Dictionary<int, OrderDishModel>();
            // получить отсоединенную RunTime запись из таблицы состояний
            _dbRunTimeRecord = getDBRunTimeRecord(dbOrder.Id);

            // создать словарь накопительных счетчиков
            _tsTimersDict = new Dictionary<OrderStatusEnum, IncrementalTimer>();
            // таймер времени приготовления
            _tsTimersDict.Add(OrderStatusEnum.Cooking, new IncrementalTimer(1000));
            // таймер времени ожидания выдачи, нахождение в состоянии Готов
            _tsTimersDict.Add(OrderStatusEnum.Ready, new IncrementalTimer(1000));
            // таймер времени ожидания фиксации заказа, нахождение в состоянии Выдано
            _tsTimersDict.Add(OrderStatusEnum.Took, new IncrementalTimer(1000));

            // для заказа статус по умолчанию - В ПРОЦЕССЕ ГОТОВКИ
            if ((int)this.Status < 1) this.Status = OrderStatusEnum.Cooking;

            UpdateFromDBEntity(dbOrder, true);

            #region условия запуска таймера текущего состояния
            // *** базовое значение таймера состояния 
            _curTimer = null;
            if (_tsTimersDict.ContainsKey(this.Status))
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

                    setStatusRunTimeDTS(this.Status, initDT, 0);
                    saveRunTimeRecord();
                }
                else
                {
                    _curTimer.InitDateTimeValue(statusDTS.DateEntered);
                }
                _curTimer.Start();    // стартуем таймер состояния
            }
            #endregion

        }  // ctor

        // translate Order to OrderSvcModel
        public void UpdateFromDBEntity(Order dbOrder, bool isNew)
        {
            lock (this)
            {
                if (isNew)
                {
                    Id = dbOrder.Id; Uid = dbOrder.UID; Number = dbOrder.Number;
                    TableName = dbOrder.TableNumber;
                    CreateDate = dbOrder.CreateDate;
                    HallName = dbOrder.RoomNumber;
                    Waiter = dbOrder.Waiter;
                    Status = AppLib.GetStatusEnumFromNullableInt(dbOrder.OrderStatusId);
                }
                else
                {
                    if (Uid.IsNull() || (Uid != dbOrder.UID)) Uid = dbOrder.UID;
                    if (Number != dbOrder.Number) Number = dbOrder.Number;
                    if (TableName.IsNull() || (TableName != dbOrder.TableNumber)) TableName = dbOrder.TableNumber;
                    if (CreateDate != dbOrder.CreateDate) CreateDate = dbOrder.CreateDate;
                    if (HallName.IsNull() || (HallName != dbOrder.RoomNumber)) HallName = dbOrder.RoomNumber;
                    if (Waiter.IsNull() || (Waiter != dbOrder.Waiter)) Waiter = dbOrder.Waiter;
                }
                OrderStatusEnum newStatus = AppLib.GetStatusEnumFromNullableInt(dbOrder.OrderStatusId);


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
                        // если это ингредиент, то он может быть уже обновлен предыдущим блюдом
                        if (dbDish.ParentUid.IsNull() == false)
                        {
                            OrderDish dbIngrDish;
                            KDSEntities db = new KDSEntities();
                            dbIngrDish = db.OrderDish.Find(dbDish.Id);
                            if (dbIngrDish != null) this._dishesDict[dbDish.Id].UpdateFromDBEntity(dbIngrDish);
                        }
                        else
                            this._dishesDict[dbDish.Id].UpdateFromDBEntity(dbDish);

                        // если статус поменялся в БД у заказа, то поменять статус у всех блюд
                        if ((_isUpdStatusFromDishes == false) && (this.Status != newStatus))
                            this._dishesDict[dbDish.Id].UpdateStatus(newStatus, false);
                    }
                    // иначе - добавить 
                    else
                    {
                        OrderDishModel newDish = new OrderDishModel(dbDish, this);
                        this._dishesDict.Add(newDish.Id, newDish);
                    }
                }
            }  // lock
        }  // method UpdateFromDBEntity

        // внешнее обновление состояния заказа
        // параметр isUpdateDishStatus = true, если заказ БЫЛ обновлен ИЗВНЕ, из БД/КДС
        //          isUpdateDishStatus = false, если заказ БУДЕТ обновлен по общему состоянию всех блюд
        public void UpdateStatus(OrderStatusEnum newStatus, bool isUpdateDishStatus)
        {
            // если статус не поменялся, то ничего не делать
            //if (this.Status == newStatus) return; 

            lock (this)
            {
                DateTime dtEnterToNewStatus = DateTime.MinValue;
                int secondsInPrevState = 0;

                // если есть таймер предыдущего состояния, то остановить его
                if (_curTimer != null)
                {
                    _curTimer.Stop();
                    secondsInPrevState = _curTimer.ValueTS;
                }

                // дата входа в НОВОЕ состояние
                if (isUpdateDishStatus)  // обновление заказа извне
                {
                    dtEnterToNewStatus = DateTime.Now;
                }
                else  // обновление заказа по ПОСЛЕДНЕМУ состоянию блюд
                {
                    dtEnterToNewStatus = getMaxDishEnterStateDate(newStatus);
                }

                // запись в RunTimeRecord
                if (_dbRunTimeRecord != null)
                {
                    // сохраняем в записи RunTimeRecord дату входа в новое состояние и время нахождения в предыдущем состоянии
                    setStatusRunTimeDTS(this.Status, DateTime.MinValue, secondsInPrevState);
                    setStatusRunTimeDTS(newStatus, dtEnterToNewStatus, 0);
                    saveRunTimeRecord();
                }

                // сохранить новый статус в БД
                if (saveStatusToDB(newStatus))
                {
                    // запуск таймера для нового состояния, чтобы клиент мог получить значение таймера
                    _curTimer = null;
                    if (_tsTimersDict.ContainsKey(newStatus))
                    {
                        _curTimer = _tsTimersDict[newStatus];
                        _curTimer.Start();
                    }

                    // сохранить новый статус в объекте
                    Status = newStatus;

                    // просто обновить состояние блюд
                    if (isUpdateDishStatus)
                    {
                        foreach (OrderDishModel modelDish in _dishesDict.Values)
                        {
                            modelDish.UpdateStatus(newStatus, false);
                        }
                    }
                }

            }  // lock
        }  // method


        // обновление состояния заказа проверкой состояний всех блюд
        // установить сост.заказа в 0,2,3,4,5 если ВСЕ блюда наход.в этом состоянии
        // установить сост.заказа в 1, если ХОТЬ одно блюдо наход.в сост. 1
        public void UpdateStatusByVerificationDishes()
        {
            int iLen = 10;
            int[] statArray = new int[iLen];

            int iStatus, iDishesCount = _dishesDict.Count;
            foreach (OrderDishModel modelDish in _dishesDict.Values)
            {
                iStatus = (int)modelDish.Status;
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
            DateTime retVal = DateTime.MinValue, dt;

            foreach (OrderDishModel modelDish in _dishesDict.Values)
            {
                dt = modelDish.EnterStatusDict[(int)newStatus];  // словарь в блюде дат входов в состояния
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
                    if (timeStanding != 0) _dbRunTimeRecord.WaitingCookTS = timeStanding;
                    break;

                case OrderStatusEnum.Cooking:
                    if (dateEntered.IsZero() == false) _dbRunTimeRecord.CookingStartDate = dateEntered;
                    if (timeStanding != 0) _dbRunTimeRecord.CookingTS = timeStanding;
                    break;

                case OrderStatusEnum.Ready:
                    if (dateEntered.IsZero() == false) _dbRunTimeRecord.ReadyDate = dateEntered;
                    if (timeStanding != 0) _dbRunTimeRecord.WaitingTakeTS = timeStanding;
                    break;

                case OrderStatusEnum.Took:
                    if (dateEntered.IsZero() == false) _dbRunTimeRecord.TakeDate = dateEntered;
                    if (timeStanding != 0) _dbRunTimeRecord.WaitingCommitTS = timeStanding;
                    break;

                case OrderStatusEnum.Cancelled:
                    if (dateEntered.IsZero() == false) _dbRunTimeRecord.CancelDate = dateEntered;
                    break;

                case OrderStatusEnum.Commit:
                    if (dateEntered.IsZero() == false) _dbRunTimeRecord.CommitDate = dateEntered;
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
                    db.OrderRunTime.Attach(_dbRunTimeRecord);
                    // указать, что запись изменилась
                    db.Entry<OrderRunTime>(_dbRunTimeRecord).State = System.Data.Entity.EntityState.Modified;
                    db.SaveChanges();
                }
                catch (Exception ex)
                {
                    writeDBException(ex, "обновления");
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
                    Order dbOrder = db.Order.Find(this.Id);
                    if (dbOrder != null)
                    {
                        dbOrder.OrderStatusId = (int)status;
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
            AppEnv.WriteLogTraceMessage("   dispose class OrderModel id {0}", this.Id);

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

            // задиспозить блюда
            if (_dishesDict != null)
            {
                foreach (OrderDishModel modelDish in _dishesDict.Values) modelDish.Dispose();
                _dishesDict.Clear();
            }
        }

    }  // class OrderModel
}
