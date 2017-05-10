using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using KDSConsoleSvcHost;


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
                        curOrder
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
    /// *** Class OrderDishSvc
    /// </summary>
    [DataContract]
    public class OrderDishModel
    {
        #region Fields
        // поля для фиксации времени между сменой статусов
        private DateTime _dtFrom, _dtTo;  // дата 
        // поля дат входа в состояние
        private DateTime _dtStartCook;
        // и отображения таймера ожидания смены статуса
        private string _strTimer;
        #endregion

        #region Properties
        [DataMember]
        public string WaitTimer { get { return _strTimer; } }

        [DataMember]
        public int Id { get; set; }

        [DataMember]
        public string Uid { get; set; }

        [DataMember]
        public DateTime CreateDate { get; set; }

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public decimal Quantity { get; set; }

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
        #endregion

        // ctor
        public OrderDishModel(OrderDish dbDish)
        {
            UpdateFromDBEntity(dbDish);

            // ДЛЯ НОВОГО БЛЮДА
            // дата создания блюда со сдвигом "готовить позже"
            _dtFrom = CreateDate.AddSeconds(DelayedStartTime);
        }

        // новое блюдо или обновить существующее из БД
        internal void UpdateFromDBEntity(OrderDish dbDish)
        {
            Id = dbDish.Id; Uid = dbDish.UID;
            CreateDate = dbDish.CreateDate;
            Name = dbDish.DishName;
            Quantity = dbDish.Quantity;
            DelayedStartTime = dbDish.DelayedStartTime;

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

        // команды на изменение статуса блюда могут приходить как от КДС, так и из FrontOffice (при чтении из БД)
        public void UpdateStatus(OrderStatusEnum newStatus)
        {
            DateTime dtNow = DateTime.Now;
            // промежуток времени, который будет отображаться на КДС
            TimeSpan ts = TimeSpan.Zero;   

            // ожидание начала приготовления блюда
            if (newStatus == OrderStatusEnum.Wait)
            {
                // автоматический старт счетчика
                if ((_department.IsAutoStart == true) && (checkStartTimer(dtNow) == true))
                    ts = dtNow - _dtFrom;
                // ручной запуск готовки: таймер не отображаем
//                else

                // форматированный промежуток времени
                if (ts.Equals(TimeSpan.Zero) == false)
                {
                    _strTimer = (ts.TotalDays > 0d) ? ts.ToString("d.hh:mm:ss") : ts.ToString("hh:mm:ss");
                }
            }  // if (newStatus == OrderStatusEnum.Wait)

            // переход в состояние "В ПРОЦЕССЕ"
            else if (newStatus == OrderStatusEnum.InProcess)
            {
                // сохранить в БД время ожидания готовки

            }

        }  // method UpdateStatus

        // проверка возможности запуска таймера ожидания готовки
        private bool checkStartTimer(DateTime dtNow)
        {
            // 1. таймер тикаем, если текущее время больше времени 
            bool retVal = (dtNow > _dtFrom);

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


    }  // class OrderDishModel

}
