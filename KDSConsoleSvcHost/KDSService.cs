using KDSConsoleSvcHost;
using KDSService.AppModel;
using NLog;
using System;
using System.Collections.Generic;
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
    public class KDSServiceClass : IDisposable, IKDSService
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
        public void Dispose()
        {
            string msg = "**** Закрытие служебного класса KDSService ****";
            Console.WriteLine(msg);
            AppEnv.WriteLogInfoMessage(msg);

            // таймер остановить, отписаться от события и уничтожить
            if (_observeTimer != null)
            {
                if (_observeTimer.Enabled == true) _observeTimer.Stop();
                _observeTimer.Elapsed -= _observeTimer_Elapsed;
                _observeTimer.Dispose();
            }
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
        #region service contract

        public List<OrderStatusModel> GetOrderStatusList()
        {
            string errMsg = null;
            List<OrderStatusModel>  retVal = ServiceDics.GetOrderStatusList(out errMsg);

            if (retVal == null) AppEnv.WriteLogErrorMessage(errMsg);
            return retVal;
        }

        public void ChangeStatus(OrderCommand command)
        {
            throw new NotImplementedException();
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

        public List<OrderModel> GetOrders()
        {
            List<OrderModel> retVal = new List<OrderModel>();
            retVal.AddRange(_ordersModel.Orders.Values);

            return retVal;
        }

        #endregion


    }  // class
}
