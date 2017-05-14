﻿using System;
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

            UpdateFromDBEntity(dbOrder, true);

            // для сохраненного статуса берем время в секундах из БД
            int? savedTS = setRunTimeStatusTimeSpan(this.Status, true, 0);
            if (savedTS != null)
            {
                int tsSeconds = Convert.ToInt32(savedTS);
                DateTime initDT = DateTime.Now.AddSeconds(-tsSeconds);
                // сразу стартуем таймер приготовления
                _curTimer = _tsTimersDict[this.Status];
                _curTimer.InitDateTimeValue(initDT);
                _curTimer.Start();

                if (runTimeStatusEnterDate(this.Status, true, null) == true)
                {
                    runTimeStatusEnterDate(this.Status, false, initDT);
                    saveRunTimeRecord();
                }
            }
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
            if (this.Status == newStatus) return; // если статус не поменялся, то ничего не делать

            DateTime dtEnterToNewStatus = DateTime.MinValue;
            TimeSpan tsAllTimeInPrevState = TimeSpan.Zero;

            // если есть таймер предыдущего состояния, то остановить его
            if (_curTimer != null)
            {
                _curTimer.Stop();
                tsAllTimeInPrevState = TimeSpan.FromSeconds(_curTimer.ValueTS);
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
            writeStatusEnterEventToDB(Status, newStatus, dtEnterToNewStatus, tsAllTimeInPrevState);

            // запуск таймера для нового состояния, чтобы клиент мог получить значение таймера
            _curTimer = null;
            if (_tsTimersDict.ContainsKey(newStatus))
            {
                _curTimer = _tsTimersDict[newStatus];
                _curTimer.Start();
            }
            // сохранить новый статус в объекте
            Status = newStatus;
            saveStatusToDB(); // и в БД

            // просто обновить состояние блюд
            if (isUpdateDishStatus)
            {
                foreach (OrderDishModel modelDish in _dishesDict.Values)
                {
                    modelDish.UpdateStatus(newStatus, false);
                }
            }
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


        private void writeStatusEnterEventToDB(OrderStatusEnum prevStatus, OrderStatusEnum newStatus, DateTime dtEnterToNewStatus, TimeSpan tsAllTimeInPrevState)
        {
            if (_dbRunTimeRecord == null) return;

            // сохраняем дату входа в новое состояние
            runTimeStatusEnterDate(newStatus, false, dtEnterToNewStatus);
            // сохраняем в записи RunTimeRecord время нахождения в предыдущем состоянии
            setRunTimeStatusTimeSpan(prevStatus, false, tsAllTimeInPrevState.ToIntSec());

            saveRunTimeRecord();
        }

        // метод, который или возвращает признак null-значения поля даты входа в состояние (isNullCheck == true)
        // или устанавливает в это поле текущую дату (isNullCheck == false)
        private bool runTimeStatusEnterDate(OrderStatusEnum status, bool isNullCheck, DateTime? dtEnterToNewStatus)
        {
            switch (status)
            {
                case OrderStatusEnum.None:
                    break;
                case OrderStatusEnum.WaitingCook:
                    break;

                case OrderStatusEnum.Cooking:
                    if (isNullCheck) return (_dbRunTimeRecord.CookingStartDate == null);
                    else _dbRunTimeRecord.CookingStartDate = dtEnterToNewStatus;
                    break;

                case OrderStatusEnum.Ready:
                    if (isNullCheck) return (_dbRunTimeRecord.ReadyDate == null);
                    else _dbRunTimeRecord.ReadyDate = dtEnterToNewStatus;
                    break;

                case OrderStatusEnum.Took:
                    if (isNullCheck) return (_dbRunTimeRecord.TakeDate == null);
                    else _dbRunTimeRecord.TakeDate = dtEnterToNewStatus;
                    break;

                case OrderStatusEnum.Cancelled:
                    if (isNullCheck) return (_dbRunTimeRecord.CancelDate == null);
                    else _dbRunTimeRecord.CancelDate = dtEnterToNewStatus;
                    break;

                case OrderStatusEnum.Commit:
                    if (isNullCheck) return (_dbRunTimeRecord.CommitDate == null);
                    else _dbRunTimeRecord.CommitDate = dtEnterToNewStatus;
                    break;

                default:
                    break;
            }
            return false;
        }

        // записать в поле RunTimeRecord время нахождения (seconds) в состоянии status
        private int? setRunTimeStatusTimeSpan(OrderStatusEnum status, bool isNullCheck, int seconds)
        {
            switch (status)
            {
                case OrderStatusEnum.None:
                    break;
                case OrderStatusEnum.WaitingCook:
                    if (isNullCheck) return _dbRunTimeRecord.WaitingCookTS;
                    else _dbRunTimeRecord.WaitingCookTS = seconds;
                    break;
                case OrderStatusEnum.Cooking:
                    if (isNullCheck) return _dbRunTimeRecord.CookingTS;
                    else _dbRunTimeRecord.CookingTS = seconds;
                    break;
                case OrderStatusEnum.Ready:
                    if (isNullCheck) return _dbRunTimeRecord.WaitingTakeTS;
                    else _dbRunTimeRecord.WaitingTakeTS = seconds;
                    break;
                case OrderStatusEnum.Took:
                    if (isNullCheck) return _dbRunTimeRecord.WaitingCommitTS;
                    else _dbRunTimeRecord.WaitingCommitTS = seconds;
                    break;
                case OrderStatusEnum.Cancelled:
                    break;
                case OrderStatusEnum.Commit:
                    break;
                default:
                    break;
            }
            return null;
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
                    db.Entry(_dbRunTimeRecord).State = System.Data.Entity.EntityState.Modified;
                    db.SaveChanges();
                }
                catch (Exception ex)
                {
                    writeDBException(ex, "обновления");
                }
            }
        }

        private void saveStatusToDB()
        {
            using (KDSEntities db = new KDSEntities())
            {
                try
                {
                    Order dbOrder = db.Order.Find(this.Id);
                    if (dbOrder != null)
                    {
                        dbOrder.OrderStatusId = (int)this.Status;
                        db.SaveChanges();
                    }
                }
                catch (Exception ex)
                {
                    writeDBException(ex, "сохранения");
                }
            }
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

            // таймеры
            if (_tsTimersDict != null)
            {
                foreach (var item in _tsTimersDict.Values)
                {
                    if (item.Enabled)
                    {
                        item.Stop();
                        if (_dbRunTimeRecord != null)
                        {
                            // сохраняем в записи RunTimeRecord время нахождения в текущем состоянии
                            setRunTimeStatusTimeSpan(this.Status, false, item.ValueTS);
                            saveRunTimeRecord();
                        }
                    }
                    item.Dispose();
                }
            }

            // задиспозить блюда
            foreach (OrderDishModel modelDish in _dishesDict.Values)
            {
                modelDish.Dispose();
            }
            _dishesDict.Clear();
        }

    }  // class OrderModel
}
