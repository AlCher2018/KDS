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

        public string UpdateOrders()
        {
            KDSEntities db = null;
            List<Order> dbOrders = null;
            try
            {
                db = new KDSEntities();
                // в запрос включить блюда, отделы и группы отделов
                dbOrders = db.Order
                    .Include("OrderDish")
                    .Include("OrderDish.Department")
                    .Include("OrderDish.Department.DepartmentDepartmentGroup")
                    .Where(o => (o.OrderStatusId < 2))
                    .ToList();
            }
            catch (Exception ex)
            {
                return ex.Message;
            }

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
                    _orders.Add(dbOrder.Id, new OrderModel(dbOrder));
                }
            }
            // ключи для удаления
            IEnumerable<int> delKeys = _orders.Keys.Except(dbOrders.Select(o => o.Id));
            foreach (int key in delKeys) _orders.Remove(key);

            if (db != null) db.Dispose();
            return null;
        }

    }

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

        private Timer _timer;

        // ctor
        public OrderDishModel(OrderDish dbDish)
        {
            _timer = new Timer();
            UpdateFromDBEntity(dbDish);
        }

        internal void UpdateFromDBEntity(OrderDish dbDish)
        {
            Id = dbDish.Id; Uid = dbDish.UID;
            CreateDate = dbDish.CreateDate;
            Name = dbDish.DishName;
            Quantity = dbDish.Quantity;
            Status = (OrderStatusEnum)Enum.Parse(typeof(OrderStatusEnum), dbDish.DishStatusId.ToString());

            // отдел
            _department = ServiceDics.Departments.GetDepartmentById(dbDish.DepartmentId);
            if (_department == null) _department = new DepartmentModel();
            else _department.DepGroups.Clear();
            // группы отделов
            foreach (DepartmentDepartmentGroup ddg in dbDish.Department.DepartmentDepartmentGroup)
            {
                _department.DepGroups.Add(ServiceDics.DepGroups.GetDepGroupById(ddg.DepartmentGroupId));
            }
            
        }

    }  // class OrderDishModel

}
