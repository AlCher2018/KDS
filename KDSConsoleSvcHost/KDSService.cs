using KDSConsoleSvcHost;
using KDSService.AppModel;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Timers;

namespace KDSService
{
    /// <summary>
    /// 1. Периодический опрос заказов из БД
    /// </summary>

    [ServiceBehavior(IncludeExceptionDetailInFaults = true, 
        InstanceContextMode = InstanceContextMode.Single)]
    public class KDSServiceClass : IDisposable, IKDSService, IKDSCommandService
    {
        private const double _ObserveTimerInterval = 500;

        // заказы на стороне службы (с таймерами)
        private OrdersModel _ordersModel;  
        // таймер наблюдения за заказами в БД
        private Timer _observeTimer;


        public KDSServiceClass()
        {
            string msg = "**** Создание служебного класса KDSService ****";
            Console.WriteLine(msg);
            AppEnv.WriteLogInfoMessage(msg);

            _observeTimer = new Timer(_ObserveTimerInterval);
            _observeTimer.Elapsed += _observeTimer_Elapsed;

            _ordersModel = new OrdersModel();
            startService();
        }

        private void startService()
        {
            _observeTimer.Start();
        }
        private void stopService()
        {
            _observeTimer.Stop();
        }


        // периодический просмотр заказов
        private void _observeTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _observeTimer.Stop();
            //Console.WriteLine("  update Orders");

            string errMsg = _ordersModel.UpdateOrders();
            if (errMsg != null) AppEnv.WriteLogErrorMessage(errMsg);

            if (_observeTimer != null) _observeTimer.Start();
        }


        // ****  SERVICE CONTRACT  *****
        #region IKDSService inplementation

        public List<OrderStatusModel> GetOrderStatusList()
        {
            string errMsg = null;
            List<OrderStatusModel>  retVal = ServiceDics.GetOrderStatusList(out errMsg);

            if (retVal == null) AppEnv.WriteLogErrorMessage(errMsg);
            return retVal;
        }

        public Dictionary<int, DepartmentGroupModel> GetDepartmentGroups()
        {
            //OperationContext context = OperationContext.Current;
            //if (context != null && context.RequestContext != null)
            //{
            //    Message msg = context.RequestContext.RequestMessage;
            //    Console.WriteLine(msg.ToString());
            //}

            return ServiceDics.DepGroups.GetDictionary();
        }

        public Dictionary<int, DepartmentModel> GetDepartments()
        {
            return ServiceDics.Departments.GetDictionary();
        }

        // все заказы
        public List<OrderModel> GetOrders()
        {
            List<OrderModel> retVal = new List<OrderModel>();
            retVal.AddRange(_ordersModel.Orders.Values);

            return retVal;
        }
        // по состоянию какого-либо блюда в заказе, отбор ТОЛЬКО блюд с указанным статусом!!
        public List<OrderModel> GetOrdersByConditions(OrderStatusEnum dishStatus = OrderStatusEnum.None, int departmentId = 0, int departmentGroupId = 0)
        {
            List<OrderModel> retVal = new List<OrderModel>();
            OrderModel curOrder = null;
            lock (_ordersModel)
            {
                foreach (OrderModel ord in _ordersModel.Orders.Values)
                {
                    // отобрать блюда по условию
                    Dictionary<int, OrderDishModel> filteredDishes = null;
                    foreach (OrderDishModel dish in ord.Dishes.Values)
                    {
                        bool selected = false;
                        if ((selected == false) && (dishStatus != OrderStatusEnum.None) && (dish.Status == dishStatus)) selected = true;
                        if ((selected == false) && (departmentId != 0) && (departmentId == dish.Department.Id)) selected = true;
                        if ((selected == false) && (departmentGroupId != 0) && (dish.Department.DepGroups.Any(dg => dg.Id == departmentGroupId))) selected = true;

                        if (selected == true)
                        {
                            if (filteredDishes == null) filteredDishes = new Dictionary<int, OrderDishModel>();
                            filteredDishes.Add(dish.Id, dish);
                        }
                    }
                        
                    if (filteredDishes != null)
                    {
                        curOrder = ord.Copy();
                        curOrder.Dishes = filteredDishes;
                        retVal.Add(curOrder);
                    }
                }
            }
            return retVal;
        }
        // по группе НП
        public List<OrderModel> GetOrdersByDepartmentGroup(int departmentGroupId)
        {
            List<OrderModel> retVal = new List<OrderModel>();
            OrderModel curOrder = null;
            lock (_ordersModel)
            {
                foreach (OrderModel ord in _ordersModel.Orders.Values)
                {
                    // отобрать блюда по условию
                    Dictionary<int, OrderDishModel> filteredDishes = ord.Dishes.Values
                        .Where(d => d.Department.DepGroups.Any(dg => dg.Id == departmentGroupId)).ToDictionary(d => d.Id);
                    if (filteredDishes.Count != 0)
                    {
                        curOrder = ord.Copy();
                        curOrder.Dishes = filteredDishes;
                        retVal.Add(curOrder);
                    }
                }
            }
            return retVal;
        }

        #endregion

        #region IKDSCommandService implementation

        // обновление статуса заказа с КДСа
        public void ChangeOrderStatus(int orderId, OrderStatusEnum orderStatus)
        {
            if (_ordersModel.Orders.ContainsKey(orderId))
            {
                _ordersModel.Orders[orderId].UpdateStatus(orderStatus, true);
            }
        }
        
        // обновление статуса блюда с КДСа
        public void ChangeOrderDishStatus(int orderId, int orderDishId, OrderStatusEnum orderDishStatus)
        {
            if (_ordersModel.Orders.ContainsKey(orderId))
            {
                OrderModel modelOrder = _ordersModel.Orders[orderId];
                if (modelOrder.Dishes.ContainsKey(orderDishId))
                {
                    OrderDishModel modelDish = modelOrder.Dishes[orderDishId];
                    modelDish.UpdateStatus(orderDishStatus, true);
                }
            }
        }
        #endregion

        public void Dispose()
        {
            string msg = "**** Закрытие служебного класса KDSService ****";
            Console.WriteLine(msg);
            AppEnv.WriteLogInfoMessage(msg);
            _ordersModel.Dispose();

            // таймер остановить, отписаться от события и уничтожить
            if (_observeTimer != null)
            {
                if (_observeTimer.Enabled == true) _observeTimer.Stop();
                _observeTimer.Elapsed -= _observeTimer_Elapsed;
                _observeTimer.Dispose();
            }
        }

    }  // class
}
