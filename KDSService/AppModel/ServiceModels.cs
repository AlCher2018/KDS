using KDSService.DataSource;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace KDSService.AppModel
{

    // основной класс службы
    internal class OrdersSvcModel
    {
        private Dictionary<int,OrderSvcModel> _orders;

        private string _errorMsg;

        public OrdersSvcModel()
        {
            _orders = new Dictionary<int, OrderSvcModel>();
        }

        public void UpdateOrders()
        {
            DBContext db = new DBContext();
            // в запрос включить блюда, отделы и группы отделов
            IEnumerable<Order> dbOrders = db.Order.
                Include("Order.OrderDish").
                Include("Order.OrderDish.Department").
                Include("Order.OrderDish.Department.DepartmentDepartmentGroup").
                Where(o => (o.OrderStatusId < 2));

            OrderSvcModel curOrder;
            foreach (Order dbOrder in dbOrders)
            {
                if (_orders.ContainsKey(dbOrder.Id))
                {
                    // обновить существующий заказ
                    curOrder = _orders[dbOrder.Id];
                    curOrder.UpdateOrderFromDB(dbOrder);
                }
                else
                {
                    // добавление заказа в словарь
                    _orders.Add(dbOrder.Id, new OrderSvcModel(dbOrder));
                }
            }
            // ключи для удаления
            IEnumerable<int> delKeys = _orders.Keys.Except(dbOrders.Select(o => o.Id));
            foreach (int key in delKeys) _orders.Remove(key);
        }

        public OrdersCltModel GetClientInstance()
        {
            OrdersCltModel retVal = new OrdersCltModel();
            retVal.ErrorMsg = _errorMsg;
            retVal.Orders = _orders.Values.Select(sModel => sModel.GetClientInstance()).ToArray();

            return retVal;
        }
    }

    internal class OrderSvcModel
    {
        public int Id { get; set; }
        public int Number { get; set; }
        public string Uid { get; set; }
        public DateTime CreateDate { get; set; }
        public string HallName { get; set; }
        public string TableName { get; set; }
        public string Waiter { get; set; }
        public OrderStatusEnum Status { get; set; }

        private Dictionary<int,OrderDishSvc> _dishes;
        public Dictionary<int, OrderDishSvc> Dishes { get { return _dishes; } }

        private Timer _timer;

        // ctor
        public OrderSvcModel(Order order)
        {
            _timer = new Timer();

            _dishes = new Dictionary<int, OrderDishSvc>();
            UpdateFromDBEntity(order);
        }

        // translate Order to OrderSvcModel
        public void UpdateFromDBEntity(Order dbOrder)
        {
            Id = dbOrder.Id; Number = dbOrder.Number;
            Uid = dbOrder.UID; CreateDate = dbOrder.CreateDate;
            HallName = dbOrder.RoomNumber; TableName = dbOrder.TableNumber;
            Waiter = dbOrder.Waiter;

            Status = (OrderStatusEnum)Enum.Parse(typeof(OrderStatusEnum), dbOrder.OrderStatus.ToString());

            _dishes.Clear();
            foreach (OrderDishSvc dish in dbOrder.OrderDish.Select(od => new OrderDishSvc(od)))
            {
                _dishes.Add(dish.Id, dish);
            }
        }

        public OrderCltModel GetClientInstance()
        {
            OrderCltModel retVal = new OrderCltModel()
            {
                Id = _id,
                OrderNumber = _number,
                HallName = _dbOrder.RoomNumber,
                TableName = _dbOrder.TableNumber,
                Status = _dbOrder.OrderStatusId,
                UID = _dbOrder.UID,
                Waiter = _dbOrder.Waiter
            };
            retVal.Dishes = _dishes.Select(sModel => sModel.GetClientInstance()).ToArray();

            return retVal;
        }

    }  // class OrderSvcModel


    /// <summary>
    /// *** Class OrderDishSvc
    /// </summary>
    [DataContract]
    internal class OrderDishSvc
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

        private Department _department;
        public Department Department { get { retur} }

        private Timer _timer;

        // ctor
        internal OrderDishSvc(OrderDish dbDish)
        {
            _timer = new Timer();

            UpdateFromDBEntity(dbDish);
        }

        internal void UpdateFromDBEntity(KDSService.DataSource.OrderDish dbDish)
        {
            Id = dbDish.Id; Uid = dbDish.UID;
            CreateDate = dbDish.CreateDate;
            Name = dbDish.DishName;
            Quantity = dbDish.Quantity;
            Status = (OrderStatusEnum)Enum.Parse(typeof(OrderStatusEnum), dbDish.DishStatusId.ToString());

            // отдел
            _department = ServiceDics.GetDepartmentById(dbDish.DepartmentId);
            _department.DepGroups.Clear();
            // группы отделов
            foreach (KDSService.DataSource.DepartmentDepartmentGroup ddg in dbDish.Department.DepartmentDepartmentGroup)
            {
                _department.DepGroups.Add(ServiceDics.GetDeGroupById(ddg.DepartmentGroupId));
            }
            
        }

    }  // class OrderDishSvcModel

}
