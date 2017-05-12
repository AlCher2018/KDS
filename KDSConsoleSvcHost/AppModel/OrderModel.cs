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

        #region Fields
        // FIELDS
        // накопительные счетчики нахождения в конкретном состоянии
        private Dictionary<OrderStatusEnum, IncrementalTimer> _tsTimersDict; // словарь накопительных счетчиков для различных состояний
        private IncrementalTimer _curTimer;  // текущий таймер для выдачи клиенту значения таймера

        // записи БД для сохранения блюда
        private OrderRunTime _dbRunTimeRecord = null;   // запись дат/времени прямого пути 
        private string _serviceErrorMessage;
        #endregion


        // ctor
        public OrderModel(Order dbOrder)
        {
            _dishesDict = new Dictionary<int, OrderDishModel>();

            UpdateFromDBEntity(dbOrder, true);

            // создать словарь накопительных счетчиков
            // таймер ожидания начала приготовления
            _tsTimersDict.Add(OrderStatusEnum.WaitingCook, new IncrementalTimer(500));
            // таймер времени приготовления
            _tsTimersDict.Add(OrderStatusEnum.Cooking, new IncrementalTimer(500));
            // таймер времени ожидания выдачи, нахождение в состоянии Готов
            _tsTimersDict.Add(OrderStatusEnum.Ready, new IncrementalTimer(500));
            // таймер времени ожидания фиксации заказа, нахождение в состоянии Выдано
            _tsTimersDict.Add(OrderStatusEnum.Took, new IncrementalTimer(500));

            // получить отсоединенную RunTime запись из таблицы состояний
            _dbRunTimeRecord = getDBRunTimeRecord(dbOrder.Id);
        }

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
                // обновить состояние или добавить блюда
                foreach (OrderDish dbDish in dbOrder.OrderDish)
                {
                    if (this._dishesDict.ContainsKey(dbDish.Id))  // есть такое блюдо во внут.словаре - обновить из БД
                    {
                        this._dishesDict[dbDish.Id].UpdateFromDBEntity(dbDish, false);
                        // если статус поменялся в БД у заказа, то поменять статус у всех блюд
                        if (this.Status != newStatus) this._dishesDict[dbDish.Id].UpdateStatus(newStatus);
                    }
                    // иначе - добавить 
                    else
                    {
                        OrderDishModel newDish = new OrderDishModel(dbDish);
                        this._dishesDict.Add(newDish.Id, newDish);
                    }
                }
            }  // lock
        }  // method UpdateFromDBEntity

        // внешнее обновление состояния заказа
        public void UpdateStatus(OrderStatusEnum newStatus)
        {
            if (this.Status == newStatus) return; // если статус не поменялся, то ничего не делать

            // дата входа в НОВОЕ состояние
            DateTime dtEnterToNewStatus = DateTime.Now;
            TimeSpan tsAllTimeInPrevState = TimeSpan.Zero;
            if (_curTimer != null)   // если есть таймер предыдущего состояния
            {
                _curTimer.Stop(); // остановить таймер состояния
                // получить ОБЩЕЕ время нахождения в состоянии
                tsAllTimeInPrevState = _curTimer.ValueTS;
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

            _curTimer.Stop();

            // просто обновить состояние блюд
            foreach (OrderDishModel modelDish in _dishesDict.Values)
            {
                modelDish.UpdateStatus(newStatus);
            }
        }

        private void writeStatusEnterEventToDB(OrderStatusEnum prevStatus, OrderStatusEnum newStatus, DateTime dtEnterToNewStatus, TimeSpan tsAllTimeInPrevState)
        {
            if (_dbRunTimeRecord == null) return;

            // сохраняем дату входа в новое состояние
            runTimeStatusEnterDate(newStatus, false, dtEnterToNewStatus);
            // сохраняем в записи RunTimeRecord время нахождения в предыдущем состоянии
            setRunTimeStatusTimeSpan(prevStatus, tsAllTimeInPrevState.ToIntSec());

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


        // метод, который или возвращает признак null-значения поля даты входа в состояние (isNullCheck == true)
        // или устанавливает в это поле текущую дату (isNullCheck == false)
        private bool runTimeStatusEnterDate(OrderStatusEnum status, bool isNullCheck, DateTime dtEnterToNewStatus)
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
        private void setRunTimeStatusTimeSpan(OrderStatusEnum status, int seconds)
        {
            switch (status)
            {
                case OrderStatusEnum.None:
                    break;
                case OrderStatusEnum.WaitingCook:
                    _dbRunTimeRecord.WaitingCookTS = seconds;
                    break;
                case OrderStatusEnum.Cooking:
                    _dbRunTimeRecord.CookingTS = seconds;
                    break;
                case OrderStatusEnum.Ready:
                    _dbRunTimeRecord.WaitingTakeTS = seconds;
                    break;
                case OrderStatusEnum.Took:
                    _dbRunTimeRecord.WaitingCommitTS = seconds;
                    break;
                case OrderStatusEnum.Cancelled:
                    break;
                case OrderStatusEnum.Commit:
                    break;
                default:
                    break;
            }
        }


        private void writeDBException(Exception ex, string subMsg1)
        {
            _serviceErrorMessage = string.Format("Ошибка {0} записи в БД", subMsg1);
            AppEnv.WriteLogErrorMessage("DB Error (dish id {0}): {1}", this.Id, ex.Message);
        }

        public void Dispose()
        {
            // таймеры
            if (_tsTimersDict != null)
            {
                foreach (var item in _tsTimersDict) item.Value.Dispose();
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
