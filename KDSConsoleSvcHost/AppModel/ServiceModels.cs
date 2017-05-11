using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using KDSConsoleSvcHost;
using KDSService.Lib;

namespace KDSService.AppModel
{

    // основной класс службы
    public class OrdersModel
    {
        private Dictionary<int,OrderModel> _orders;
        public Dictionary<int, OrderModel> Orders { get { return _orders; } }

        private string _errorMsg;

        public OrdersModel()
        {
            _orders = new Dictionary<int, OrderModel>();
        }

        //**************************************
        // ГЛАВНАЯ ПРОЦЕДУРА ОБНОВЛЕНИЯ ЗАКАЗОВ
        //**************************************
        public string UpdateOrders()
        {
            // получить заказы из БД
            List<Order> dbOrders = null;
            try
            {
                using (KDSEntities db = new KDSEntities())
                {
                    // в запрос включить блюда, отделы и группы отделов
                    // отсортированные по порядке появления в таблице
                    dbOrders = db.Order
                        .Include("OrderDish")
                        .Include("OrderDish.Department")
                        .Include("OrderDish.Department.DepartmentDepartmentGroup")
                        .Where(o => (o.OrderStatusId < 2))
                        .OrderBy(o => o.Id)
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }

            // цикл по полученным из БД заказам
            if (dbOrders != null)
            {
                lock (dbOrders)
                {
                    // сохранить в свойствах приложения словарь блюд с их количеством, 
                    // которые ожидают готовки или уже готовятся
                    Dictionary<int, decimal> dishesQty = getDishesQty(dbOrders);
                    AppEnv.SetAppProperty("dishesQty", dishesQty);

                    OrderModel curOrder;
                    foreach (Order dbOrder in dbOrders)
                    {
                        if (_orders.ContainsKey(dbOrder.Id))
                        {
                            // обновить существующий заказ
                            curOrder = _orders[dbOrder.Id];
                            curOrder.UpdateFromDBEntity(dbOrder);
                        }
                        else
                        {
                            // добавление заказа в словарь
                            curOrder = new OrderModel(dbOrder);
                            _orders.Add(dbOrder.Id, curOrder);
                        }
                        //curOrder
                    }
                    // ключи для удаления
                    IEnumerable<int> delKeys = _orders.Keys.Except(dbOrders.Select(o => o.Id));
                    foreach (int key in delKeys) _orders.Remove(key);
                }  // lock
            }

            return null;
        }  // method

        private Dictionary<int, decimal> getDishesQty(List<Order> dbOrders)
        {
            Dictionary<int, decimal> retVal = new Dictionary<int, decimal>();
            foreach (Order order in dbOrders)
            {
                foreach (OrderDish dish in order.OrderDish)
                {
                    if (retVal.ContainsKey(dish.Id))
                        retVal[dish.Id] += dish.Quantity;
                    else
                        retVal.Add(dish.Id, dish.Quantity);
                }
            }

            return (retVal.Count == 0) ? null : retVal;
        }  // method

    }  // class


    [DataContract]
    public class OrderModel
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

        private Dictionary<int,OrderDishModel> _dishes;
        [DataMember]
        public Dictionary<int, OrderDishModel> Dishes
        {
            get { return _dishes; }
            set { }
        }

        private Timer _timer;

        // ctor
        public OrderModel(Order order)
        {
            _timer = new Timer();

            _dishes = new Dictionary<int, OrderDishModel>();
            UpdateFromDBEntity(order);
        }

        // translate Order to OrderSvcModel
        public void UpdateFromDBEntity(Order dbOrder)
        {
            Id = dbOrder.Id; Number = dbOrder.Number;
            Uid = dbOrder.UID; CreateDate = dbOrder.CreateDate;
            HallName = dbOrder.RoomNumber; TableName = dbOrder.TableNumber;
            Waiter = dbOrder.Waiter;

            Status = (OrderStatusEnum)Enum.Parse(typeof(OrderStatusEnum), dbOrder.OrderStatusId.ToString());

            _dishes.Clear();
            foreach (OrderDishModel dish in dbOrder.OrderDish.Select(od => new OrderDishModel(od)))
            {
                _dishes.Add(dish.Id, dish);
            }
        }

    }  // class OrderSvcModel


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
        // поля для фиксации времени между сменой статусов
        //private DateTime _dtFrom, _dtTo;  // дата 
        // поля дат состояний и временных промежутков
        private DateTime _dtCookingStartEstimated;   // ожидаемое начало приготовления
        private DateTime _dtCookingStart;   // конкретное начало приготовления
        private DateTime _dtReady;          // окончание приготовления
        private DateTime _dtTake;           // выдача
        private DateTime _dtReturn;         // возврат
        private DateTime _dtCancel;         // отмена
        private DateTime _dtCommit;         // фиксация
        private TimeSpan _tsWaitingCook;    // время ожидания пригтовления
        private TimeSpan _tsCooking;        // время приготовления
        private TimeSpan _tsWaitingTake;    // время ожидания выдачи
        private TimeSpan _tsWaitingCommit;  // время ожидания фиксации заказа

        // таймер ожидания смены статуса или нахождения в состоянии
        private TimeSpan _tsTimerBase;      // базовое значение врем.пром., для накопительных значений после возврата
        private DateTime _dtTimerFrom;      // дата начала временного промежутка
        private TimeSpan _tsTimerIncr;      // приращение таймера
        private TimeSpan _tsTimerValue;     // временной промежуток, рассчитываемый в таймере по формуле
                                            //  _tsTimerBase + Now - _dtTempTSFrom
        private Timer _timer;               // таймер, изменяющий временный промежуток

        // записи БД для сохранения блюда
        private OrderDish _dbDish = null;                   // запись блюда
        private OrderDishRunTime _dbRunTime = null;         // запись дат/времени прямого пути 
        private OrderDishReturnTime _dbReturnTime = null;   // запись дат/времени возвратного цикла
        private string _serviceErrorMessage;
        #endregion

        #region Properties
        // форматированное представление временного промежутка для внешних клиентов
        [DataMember]
        public string WaitingTimerString {
            get {
                string retVal = null;
                if (_tsTimerValue.IsZero() == false)
                {
                    retVal = (_tsTimerValue.TotalDays > 0d) ? _tsTimerValue.ToString(@"d\.hh\:mm\:ss") : _tsTimerValue.ToString(@"hh\:mm\:ss");
                    // отрицательное время
                    if (_tsTimerValue.Ticks < 0) retVal = "-" + retVal;
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
            _dtTimerFrom.SetZero(); _tsTimerValue = TimeSpan.Zero;
            _tsTimerBase = new TimeSpan(0);

            _timer = new Timer(500);
            _timer.Elapsed += _timer_Elapsed;

            // ожидаемое время начала приготовления для автоматического пуска счетчика ожидания
            _dtCookingStartEstimated = CreateDate.AddSeconds(DelayedStartTime); 

            UpdateFromDBEntity(dbDish, true);
        }

        #region внутренний счетчик
        private void startTimer()
        {
            if (_timer == null) return;
            // если таймер запущен, то останавливаем его
            if (_timer.Enabled == true) _timer.Stop();
            _timer.Start();
        }
        private void stopTimer()
        {
            if (_timer == null) return;
            if (_timer.Enabled == true) _timer.Stop();
        }
        // должна быть установлена начальная дата для счетчика !!!
        private void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (_dtTimerFrom.IsZero() == false)
            {
                _tsTimerIncr = (DateTime.Now - _dtTimerFrom);
                _tsTimerValue = _tsTimerBase + _tsTimerIncr;
            }
        }
        #endregion

        // новое блюдо или обновить существующее из БД
        internal void UpdateFromDBEntity(OrderDish dbDish, bool isNew = false)
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
            }
            else
            {
                if (Id != dbDish.Id) Id = dbDish.Id;
                if (Uid.IsNull() || (Uid != dbDish.UID)) Uid = dbDish.UID;
                if (CreateDate != dbDish.CreateDate) CreateDate = dbDish.CreateDate;
                if (Name.IsNull() || (Name != dbDish.DishName)) Name = dbDish.DishName;
                if (FilingNumber != dbDish.FilingNumber) FilingNumber = dbDish.FilingNumber;
                if (ParentUid.IsNull() || (ParentUid != dbDish.ParentUid)) ParentUid = dbDish.ParentUid;
                if (Comment.IsNull() || (Comment != dbDish.Comment)) Comment = dbDish.Comment;
                if (Quantity != dbDish.Quantity) Quantity = dbDish.Quantity;
            }
            #endregion

            // статус блюда
            OrderStatusEnum newStatus = (OrderStatusEnum)Enum.Parse(typeof(OrderStatusEnum), dbDish.DishStatusId.ToString());
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
            switch (newStatus)
            {
                // ожидание начала приготовления блюда
                case OrderStatusEnum.WaitingCook:
                    // автоматический старт внутреннего счетчика для ОЖИДАНИЯ ПРИГОТОВЛЕНИЯ
                    if ((_department.IsAutoStart == true) && _tsTimerValue.IsZero())   // счетчик еще не запущен
                    {
                        if (checkStartWaitingCookTimer() == true)
                        {
                            _dtTimerFrom = _dtCookingStartEstimated;
                            startTimer();
                        }
                    }
                    break;

                // переход в состояние "В ПРОЦЕССЕ"
                case OrderStatusEnum.Cooking:
                    // нормальный переход из состояния "Ожидание приготовления"
                    // здесь создаем запись прямого пути
                    if (Status == OrderStatusEnum.WaitingCook)
                    {
                        // остановить счетчик WaitingCook
                        stopTimer();
                        // дата входа в состояние
                        _dtCookingStart = DateTime.Now;
                        // время нахождения в предыдущем состоянии
                        _tsWaitingCook = _dtCookingStart - _dtCookingStartEstimated;
                        // создать новую запись в БД и держать ее в поле
                        saveRunTimeWaitingCookTS();
                        // запустить счетчик Cooking для прямого пути
                        _dtTimerFrom = _dtCookingStart; _tsTimerBase.SetZero();
                        startTimer();
                    }
                    // возврат из последующих состояний (ошибка повара или решение менеджера)
                    // здесь создаем запись возвратного цикла
                    else if ((Status == OrderStatusEnum.Ready) || (Status == OrderStatusEnum.Took))
                    {
                        #region return event
                        // дата входа в состояние
                        //_dtReturn = DateTime.Now;
                        //// создаем запись возвратного цикла
                        //_dbReturnTime = new OrderDishReturnTime()
                        //{
                        //    OrderDishId = this.Id, StatusFrom = (int)Status, ReturnDate = _dtReturn
                        //};
                        //// возврат из состояния Готов
                        //if (Status == OrderStatusEnum.Ready)  
                        //{
                        //    // остановить счетчик WaitingTake
                        //    stopTimer();
                        //    //if (_dbRunTime)
                        //    // время нахождения в предыдущем состоянии
                        //    _tsWaitingTake = _dtReturn - _dtReady;
                        //    // обновить запись блюда или добавить запись в табл.возвратов
                        //    saveRunTimeReturnFromReady();
                        //    // запустить счетчик нахождения в текущем состоянии
                        //    _dtTimerFrom = _dtCookingStart;
                        //}
                        //else
                        //{
                        //    // время нахождения в предыдущем состоянии
                        //    _tsWaitingCook = _dtCookingStart - _dtCookingStartEstimated;
                        //    // создать новую запись в БД и держать ее в поле
                        //    saveRunTimeWaitingCookTS();
                        //    // запустить счетчик нахождения в текущем состоянии
                        //    _dtTimerFrom = _dtCookingStart;
                        //}
                        //startTimer();
                        #endregion
                    }
                    break;

                // состояние блюдо/заказ ГОТОВО
                case OrderStatusEnum.Ready:
                    // остановить счетчик Cooking
                    stopTimer();
                    // дата входа в состояние
                    _dtReady = DateTime.Now;
                    // время нахождения в предыдущем состоянии
                    _tsCooking = _tsTimerValue;  // накопительное значение с возвратными циклами
                    // обновить запись в БД
                    saveRunTimeCookingTS();
                    // запустить счетчик нахождения в текущем состоянии
                    _dtTimerFrom = _dtCookingStart;
                    startTimer();
                    break;

                case OrderStatusEnum.Took:
                    break;

                case OrderStatusEnum.Cancelled:
                    break;

                case OrderStatusEnum.Commit:
                    break;

                default:
                    break;
            }

            // сохранить новый статус в объекте
            if (newStatus != Status) Status = newStatus;
        }  // method UpdateStatus

        // сохранить время приготовления _tsCooking
        private bool saveRunTimeCookingTS()
        {
            bool retVal = false;

            // если в записи блюда поле CookingTS пустое, то это прямой путь и записываем в это поле
            if (isDBIntZero(_dbRunTime.CookingTS)) _dbRunTime.CookingTS = _tsCooking.ToIntSec();
            // иначе записываем в возвратную запись приращение времени
            else _dbReturnTime.CookingTS = _tsTimerIncr.ToIntSec();

            try
            {
                saveDB();
                retVal = true;
            }
            catch (Exception ex)
            {
                _serviceErrorMessage = "Ошибка сохранения записи в БД";
                AppEnv.WriteLogErrorMessage("Error save dish (id {0}) to DB: {1}", this.Id, ex.Message);
            }
            return retVal;
        }

        // сохранение поля _tsWaitingTake при возврате из Ready to Cooking 
        private bool saveRunTimeReturnFromReady()
        {
            bool retVal = false;
            // если в строке блюда поле пустое, то сохраняем в него, иначе
            if (isDBIntZero(_dbRunTime.WaitingTakeTS))
                _dbRunTime.WaitingTakeTS = (int)_tsWaitingTake.TotalSeconds;

            try
            {
                saveDB();
                retVal = true;
            }
            catch (Exception ex)
            {
                _serviceErrorMessage = "Ошибка сохранения записи в БД";
                AppEnv.WriteLogErrorMessage("Error save dish (id {0}) to DB: {1}", this.Id, ex.Message);
            }
            return retVal;
        }  // method

        // вернуть признак того, что int из поля БД пустой или нулевой
        private bool isDBIntZero(int? dbValue)
        {
            return (Convert.ToInt32(dbValue) == 0);
        }

        // сохранить в БД время ожидания готовки: создать запись в таблице и хранить ее в локальном поле
        private bool saveRunTimeWaitingCookTS()
        {
            bool retVal = false;
            _dbRunTime = new OrderDishRunTime()
            {
                OrderDishId = this.Id, CookingStartEstimatedDate = _dtCookingStartEstimated,
                WaitingCookTS = (int)_tsWaitingCook.TotalSeconds
            };

            using (KDSEntities db = new KDSEntities())
            {
                db.OrderDishRunTime.Add(_dbRunTime);
                try
                {
                    db.SaveChanges();
                    retVal = true;
                }
                catch (Exception ex)
                {
                    _serviceErrorMessage = "Ошибка сохранения записи в БД";
                    AppEnv.WriteLogErrorMessage("Error save dish (id {0}) to DB: {1}", this.Id, ex.Message);
                }
            }
            return retVal;
        }  // method

        private void saveDB()
        {
            using (KDSEntities db = new KDSEntities())
            {
                try
                {
                    db.SaveChanges();
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }  // method saveDB

        // проверка возможности АВТОМАТИЧЕСКОГО запуска таймера ожидания готовки
        private bool checkStartWaitingCookTimer()
        {
            // 1. таймер тикаем, если текущая дата больше даты ожидаемого времени начала приготовления
            bool retVal = (DateTime.Now >= _dtCookingStartEstimated);

            // 2. проверяем общее кол-во такого блюда в заказах, если установлено кол-во одновременно готовящихся блюд
            if (retVal == false)
            {
                Dictionary<int, decimal> dishesQty = (Dictionary<int, decimal>)AppEnv.GetAppProperty("dishesQty");
                if (dishesQty != null)
                    retVal = (dishesQty[this.Id] < _department.DishQuantity);
                else
                    retVal = true;
            }

            return retVal;
        }

        public void Dispose()
        {
            if (_timer != null) { _timer.Stop(); _timer.Dispose(); _timer = null; }
        }
    }  // class OrderDishModel

}
