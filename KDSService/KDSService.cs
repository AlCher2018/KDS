using KDSConsoleSvcHost;
using KDSService.AppModel;
using KDSService.DataSource;
using KDSService.Lib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Timers;


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
        
        // сервис WCF
        private ServiceHost _host;
        public ServiceHost ServiceHost { get { return _host; } }

        // заказы на стороне службы (с таймерами)
        private OrdersModel _ordersModel;

        public KDSServiceClass()
        {
        }

        public void InitService(string configFile = null)
        {
            // установить свой config-файл
            if (configFile != null) AppConfig.Change(configFile);

            bool isResultOk;
            string msg = null;

            msg = AppEnv.LoggerInit();
            if (msg != null)
                throw new Exception("Error: " + msg);

            // инициализация приложения
            AppEnv.WriteLogInfoMessage("**** НАЧАЛО работы КДС-сервиса ****");
            AppEnv.WriteLogInfoMessage("Инициализация КДС-сервиса...");
            AppEnv.WriteLogInfoMessage("   - версия файла {0}: {1}", AppEnv.GetAppFileName(),  AppEnv.GetAppVersion());
            isResultOk = AppEnv.AppInit(out msg);
            if (!isResultOk)
                throw new Exception("Ошибка инициализации КДС-сервиса: " + msg);

            // проверить доступность БД
            AppEnv.WriteLogInfoMessage("  Проверка доступа к базе данных...");
            isResultOk = AppEnv.CheckDBConnection(typeof(KDSEntities), out msg);
            if (!isResultOk)
                throw new Exception("Ошибка проверки доступа к базе данных: " + msg);

            // проверка справочных таблиц (в классе ModelDicts)
            AppEnv.WriteLogInfoMessage("  Проверка наличия справочных таблиц...");
            isResultOk = AppEnv.CheckAppDBTable(out msg);
            if (!isResultOk)
                throw new Exception("Ошибка проверки справочных таблиц: " + msg);

            // получение словарей приложения из БД
            AppEnv.WriteLogInfoMessage("  Получение справочных таблиц из БД...");
            isResultOk = ModelDicts.UpdateModelDictsFromDB(out msg);
            if (!isResultOk)
                throw new Exception("Ошибка получения словарей из БД: " + msg);

            _observeTimer = new Timer(_ObserveTimerInterval) { AutoReset = true };
            _observeTimer.Elapsed += _observeTimer_Elapsed;

            AppEnv.WriteLogInfoMessage("  Инициализация внутренней коллекции заказов...");
            try
            {
                _ordersModel = new OrdersModel();
            }
            catch (Exception)
            {
                throw;
            }

            AppEnv.WriteLogInfoMessage("Инициализация КДС-сервиса... Ok");
        }

        // создает сервис WCF, параметры канала считываются из app.config
        public void CreateHost()
        {
            try
            {
                AppEnv.WriteLogInfoMessage("Создание канала для приема сообщений...");
                //host = new ServiceHost(typeof(KDSService.KDSServiceClass));
                _host = new ServiceHost(this);
                //host.OpenTimeout = TimeSpan.FromMinutes(10);  // default 1 min
                //host.CloseTimeout = TimeSpan.FromMinutes(1);  // default 10 sec

                _host.Open();
                writeHostInfoToLog();
                AppEnv.WriteLogInfoMessage("Создание канала для приема сообщений... Ok");
            }
            catch (Exception ex)
            {
                // исключение записать в лог
                AppEnv.WriteLogErrorMessage("Ошибка открытия канала сообщений: {0}{1}\tTrace: {2}", ex.Message, Environment.NewLine, ex.StackTrace);
                // и передать выше
                throw;
            }
        }

        public void StartService()
        {
            if (_observeTimer != null) _observeTimer.Start();
        }

        public void StopService()
        {
            if (_observeTimer != null) _observeTimer.Stop();
        }

        private void writeHostInfoToLog()
        {
            foreach (System.ServiceModel.Description.ServiceEndpoint se in _host.Description.Endpoints)
            {
                AppEnv.WriteLogInfoMessage(" - host info: address {0}; binding {1}; contract {2}", se.Address, se.Binding.Name, se.Contract.Name);
            }
        }

        // периодический просмотр заказов
        private void _observeTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            StopService();

            string errMsg = _ordersModel.UpdateOrders();
            if (errMsg != null) AppEnv.WriteLogErrorMessage(errMsg);

            StartService();
        }


        // ****  SERVICE CONTRACT  *****
        #region IKDSService inplementation

        public List<OrderStatusModel> GetOrderStatuses()
        {
            string errMsg = null;
            string userAction = "GetOrderStatuses()... ";

            List<OrderStatusModel>  retVal = ModelDicts.GetOrderStatusesList(out errMsg);

            if (errMsg.IsNull())
                userAction += string.Format("Ok ({0} records)",retVal.Count);
            else
            {
                AppEnv.WriteLogErrorMessage(errMsg);
                userAction += errMsg;
            }
            AppEnv.WriteLogUserAction(userAction);

            return retVal;
        }

        public List<DepartmentModel> GetDepartments()
        {
            string errMsg = null;
            string userAction = "GetDepartments()... ";

            List<DepartmentModel> retVal = ModelDicts.GetDepartmentsList(out errMsg);

            if (errMsg.IsNull())
                userAction += string.Format("Ok ({0} records)", retVal.Count);
            else
            {
                AppEnv.WriteLogErrorMessage(errMsg);
                userAction += errMsg;
            }
            AppEnv.WriteLogUserAction(userAction);

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

            AppEnv.WriteLogUserAction("ChangeOrderStatus(orderId:{0}, status:{1})", orderId, orderStatus.ToString());

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

                    AppEnv.WriteLogUserAction("ChangeOrderDishStatus(orderId:{0}, orderDishId:{1}, status:{2})", orderId, orderDishId, orderDishStatus.ToString());

                    modelDish.UpdateStatus(orderDishStatus, true);
                }
            }
            _observeTimer.Start();
        }
        #endregion

        public void Dispose()
        {
            AppEnv.WriteLogTraceMessage("Закрытие служебного класса KDSService...");
            bool b1 = true;
            // таймер остановить, отписаться от события и уничтожить
            StopService();
            _observeTimer.Dispose();

            AppEnv.WriteLogTraceMessage("   close ServiceHost...");
            if (_host != null)
            {
                try
                {
                    _host.Close(); _host = null;
                }
                catch (Exception ex)
                {
                    AppEnv.WriteLogErrorMessage("   Error: " + ex.ToString());
                    b1 = false;
                }
            }

            AppEnv.WriteLogTraceMessage("   clear inner Orders collection...");
            if (_ordersModel != null)
            {
                try
                {
                    _ordersModel.Dispose();
                }
                catch (Exception ex)
                {
                    AppEnv.WriteLogErrorMessage("   Error: " + ex.ToString());
                    b1 = false;
                }
            }

            AppEnv.WriteLogInfoMessage("**** ЗАВЕРШЕНИЕ работы КДС-сервиса ****");
        }


    }  // class
}
