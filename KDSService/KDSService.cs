using KDSConsoleSvcHost;
using KDSService.AppModel;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Timers;
using System.Windows;

namespace KDSService
{
    /// <summary>
    /// 1. Периодический опрос заказов из БД
    /// </summary>

    [ServiceBehavior(AddressFilterMode = AddressFilterMode.Any, 
        IncludeExceptionDetailInFaults = true,
        InstanceContextMode = InstanceContextMode.Single)]
    public class KDSServiceClass : IDisposable, IKDSService, IKDSCommandService
    {
        // таймер наблюдения за заказами в БД
        private Timer _observeTimer;
        // периодичность опроса БД, в мсек
        private const double _ObserveTimerInterval = 1000;

        // заказы на стороне службы (с таймерами)
        private OrdersModel _ordersModel;

        public KDSServiceClass()
        {
            // инициализация приложения
            string msg = null;
            if (AppEnv.AppInit(out msg) == false)
            {
                if (msg != null) throw new Exception("Ошибка инициализации приложения: " + msg);
            }

            msg = "  получение словарей приложения из БД...";
            AppEnv.WriteLogInfoMessage(msg);

            if (ModelDicts.UpdateModelDictsFromDB(out msg) == false) throw new Exception(msg);

            msg = "  получение словарей приложения из БД... Ok";
            AppEnv.WriteLogInfoMessage(msg);

            _observeTimer = new Timer(_ObserveTimerInterval) { AutoReset = true};
            _observeTimer.Elapsed += _observeTimer_Elapsed;

            _ordersModel = new OrdersModel();

            startService();
        }

        private void startService()
        {
            if (_observeTimer != null) _observeTimer.Start();
        }
        private void stopService()
        {
            if (_observeTimer != null) _observeTimer.Stop();
        }


        // периодический просмотр заказов
        private void _observeTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            stopService();

            string errMsg = _ordersModel.UpdateOrders();
            if (errMsg != null) AppEnv.WriteLogErrorMessage(errMsg);

            startService();
        }


        // ****  SERVICE CONTRACT  *****
        #region IKDSService inplementation

        public List<OrderStatusModel> GetOrderStatuses()
        {
            string errMsg = null;
            List<OrderStatusModel>  retVal = ModelDicts.GetOrderStatusesList(out errMsg);

            if (retVal == null) AppEnv.WriteLogErrorMessage(errMsg);

            return retVal;
        }

        public List<DepartmentModel> GetDepartments()
        {
            string errMsg = null;
            List<DepartmentModel> retVal = ModelDicts.GetDepartmentsList(out errMsg);

            if (retVal == null) AppEnv.WriteLogErrorMessage(errMsg);

            return retVal;
        }

        // все заказы
        public List<OrderModel> GetOrders()
        {
            List<OrderModel> retVal = null;
            retVal = _ordersModel.Orders.Values.ToList();

            return retVal;
        }

        // **** настройки из config-файла хоста
        public Dictionary<string, object> GetHostAppSettings()
        {
            Dictionary<string, object> retval = new Dictionary<string, object>()
            {
                { "IsIngredientsIndependent", (bool)AppEnv.GetAppProperty("IsIngredientsIndependent", false)},
                { "ExpectedTake", (int)AppEnv.GetAppProperty("ExpectedTake", 0)},
                { "UseReadyConfirmedState", (bool)AppEnv.GetAppProperty("UseReadyConfirmedState", false)},
                { "TakeCancelledInAutostartCooking", (bool)AppEnv.GetAppProperty("TakeCancelledInAutostartCooking", false)},
            };

            return retval;
        }


        public void SetExpectedTakeValue(int value)
        {
            AppEnv.SetAppProperty("ExpectedTake", value);

            string errMsg;
            AppEnv.SaveAppSettings("ExpectedTake", value.ToString(), out errMsg);
        }

        #endregion

        #region IKDSCommandService implementation

        // обновление статуса заказа с КДСа
        public void ChangeOrderStatus(int orderId, OrderStatusEnum orderStatus)
        {
            _observeTimer.Stop();

            if (_ordersModel.Orders.ContainsKey(orderId))
            {
                _ordersModel.Orders[orderId].UpdateStatus(orderStatus, true);
            }
            _observeTimer.Start();
        }

        // обновление статуса блюда с КДСа
        public void ChangeOrderDishStatus(int orderId, int orderDishId, OrderStatusEnum orderDishStatus)
        {
            _observeTimer.Stop();
            if (_ordersModel.Orders.ContainsKey(orderId))
            {
                OrderModel modelOrder = _ordersModel.Orders[orderId];
                if (modelOrder.Dishes.ContainsKey(orderDishId))
                {
                    OrderDishModel modelDish = modelOrder.Dishes[orderDishId];
                    modelDish.UpdateStatus(orderDishStatus, true);
                }
            }
            _observeTimer.Start();
        }
        #endregion

        public void Dispose()
        {
            // таймер остановить, отписаться от события и уничтожить
            stopService();
            _observeTimer.Dispose();

            string msg = "**** Закрытие служебного класса KDSService ****";
            try
            {
                Console.WriteLine(msg); AppEnv.WriteLogInfoMessage(msg);
                _ordersModel.Dispose();

            }
            catch (Exception ex)
            {
                AppEnv.WriteLogInfoMessage("Error: " + ex.ToString());
            }
        }


    }  // class
}
