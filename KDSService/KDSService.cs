using IntegraLib;
using KDSConsoleSvcHost;
using KDSService.AppModel;
using KDSService.DataSource;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
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
        /*
  <system.serviceModel>
    <bindings>
      <netTcpBinding>
        <binding name="getBinding">
          <security mode="None" />
        </binding>
        <binding name="setBinding" receiveTimeout="05:00:00">
          <reliableSession enabled="true" inactivityTimeout="05:00:00"/>
          <security mode="None" />
        </binding>
      </netTcpBinding>
    </bindings>

    <services>
      <service name="KDSService.KDSServiceClass">
        <endpoint address="net.tcp://localhost:8733/KDSService" binding="netTcpBinding"
          contract="KDSService.IKDSService" bindingConfiguration="getBinding"/>
        <endpoint address="net.tcp://localhost:8734/KDSCommandService/"
          binding="netTcpBinding" contract="KDSService.IKDSCommandService" bindingConfiguration="setBinding"/>
        <endpoint address="net.tcp://localhost/mex" binding="mexTcpBinding"
          contract="IMetadataExchange" />
      </service>
    </services>
    <behaviors>
      <serviceBehaviors>
        <behavior>
          <serviceMetadata/>
          <serviceDebug includeExceptionDetailInFaults="False"/>
        </behavior>
      </serviceBehaviors>
    </behaviors>
  </system.serviceModel>

***** Host Info *****
Address: net.tcp://localhost:8733/KDSService
Binding: NetTcpBinding
Contract: IKDSService

Address: net.tcp://localhost:8734/KDSService
Binding: NetTcpBinding
Contract: IKDSCommandService

Address: net.tcp://localhost/mex
Binding: MetadataExchangeTcpBinding
Contract: IMetadataExchange
**********************
         */
        public void CreateHost()
        {
            try
            {
                AppEnv.WriteLogInfoMessage("Создание канала для приема сообщений...");
                //_host = new ServiceHost(typeof(KDSService.KDSServiceClass));
                _host = new ServiceHost(this);
                if (_host.Description.Behaviors.Contains(typeof(ServiceMetadataBehavior)) == false)
                {
                    ServiceMetadataBehavior metaBhv = new ServiceMetadataBehavior();
                    _host.Description.Behaviors.Add(metaBhv);
                }

                NetTcpBinding getBinding = new NetTcpBinding(SecurityMode.None, false);
                NetTcpBinding setBinding = new NetTcpBinding(SecurityMode.None, true);
                setBinding.ReceiveTimeout = new TimeSpan(5, 0, 0);
                setBinding.ReliableSession.InactivityTimeout = new TimeSpan(5, 0, 0);

                _host.AddServiceEndpoint(typeof(IKDSService), getBinding, "net.tcp://localhost:8733/KDSService");
                _host.AddServiceEndpoint(typeof(IKDSCommandService), setBinding, "net.tcp://localhost:8734/KDSCommandService");
                _host.AddServiceEndpoint(typeof(System.ServiceModel.Description.IMetadataExchange), MetadataExchangeBindings.CreateMexTcpBinding(), "net.tcp://localhost/mex");

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

        public List<OrderStatusModel> GetOrderStatuses(string machineName)
        {
            string errMsg = null;
            string logMsg = "GetOrderStatuses(): ";

            List<OrderStatusModel> retVal = ModelDicts.GetOrderStatusesList(out errMsg);

            if (errMsg.IsNull())
                logMsg += string.Format("Ok ({0} records)",retVal.Count);
            else
            {
                AppEnv.WriteLogClientAction(machineName, errMsg);
                logMsg += errMsg;
            }

            AppEnv.WriteLogClientAction(machineName, logMsg);

            return retVal;
        }

        public List<DepartmentModel> GetDepartments(string machineName)
        {
            string errMsg = null;
            string logMsg = "GetDepartments(): ";

            List<DepartmentModel> retVal = ModelDicts.GetDepartmentsList(out errMsg);

            if (errMsg.IsNull())
                logMsg += string.Format("Ok ({0} records)", retVal.Count);
            else
            {
                AppEnv.WriteLogErrorMessage(errMsg);
                logMsg += errMsg;
            }
            AppEnv.WriteLogClientAction(machineName, logMsg);

            return retVal;
        }

        // все заказы
        public List<OrderModel> GetOrders(string machineName, List<int> clientStatusIDs, List<int> clientDepIDs, OrderGroupEnum clientGroupBy)
        {
            List<OrderModel> retVal = new List<OrderModel>();

            AppEnv.WriteLogClientAction(machineName, "GetOrders('{0}', '{1}', '{2}', '{3}')", machineName, string.Join(",", clientStatusIDs), string.Join(",", clientDepIDs), clientGroupBy.ToString());

            OrderModel validOrder;
            List<OrderDishModel> validDishes = new List<OrderDishModel>();
            // key - блюдо, value - список ингредиентов
            Dictionary<OrderDishModel, List<OrderDishModel>> dishIngr = new Dictionary<OrderDishModel, List<OrderDishModel>>();
            lock (_ordersModel)
            {
                foreach (OrderModel order in _ordersModel.Orders.Values)
                {
                    // разобрать плоский список блюд и ингр. в иерархический
                    dishIngr.Clear();
                    foreach (OrderDishModel dish in order.Dishes.Values.Where(d => d.ParentUid.IsNull()))
                    {
                        List<OrderDishModel> ingrList = new List<OrderDishModel>(order.Dishes.Values.Where(d => (!d.ParentUid.IsNull()) && (d.ParentUid == dish.Uid)));
                        dishIngr.Add(dish, ingrList);
                    }

                    // собрать в validDishes блюда и ингр., которые проходят проверку
                    validDishes.Clear();
                    foreach (KeyValuePair<OrderDishModel, List<OrderDishModel>> diPair in dishIngr)
                    {
                        // блюдо проходит - берем и все его ингредиенты
                        if (checkOrderItem(clientStatusIDs, clientDepIDs, diPair.Key))
                        {
                            validDishes.Add(diPair.Key);
                            validDishes.AddRange(diPair.Value);
                        }
                        // иначе, если есть ингредиент, проходящий проверку, то добавляем и блюдо и прошедшие проверку ингредиенты
                        else if (diPair.Value.Any(ingr => checkOrderItem(clientStatusIDs, clientDepIDs, ingr)))
                        {
                            validDishes.Add(diPair.Key);
                            validDishes.AddRange(diPair.Value.Where(ingr => checkOrderItem(clientStatusIDs, clientDepIDs, ingr)));
                        }
                    }

                    if (validDishes.Count > 0)
                    {
                        validOrder = order.Copy();
                        validDishes.ForEach(d => validOrder.Dishes.Add(d.Id, d));

                        retVal.Add(validOrder);
                    }

                }  // foreach OrderModel
            }  // lock _ordersModel

            // группировка и сортировка
            if (retVal.Count > 0)
            {
                // группировка по CreateDate блюд может увеличить кол-во заказов
                if (clientGroupBy == OrderGroupEnum.ByCreateTime)
                {
                    // разбить заказы по датам (CreateDate)
                    SortedList<DateTime, OrderModel> sortedOrders = new SortedList<DateTime, OrderModel>();
                    foreach (OrderModel order in retVal)
                    {
                        if (sortedOrders.ContainsKey(order.CreateDate) == false) sortedOrders.Add(order.CreateDate, order);
                    }
                    // если кол-во заказов не изменилось, просто сохраним отсортированный список заказов
                    if (retVal.Count == sortedOrders.Count)
                        retVal = sortedOrders.Values.ToList();
                    // иначе создать заново выходный список
                    else
                    {

                    }
                }

                // сортировка по номеру подачи и Id
                List<OrderDishModel> sortedDishes;
                foreach (OrderModel order in retVal)
                {
                    sortedDishes = order.Dishes.Values.OrderBy(d => d.FilingNumber).ThenBy(d => d.Id).ToList();

                    order.Dishes.Clear();
                    sortedDishes.ForEach(d => order.Dishes.Add(d.Id, d));
                }
            }

            AppEnv.WriteLogClientAction(machineName, " - result: {0} orders", retVal.Count);
            return retVal;
        }

        private bool checkOrderItem(List<int> clientStatusIDs, List<int> clientDepIDs, OrderDishModel orderItem)
        {
            return (clientStatusIDs.Contains(orderItem.DishStatusId) && clientDepIDs.Contains(orderItem.DepartmentId));
        }

        private class DateTimeComparer : IComparer<DateTime>
        {
            public int Compare(DateTime x, DateTime y)
            {
                return x.CompareTo(y);
            }
        }

        // **** настройки из config-файла хоста
        public Dictionary<string, object> GetHostAppSettings(string machineName)
        {
            string logMsg = "GetHostAppSettings(): ";
            Dictionary<string, object> retval = new Dictionary<string, object>();
            try
            {
                // сложные типы передаем клиенту, как строки
                var v1 = AppProperties.GetProperty("TimeOfAutoCloseYesterdayOrders");
                TimeSpan ts1 = ((v1 == null) ? TimeSpan.Zero : (TimeSpan)v1);
                v1 = AppProperties.GetProperty("UnusedDepartments");
                string s2 = ((v1 == null) ? "" : string.Join(",", (HashSet<int>)v1));

                retval.Add("ExpectedTake", AppProperties.GetIntProperty("ExpectedTake"));
                retval.Add("UseReadyConfirmedState", AppProperties.GetBoolProperty("UseReadyConfirmedState"));
                retval.Add("TakeCancelledInAutostartCooking", AppProperties.GetBoolProperty("TakeCancelledInAutostartCooking"));
                retval.Add("TimeOfAutoCloseYesterdayOrders", ts1.ToString());
                retval.Add("UnusedDepartments", s2);

                logMsg += "Ok";
            }
            catch (Exception ex)
            {
                logMsg += ex.Message;
            }
            AppEnv.WriteLogClientAction(machineName, logMsg);

            return retval;
        }

        public void SetExpectedTakeValue(string machineName, int value)
        {
            string logMsg = string.Format("SetExpectedTakeValue({0}): ", value);

            AppProperties.SetProperty("ExpectedTake", value);

            string errMsg;
            if (CfgFileHelper.SaveAppSettings("ExpectedTake", value.ToString(), out errMsg))
                logMsg += "Ok";
            else
                logMsg += errMsg;
            AppEnv.WriteLogClientAction(machineName, logMsg);
        }

        #endregion

        #region IKDSCommandService implementation
        // заблокировать заказ от изменения по таймеру
        public void LockOrder(string machineName, int orderId)
        {
            string logMsg = string.Format("LockOrder({0}): ", orderId);

            Dictionary<int, bool> hs = (Dictionary<int, bool>)AppProperties.GetProperty("lockedOrders");  // получить
            if (!hs.ContainsKey(orderId)) hs.Add(orderId, false);   // добавить
            else hs[orderId] = false;

            logMsg += "Ok";
            AppEnv.WriteLogClientAction(machineName, logMsg);
        }
        // разблокировать заказ от изменения по таймеру
        public void DelockOrder(string machineName, int orderId)
        {
            string logMsg = string.Format("DelockOrder({0}): ", orderId);

            Dictionary<int, bool> hs = (Dictionary<int, bool>)AppProperties.GetProperty("lockedOrders");
            if (hs.ContainsKey(orderId)) hs[orderId] = true;

            logMsg += "Ok";
            AppEnv.WriteLogClientAction(machineName, logMsg);
        }

        // заблокировать блюдо от изменения по таймеру
        public void LockDish(string machineName, int dishId)
        {
            string logMsg = string.Format("LockDish({0}): ", dishId);

            Dictionary<int, bool> hs = (Dictionary<int, bool>)AppProperties.GetProperty("lockedDishes");
            if (!hs.ContainsKey(dishId)) hs.Add(dishId, false);
            AppProperties.SetProperty("lockedDishes", hs);

            logMsg += "Ok";
            AppEnv.WriteLogClientAction(machineName, logMsg);
        }
        // разблокировать блюдо от изменения по таймеру
        public void DelockDish(string machineName, int dishId)
        {
            string logMsg = string.Format("DelockDish({0}): ", dishId);

            Dictionary<int, bool> hs = (Dictionary<int, bool>)AppProperties.GetProperty("lockedDishes");
            if (hs.ContainsKey(dishId)) hs[dishId] = true;
            AppProperties.SetProperty("lockedDishes", hs);

            logMsg += "Ok";
            AppEnv.WriteLogClientAction(machineName, logMsg);
        }

        // обновление статуса заказа с КДСа
        public void ChangeOrderStatus(string machineName, int orderId, OrderStatusEnum orderStatus)
        {
            string logMsg = string.Format("ChangeOrderStatus({0}, {1}): ", orderId, orderStatus);
            DateTime dtTmr = DateTime.Now;
            AppEnv.WriteLogClientAction(machineName, logMsg + " - START");
            //StopTimer();
            _timerEnable = false;

            if (_ordersModel.Orders.ContainsKey(orderId))
            {
                _ordersModel.Orders[orderId].UpdateStatus(orderStatus, true, machineName);
                logMsg += "Ok";
            }
            else
            {
                logMsg += "order not found in the Model";
            }

            //StartTimer();
            _timerEnable = true;

            AppEnv.WriteLogClientAction(machineName, logMsg + " - FINISH" + (DateTime.Now - dtTmr).ToString());
        }

        // обновление статуса блюда с КДСа
        public void ChangeOrderDishStatus(string machineName, int orderId, int orderDishId, OrderStatusEnum orderDishStatus)
        {
            string logMsg = string.Format("ChangeOrderDishStatus(orderId:{0}, dishId:{1}, status:{2}): ", orderId, orderDishId, orderDishStatus);
            DateTime dtTmr = DateTime.Now;
            AppEnv.WriteLogClientAction(machineName, logMsg + " - START");
            //StopTimer();
            _timerEnable = false;

            bool result = false;
            if (_ordersModel.Orders.ContainsKey(orderId))
            {
                OrderModel modelOrder = _ordersModel.Orders[orderId];
                if (modelOrder.Dishes.ContainsKey(orderDishId))
                {
                    OrderDishModel modelDish = modelOrder.Dishes[orderDishId];

                    result = modelDish.UpdateStatus(orderDishStatus, machineName: machineName);
                }
            }

            //if (result)
            //{
            //    // убедиться, что в БД записан нужный статус
            //    DateTime dt = DateTime.Now;
            //    bool chkStat = false;
            //    while ((!chkStat) && ((DateTime.Now - dt).TotalMilliseconds <= 2000))
            //    {
            //        System.Threading.Thread.Sleep(100);  // тормознуться на 100 мс
            //        using (KDSEntities db = new KDSEntities())
            //        {
            //            try
            //            {
            //                OrderDish dbDish = db.OrderDish.Find(orderDishId);
            //                chkStat = ((dbDish != null) && (dbDish.DishStatusId == (int)orderDishStatus));
            //            }
            //            catch (Exception ex)
            //            {
            //                AppEnv.WriteLogErrorMessage("Ошибка проверочного чтения после записи нового состояния в БД: {0}", AppEnv.GetShortErrMessage(ex));
            //            }
            //        }
            //    }
            //    // истекло время ожидания записи в БД
            //    if (!chkStat)
            //    {
            //        AppEnv.WriteLogErrorMessage("Истекло время ожидания (2 сек) проверочного чтения после записи нового состояния.");
            //    }
            //}

            AppEnv.WriteLogClientAction(machineName, logMsg + " - FINISH - " + (DateTime.Now - dtTmr).ToString());

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
