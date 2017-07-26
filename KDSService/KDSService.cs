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
        private bool _timerEnable;
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

            _observeTimer = new Timer(_ObserveTimerInterval) { AutoReset = false };
            _observeTimer.Elapsed += _observeTimer_Elapsed;
            StartTimer();
            _timerEnable = true;

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

        public void StartTimer()
        {
            if (_observeTimer != null) _observeTimer.Start();
        }

        public void StopTimer()
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
            StopTimer();
            if (_timerEnable)
            {
                string errMsg = _ordersModel.UpdateOrders();

                if (errMsg != null) AppEnv.WriteLogErrorMessage(errMsg);
            }
            StartTimer();
        }


        // ****  SERVICE CONTRACT  *****
        #region IKDSService inplementation

        public List<OrderStatusModel> GetOrderStatuses()
        {
            string errMsg = null;
            string userAction = "    - svc(GetOrderStatuses()): ";

            List<OrderStatusModel>  retVal = ModelDicts.GetOrderStatusesList(out errMsg);

            if (errMsg.IsNull())
                userAction += string.Format("Ok ({0} records)",retVal.Count);
            else
            {
                AppEnv.WriteLogErrorMessage(errMsg);
                userAction += errMsg;
            }
            AppEnv.WriteLogTraceMessage(userAction);

            return retVal;
        }

        public List<DepartmentModel> GetDepartments()
        {
            string errMsg = null;
            string userAction = "    - svc(GetDepartments()): ";

            List<DepartmentModel> retVal = ModelDicts.GetDepartmentsList(out errMsg);

            if (errMsg.IsNull())
                userAction += string.Format("Ok ({0} records)", retVal.Count);
            else
            {
                AppEnv.WriteLogErrorMessage(errMsg);
                userAction += errMsg;
            }
            AppEnv.WriteLogTraceMessage(userAction);

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
            // сложные типы передаем клиенту, как строки
            string s1 = ((TimeSpan)AppEnv.GetAppProperty("TimeOfAutoCloseYesterdayOrders", TimeSpan.Zero)).ToString();
            var v1 = (HashSet<int>)AppEnv.GetAppProperty("UnusedDepartments");
            string s2;
            if (v1 == null) s2 = ""; else s2 = string.Join(",", v1);

            Dictionary<string, object> retval = new Dictionary<string, object>()
            {
                { "ExpectedTake", (int)AppEnv.GetAppProperty("ExpectedTake", 0)},
                { "UseReadyConfirmedState", (bool)AppEnv.GetAppProperty("UseReadyConfirmedState", false)},
                { "TakeCancelledInAutostartCooking", (bool)AppEnv.GetAppProperty("TakeCancelledInAutostartCooking", false)},
                { "TimeOfAutoCloseYesterdayOrders", s1},
                { "UnusedDepartments", s2}
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
        // заблокировать заказ от изменения по таймеру
        public void LockOrder(int orderId)
        {
            Dictionary<int, bool> hs = (Dictionary<int, bool>)AppEnv.GetAppProperty("lockedOrders");
            if (hs == null) hs = new Dictionary<int, bool>();
            if (!hs.ContainsKey(orderId)) hs.Add(orderId, false);
            AppEnv.SetAppProperty("lockedOrders", hs);
        }
        // разблокировать заказ от изменения по таймеру
        public void DelockOrder(int orderId)
        {
            Dictionary<int, bool> hs = (Dictionary<int, bool>)AppEnv.GetAppProperty("lockedOrders");
            if ((hs != null) && hs.ContainsKey(orderId)) hs[orderId] = true;
            AppEnv.SetAppProperty("lockedOrders", hs);
        }
        // заблокировать блюдо от изменения по таймеру
        public void LockDish(int dishId)
        {
            Dictionary<int, bool> hs = (Dictionary<int, bool>)AppEnv.GetAppProperty("lockedDishes");
            if (hs == null) hs = new Dictionary<int, bool>();
            if (!hs.ContainsKey(dishId)) hs.Add(dishId, false);
            AppEnv.SetAppProperty("lockedDishes", hs);
        }
        // разблокировать блюдо от изменения по таймеру
        public void DelockDish(int dishId)
        {
            Dictionary<int, bool> hs = (Dictionary<int, bool>)AppEnv.GetAppProperty("lockedDishes");
            if ((hs != null) && hs.ContainsKey(dishId)) hs[dishId] = true;
            AppEnv.SetAppProperty("lockedDishes", hs);
        }

        // обновление статуса заказа с КДСа
        public void ChangeOrderStatus(int orderId, OrderStatusEnum orderStatus)
        {
            //StopTimer();
            _timerEnable = false;
            AppEnv.WriteLogUserAction("KDS service try to change ORDER status (Id {0}) to {1}", orderId, orderStatus.ToString());

            if (_ordersModel.Orders.ContainsKey(orderId))
            {
                _ordersModel.Orders[orderId].UpdateStatus(orderStatus, true);
            }

            //StartTimer();
            _timerEnable = true;
        }

        // обновление статуса блюда с КДСа
        public void ChangeOrderDishStatus(int orderId, int orderDishId, OrderStatusEnum orderDishStatus)
        {
            //StopTimer();
            _timerEnable = false;

            AppEnv.WriteLogTraceMessage(string.Format("svc: COMMAND change DISH status (Id {0}, orderId {1}) to {2} -- START", orderDishId, orderId, orderDishStatus.ToString()));

            bool result = false;
            if (_ordersModel.Orders.ContainsKey(orderId))
            {
                OrderModel modelOrder = _ordersModel.Orders[orderId];
                if (modelOrder.Dishes.ContainsKey(orderDishId))
                {
                    OrderDishModel modelDish = modelOrder.Dishes[orderDishId];

                    result = modelDish.UpdateStatus(orderDishStatus);
                }
            }

            if (result)
            {
                // убедиться, что в БД записан нужный статус
                DateTime dt = DateTime.Now;
                bool chkStat = false;
                while ((!chkStat) && ((DateTime.Now - dt).TotalMilliseconds <= 2000))
                {
                    System.Threading.Thread.Sleep(100);  // тормознуться на 100 мс
                    using (KDSEntities db = new KDSEntities())
                    {
                        try
                        {
                            OrderDish dbDish = db.OrderDish.Find(orderDishId);
                            chkStat = ((dbDish != null) && (dbDish.DishStatusId == (int)orderDishStatus));
                        }
                        catch (Exception ex)
                        {
                            AppEnv.WriteLogErrorMessage("Ошибка проверочного чтения после записи нового состояния в БД: {0}", AppEnv.GetShortErrMessage(ex));
                        }
                    }
                }
                // истекло время ожидания записи в БД
                if (!chkStat)
                {
                    AppEnv.WriteLogErrorMessage("Истекло время ожидания проверочного чтения после записи нового состояния.");
                }
            }

            AppEnv.WriteLogTraceMessage(string.Format("svc: COMMAND change DISH status (Id {0}, orderId {1}) to {2} -- FINISH", orderDishId, orderId, orderDishStatus.ToString()));

            //StartTimer();
            _timerEnable = true;
        }
        #endregion

        public void Dispose()
        {
            AppEnv.WriteLogTraceMessage("Закрытие служебного класса KDSService...");
            // таймер остановить, отписаться от события и уничтожить
            StopTimer();
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
                }
            }

            AppEnv.WriteLogInfoMessage("**** ЗАВЕРШЕНИЕ работы КДС-сервиса ****");
        }


    }  // class
}
