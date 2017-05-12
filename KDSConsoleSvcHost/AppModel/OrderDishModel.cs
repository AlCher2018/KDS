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
        #region Fields
        // поля дат состояний и временных промежутков
        private DateTime _dtCookingStartEstimated;   // ожидаемое начало приготовления
        private TimeSpan _tsCookingEstimated;   // время приготовления
        // накопительные счетчики нахождения в конкретном состоянии
        private Dictionary<OrderStatusEnum, IncrementalTimer> _tsTimersDict; // словарь накопительных счетчиков для различных состояний
        private IncrementalTimer _curTimer;  // текущий таймер для выдачи клиенту значения таймера

        // записи БД для сохранения блюда
        private OrderDishRunTime _dbRunTimeRecord = null;         // запись дат/времени прямого пути 
        private string _serviceErrorMessage;
        #endregion

        #region Properties
        // форматированное представление временного промежутка для внешних клиентов
        [DataMember]
        public string WaitingTimerString {
            get {
                string retVal = null;
                if (_curTimer != null)
                {
                    TimeSpan tsTimerValue = _curTimer.ValueTS;

                    // если для состояния приготовления есть время приготовления, то рассчитываем отображаемое значение
                    if ((Status == OrderStatusEnum.Cooking) && (EstimatedTime != 0))
                    {
                        tsTimerValue = _tsCookingEstimated - tsTimerValue;
                    }

                    retVal = (tsTimerValue.TotalDays > 0d) ? tsTimerValue.ToString(@"d\.hh\:mm\:ss") : tsTimerValue.ToString(@"hh\:mm\:ss");
                    // отрицательное время
                    if (tsTimerValue.Ticks < 0) retVal = "-" + retVal;
                }
                return retVal;
            }
            set { }  // необходимо для DataMember
        }

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

        public int EstimatedTime { get; set; }

        [DataMember]
        public OrderStatusEnum Status { get; set; }

        private DepartmentModel _department;
        [DataMember]
        public DepartmentModel Department
        {
            get { return _department; }
            set { }
        }

        // время (в сек) "Готовить позже"
        // клентам нет смысла передавать
        public int DelayedStartTime { get; set; }

        [DataMember]
        public string ServiceErrorMessage { get { return _serviceErrorMessage; } set { } }

        #endregion

        // ctor
        // ДЛЯ НОВОГО БЛЮДА
        public OrderDishModel(OrderDish dbDish)
        {
            UpdateFromDBEntity(dbDish, true);

            // ожидаемое время начала приготовления для автоматического перехода в состояние приготовления
            _dtCookingStartEstimated = CreateDate.AddSeconds(DelayedStartTime);
            _tsCookingEstimated = (EstimatedTime == 0) ? TimeSpan.Zero : TimeSpan.FromSeconds(EstimatedTime);

            // создать словарь накопительных счетчиков
            // таймер ожидания начала приготовления
            _tsTimersDict.Add(OrderStatusEnum.WaitingCook, new IncrementalTimer(500));
            // таймер времени приготовления
            _tsTimersDict.Add(OrderStatusEnum.Cooking, new IncrementalTimer(500));
            // таймер времени ожидания выдачи, нахождение в состоянии Готов
            _tsTimersDict.Add(OrderStatusEnum.Ready, new IncrementalTimer(500));
            // таймер времени ожидания фиксации заказа, нахождение в состоянии Выдано
            _tsTimersDict.Add(OrderStatusEnum.Took, new IncrementalTimer(500));

            // получить запись из таблицы состояний
            _dbRunTimeRecord = getDBRunTimeRecord(dbDish.Id);
        }

        private OrderDishRunTime getDBRunTimeRecord(int id)
        {
            OrderDishRunTime runtimeRecord = null;
            using (KDSEntities db = new KDSEntities())
            {
                runtimeRecord = db.OrderDishRunTime.FirstOrDefault(rec => rec.OrderDishId == id);
                if (runtimeRecord == null)
                {
                    runtimeRecord = new OrderDishRunTime() { OrderDishId = id};
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

        // новое блюдо или обновить существующее из БД
        internal void UpdateFromDBEntity(OrderDish dbDish, bool isNew)
        {
            lock (this)
            {
                #region simple fields
                if (isNew)
                {
                    Id = dbDish.Id; Uid = dbDish.UID;
                    CreateDate = dbDish.CreateDate;
                    Name = dbDish.DishName;
                    FilingNumber = dbDish.FilingNumber;
                    ParentUid = dbDish.ParentUid;
                    Comment = dbDish.Comment;
                    Quantity = dbDish.Quantity;
                    DelayedStartTime = dbDish.DelayedStartTime;
                    EstimatedTime = dbDish.EstimatedTime;
                    Status = AppLib.GetStatusEnumFromNullableInt(dbDish.DishStatusId);
                }
                else
                {
                    if (Uid.IsNull() || (Uid != dbDish.UID)) Uid = dbDish.UID;
                    if (CreateDate != dbDish.CreateDate) CreateDate = dbDish.CreateDate;
                    if (Name.IsNull() || (Name != dbDish.DishName)) Name = dbDish.DishName;
                    if (FilingNumber != dbDish.FilingNumber) FilingNumber = dbDish.FilingNumber;
                    if (ParentUid.IsNull() || (ParentUid != dbDish.ParentUid)) ParentUid = dbDish.ParentUid;
                    if (Comment.IsNull() || (Comment != dbDish.Comment)) Comment = dbDish.Comment;
                    if (Quantity != dbDish.Quantity) Quantity = dbDish.Quantity;
                    if (EstimatedTime != dbDish.EstimatedTime) EstimatedTime = dbDish.EstimatedTime;
                }
                #endregion

                // статус блюда
                OrderStatusEnum newStatus = AppLib.GetStatusEnumFromNullableInt(dbDish.DishStatusId);
                
                if (newStatus <= OrderStatusEnum.WaitingCook)
                {
                    // проверяем условие автоматического перехода в режим приготовления
                    if (canAutoPassToCookingStatus()) newStatus = OrderStatusEnum.Cooking;
                }

                bool isNewStatus = (newStatus != Status);     // статус изменен

                // если поменялся отдел
                if (_department.Id != dbDish.DepartmentId)
                {
                    // необходимо обновить статус
                    isNewStatus = true;
                    // сохранить в поле
                    _department = ServiceDics.Departments.GetDepartmentById(dbDish.DepartmentId);
                    if (_department == null) _department = new DepartmentModel();
                    // группы отделов
                    foreach (DepartmentDepartmentGroup ddg in dbDish.Department.DepartmentDepartmentGroup)
                    {
                        _department.DepGroups.Add(ServiceDics.DepGroups.GetDepGroupById(ddg.DepartmentGroupId));
                    }
                }

                if (isNewStatus) UpdateStatus(newStatus);

            }  // lock

        }  // method


        // ******************************************
        //    ОСНОВНАЯ ПРОЦЕДУРА БИЗНЕС-ЛОГИКИ
        //    ПРИ ИЗМЕНЕНИИ СТАТУСА БЛЮДА.
        //   Блюдо. Самостоятельное или ингредиент (подчиненное блюдо)
        // ******************************************
        // команды на изменение статуса блюда могут приходить как от КДС, так и из FrontOffice (при чтении из БД)
        // состояния и даты сохраняются в БД при каждом изменении
        public void UpdateStatus(OrderStatusEnum newStatus)
        {
            if (this.Status == newStatus) return; // если статус не поменялся, то ничего не делать

            // дата входа в НОВОЕ состояние
            DateTime dtEnterToNewStatus = DateTime.Now;
            // время нахождения в ПРЕДЫДУЩЕМ состоянии
            TimeSpan tsStoodInPrevState = TimeSpan.Zero;
            if (_curTimer != null)   // если есть таймер предыдущего состояния
            {
                _curTimer.Stop(); // остановить таймер состояния
                // получить время нахождения в состоянии с момента последнего входа
                tsStoodInPrevState = _curTimer.IncrementTS;
            }
            // запись или в RunTimeRecord или в ReturnTable
            writeStatusEnterEventToDB(Status, newStatus, dtEnterToNewStatus, tsStoodInPrevState);

            // запуск таймера для нового состояния, чтобы клиент мог получить значение таймера
            _curTimer = null;
            if (_tsTimersDict.ContainsKey(newStatus))
            {
                _curTimer = _tsTimersDict[newStatus];
                _curTimer.Start();
            }
            // сохранить новый статус в объекте
            Status = newStatus;
        }  // method UpdateStatus

        // для предыдущего состояния сохранить время нахождения в этом состоянии
        // для текущего состояния сохранить дату входа в это состояние
        private void writeStatusEnterEventToDB(OrderStatusEnum prevStatus, OrderStatusEnum newStatus, DateTime dtEnterToNewStatus, TimeSpan tsStoodInPrevState)
        {
            if (_dbRunTimeRecord == null) return;

            // пустое ли поле в RunTimeRecord для даты входа текущего статуса?
            if (runTimeStatusEnterDate(newStatus, true, dtEnterToNewStatus) == true)  // возв. true, если поле == null
            {
                // сохраняем дату входа в новое состояние
                runTimeStatusEnterDate(newStatus, false, dtEnterToNewStatus);
                // сохраняем в записи RunTimeRecord время нахождения в предыдущем состоянии
                setRunTimeStatusTimeSpan(prevStatus, tsStoodInPrevState.ToIntSec());

                // приаттачить и сохранить в DB-контексте два поля из RunTimeRecord
                using (KDSEntities db = new KDSEntities())
                {
                    try
                    {
                        db.OrderDishRunTime.Attach(_dbRunTimeRecord);
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

            // создать новую запись в Return table
            else
            {
                using (KDSEntities db = new KDSEntities())
                {
                    db.OrderDishReturnTime.Add(new OrderDishReturnTime()
                    {
                        OrderDishId = this.Id, ReturnDate = dtEnterToNewStatus,
                        StatusFrom = (int)prevStatus, StatusFromTimeSpan = tsStoodInPrevState.ToIntSec(),
                        StatusTo = (int)newStatus
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
        }  // method


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

        private int getRunTimeTSByStatus(OrderStatusEnum status)
        {
            int retVal = 0;
            switch (status)
            {
                case OrderStatusEnum.None:
                    break;
                case OrderStatusEnum.WaitingCook:
                    retVal = Convert.ToInt32(_dbRunTimeRecord.WaitingCookTS);
                    break;
                case OrderStatusEnum.Cooking:
                    retVal = Convert.ToInt32(_dbRunTimeRecord.CookingTS);
                    break;
                case OrderStatusEnum.Ready:
                    retVal = Convert.ToInt32(_dbRunTimeRecord.WaitingTakeTS);
                    break;
                case OrderStatusEnum.Took:
                    retVal = Convert.ToInt32(_dbRunTimeRecord.WaitingCommitTS);
                    break;
                case OrderStatusEnum.Cancelled:
                    break;
                case OrderStatusEnum.Commit:
                    break;
                default:
                    break;
            }

            return retVal;
        }


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
            if (_tsTimersDict != null)
            {
                foreach (var item in _tsTimersDict) item.Value.Dispose();
            }
        }
    }  // class OrderDishModel

}
