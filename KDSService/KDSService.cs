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
        
        // сервис WCF
        private ServiceHost _host;
        public ServiceHost ServiceHost { get { return _host; } }

        // словарь клиентов
        Dictionary<string, ClientInfo> _clients;

        // заказы на стороне службы (с таймерами)
        private OrdersModel _ordersModel;


        // CTOR
        public KDSServiceClass()
        {
            _clients = new Dictionary<string, ClientInfo>();
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

            // информация о файлах и сборках
            AppEnv.WriteLogInfoMessage(" - host-файл: '{0}', ver. {1}", AppEnvironment.GetAppFullFile(), AppEnvironment.GetAppVersion());
            ITSAssemmblyInfo asmInfo = new ITSAssemmblyInfo("KDSService");
            AppEnv.WriteLogInfoMessage(" - KDSService lib: '{0}', ver. {1}", asmInfo.FullFileName, asmInfo.Version);
            asmInfo.LoadInfo("IntegraLib");
            AppEnv.WriteLogInfoMessage(" - Integra lib: '{0}', ver. {1}", asmInfo.FullFileName, asmInfo.Version);

            AppEnv.WriteLogInfoMessage("Инициализация КДС-сервиса...");
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

            // периодичность опроса БД - 500 в мсек
            _observeTimer = new Timer(500) { AutoReset = false };
            _observeTimer.Elapsed += _observeTimer_Elapsed;
            StartTimer();

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

            // если ни один клиент не читает буфер заказов (_dbOrders), то можно буфер обновить данными из БД
            if (_clients.All(kvp => (kvp.Value.GetOrdersFlag == false)))
            {
                string errMsg = _ordersModel.UpdateOrders();

                if (errMsg != null) AppEnv.WriteLogErrorMessage(errMsg);

                // установить стандартный интервал опроса БД
                if (_observeTimer.Interval != 500) _observeTimer.Interval = 500; // msec
            }
            // иначе установить минимальный интервал для наискорейшего чтения данных из БД
            else
            {
                if (_observeTimer.Interval != 50) _observeTimer.Interval = 50; // msec
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

        // *** ЗАПРОС ЗАКАЗОВ ОТ КЛИЕНТОВ ***
        // сюда передаются условия отбора и группировки, также здесь происходить сортировка позиций заказа
        public ServiceResponce GetOrders(string machineName, ClientDataFilter clientFilter)
        {
            AppEnv.WriteLogClientAction(machineName, "GetOrders('{0}', '{1}')", machineName, clientFilter.ToString());

            // таймер чтения из БД выключен - ждем, пока не закончится чтение данных из БД
            if (_observeTimer.Enabled == false)
            {
                AppEnv.WriteLogClientAction(machineName, " - return 0, svc reading data yet...");
                // вернуть клиенту null - признак того, что надо уменьшить интервал таймера запроса данных к службе
                return null;
            }

            // "хвосты" по умолчанию - false, набор List<OrderModel> создается в конструкторе ServiceResponce
            ServiceResponce retVal = new ServiceResponce();
            List<OrderModel> retValList = retVal.OrdersList;
            DateTime dtTmr = DateTime.Now;

            // временные буферы
            OrderModel validOrder;
            List<OrderDishModel> validDishes = new List<OrderDishModel>();
            // key - блюдо, value - список ингредиентов
            Dictionary<OrderDishModel, List<OrderDishModel>> dishIngr = new Dictionary<OrderDishModel, List<OrderDishModel>>();

            // установить флаг чтения заказов из внутренней коллекции клиентом machineName
            getClientInfo(machineName).GetOrdersFlag = true;

            // получить в retVal отфильтрованные заказы
            foreach (OrderModel order in _ordersModel.Orders.Values)
            {
                //    iCnt++;
                //Debug.Print(string.Format("- {0}. обработка Id - {1}", iCnt, order.Id.ToString()));

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
                    if (checkOrderItem(clientFilter.StatusesList, clientFilter.DepIDsList, diPair.Key))
                    {
                        validDishes.Add(diPair.Key);
                        validDishes.AddRange(diPair.Value);
                    }
                    // иначе, если есть ингредиент, проходящий проверку, то добавляем и блюдо и прошедшие проверку ингредиенты
                    else if (diPair.Value.Any(ingr => checkOrderItem(clientFilter.StatusesList, clientFilter.DepIDsList, ingr)))
                    {
                        validDishes.Add(diPair.Key);
                        validDishes.AddRange(diPair.Value.Where(ingr => checkOrderItem(clientFilter.StatusesList, clientFilter.DepIDsList, ingr)));
                    }
                }

                if (validDishes.Count > 0)
                {
                    validOrder = order.Copy();
                    // к заказу добавить правильные блюда
                    foreach (OrderDishModel dish in validDishes)
                    {
                        if (validOrder.Dishes.ContainsKey(dish.Id) == false)
                            validOrder.Dishes.Add(dish.Id, dish);
                    }

                    // и добавить к результату правильный заказ
                    retValList.Add(validOrder);
                }

            }  // foreach OrderModel

            // сбросить флаг чтения заказов из внутренней коллекции клиентом machineName
            getClientInfo(machineName).GetOrdersFlag = false;
            
            // группировка и сортировка retVal
            if (retValList.Count > 0)
            {
                // для каждого клиента хранить набор Id заказов из БД для определения появления нового заказа (проигрывание мелодии на клиенте)
                int[] uniqOrdersId = retValList.Select(o => o.Id).Distinct().ToArray();  // собрать уникальные Id
                ClientInfo client = getClientInfo(machineName);
                bool isExistsNewOrder = ((client.CurrentOrderIdsList.Count == 0) && (uniqOrdersId.Length > 0));
                if (!isExistsNewOrder)
                {
                    foreach (int curId in uniqOrdersId)
                        if (!client.CurrentOrderIdsList.Contains(curId))
                        {
                            isExistsNewOrder = true; break;
                        }
                    client.CurrentOrderIdsList.Clear();
                }
                client.CurrentOrderIdsList.AddRange(uniqOrdersId);
                retVal.IsExistsNewOrder = isExistsNewOrder;

                // группировка по CreateDate блюд может увеличить кол-во заказов
                #region группировка по CreateDate блюд может увеличить кол-во заказов
                if (clientFilter.GroupBy == OrderGroupEnum.ByCreateTime)
                {
                    // разбить заказы по датам (CreateDate)
                    SortedList<DateTime, OrderModel> sortedOrders = new SortedList<DateTime, OrderModel>();
                    List<DateTime> dtList;
                    foreach (OrderModel order in retValList)
                    {
                        dtList = order.Dishes.Values.Select(d => d.CreateDate).Distinct().ToList();
                        foreach (DateTime dtCreate in dtList)
                        {
                            if (sortedOrders.ContainsKey(dtCreate) == false) sortedOrders.Add(dtCreate, order);
                        }
                    }
                    // если кол-во заказов не изменилось, просто сохраним отсортированный список заказов
                    if (retValList.Count == sortedOrders.Count)
                        retValList = sortedOrders.Values.ToList();
                    // иначе создать заново выходный список заказов с блюдами
                    else
                    {
                        retValList.Clear();
                        OrderModel tmpOrder;
                        foreach (var ord in sortedOrders)
                        {
                            // скопировать заказ
                            tmpOrder = ord.Value.Copy();
                            tmpOrder.CreateDate = ord.Key;
                            // скопировать блюда на данную дату
                            foreach (var tmpDish in ord.Value.Dishes.Values.Where(d => d.CreateDate == ord.Key))
                                tmpOrder.Dishes.Add(tmpDish.Id, tmpDish);
                            // добавить заказ в выходную коллекцию
                            retValList.Add(tmpOrder);
                        }
                    }
                }
                #endregion
                
                // группировка по номеру заказа
                else if (clientFilter.GroupBy == OrderGroupEnum.ByOrderNumber)
                {
                    retValList.Sort((om1, om2) => om1.Number.CompareTo(om2.Number));
                }

                // сортировка блюд в заказах по номеру подачи
                Dictionary<int, OrderDishModel> sortedDishes;
                foreach (OrderModel order in retValList)
                {
                    sortedDishes = (from dish in order.Dishes.Values orderby dish.FilingNumber select dish).ToDictionary(d => d.Id);
                    order.Dishes = sortedDishes;
                }

                if (retVal.IsExistsNewOrder)
                {
                    clientFilter.EndpointOrderID = retValList[0].Id;
                    clientFilter.EndpointOrderItemID = retValList[0].Dishes.First().Value.Id;
                }

                // ограничение количества отдаваемых клиенту объектов
                if ((clientFilter.EndpointOrderID > 0) 
                    || (clientFilter.EndpointOrderItemID > 0)
                    || (clientFilter.ApproxMaxDishesCountOnPage > 0))
                {
                    limitOrderItems(retVal, clientFilter);
                }

            }  // if (retVal.Count > 0)

            AppEnv.WriteLogClientAction(machineName, " - result: {0} orders - {1}", retValList.Count, (DateTime.Now - dtTmr).ToString());
            return retVal;
        }

        private int getTotalItemsCount(List<OrderModel> ordersList)
        {
            int retVal = 0;
            foreach (OrderModel item in ordersList)
            {
                retVal += item.Dishes.Count;
            }
            return retVal;
        }

        // ограничение количества отдаваемых клиенту объектов
        private void limitOrderItems(ServiceResponce svcResp, ClientDataFilter clientFilter)
        {
            int idxOrder = -1;
            List<OrderModel> orderList = svcResp.OrdersList;

            // найти индекс элемента коллекции заказов, 
            // у которого order.Id==clientFilter.OrderId && dish.Id==clientFilter.DishId
            // элементов order.Id==clientFilter.OrderId может быть несколько при группировке по времени
            if ((clientFilter.EndpointOrderID > 0) && (clientFilter.EndpointOrderItemID > 0))
            {
                idxOrder = orderList.FindIndex(om =>
                (om.Id == clientFilter.EndpointOrderID) && (om.Dishes.Any(kvp => kvp.Value.Id == clientFilter.EndpointOrderItemID)));
            }
            // если не найден такой OrderModel, то начинаем с первого элемента
            if (idxOrder == -1) idxOrder = 0;

            //int idxOrderItem = -1;
            //int i = 0;
            //foreach (KeyValuePair<int,OrderDishModel> item in orderList[idxOrder].Dishes)
            //{
            //    if (item.Value.Id == clientFilter.EndpointOrderItemID) { idxOrderItem = i;  break; }
            //    i++;
            //}
            //// конечное блюдо не найдено - начинаем с начала или с конца коллекции элементов заказа
            //if (idxOrderItem == -1)
            //    idxOrderItem = (clientFilter.LeafDirection == LeafDirectionEnum.Backward) 
            //        ? orderList[idxOrder].Dishes.Count : 0;

            // отдавать по-заказно
            int maxItems = clientFilter.ApproxMaxDishesCountOnPage;
            //  движение назад
            if (clientFilter.LeafDirection == LeafDirectionEnum.Backward)
            {
                int preOrderIdx = idxOrder;
                if ((idxOrder > 0) && (clientFilter.EndpointOrderItemID == 0)) idxOrder--;
                // найти заказы для возврата
                while ((idxOrder >= 0) && (maxItems > 0))
                {
                    maxItems -= orderList[idxOrder].Dishes.Count;
                    idxOrder--;
                }
                // если дошли до начала набора
                if (idxOrder < 0)
                {
                    svcResp.isExistsPrevOrders = false;
                    // то выбираем вперед maxItems элементов с начала набора
                    idxOrder = 0; maxItems = clientFilter.ApproxMaxDishesCountOnPage;
                    while ((idxOrder < orderList.Count) && (maxItems > 0))
                    {
                        maxItems -= orderList[idxOrder].Dishes.Count;
                        idxOrder++;
                    }
                    // остальные - удалить
                    if (idxOrder < (orderList.Count - 1))
                    {
                        orderList.RemoveRange(idxOrder + 1, orderList.Count - idxOrder - 1);
                        svcResp.isExistsNextOrders = true;
                    }
                }
                // иначе, удалить заказы перед idxOrder
                else
                {
                    svcResp.isExistsPrevOrders = true;
                    orderList.RemoveRange(0, idxOrder);
                    // и после preOrderIdx
                    if (preOrderIdx < (orderList.Count - 1))
                    {
                        orderList.RemoveRange(preOrderIdx + 1, orderList.Count - preOrderIdx - 1);
                        svcResp.isExistsNextOrders = true;
                    }
                }
            }

            //  движение вперед
            else
            {
                // сместить индекс вперед, если граничное блюдо - последнее
                if ((clientFilter.LeafDirection == LeafDirectionEnum.Forward)
                    && (orderList[idxOrder].Dishes.Last().Value.Id == clientFilter.EndpointOrderItemID))
                    idxOrder++;

                // удалить заказы перед idxOrder
                if (idxOrder > 0)
                {
                    orderList.RemoveRange(0, idxOrder);
                    svcResp.isExistsPrevOrders = true;
                }
                // найти заказы для возврата
                idxOrder = 0;
                while ((idxOrder < orderList.Count) && (maxItems > 0))
                {
                    maxItems -= orderList[idxOrder].Dishes.Count;
                    idxOrder++;
                }
                // удалить заказы после idxOrder
                if (idxOrder < (orderList.Count - 1))
                {
                    orderList.RemoveRange(idxOrder + 1, orderList.Count - idxOrder - 1);
                    svcResp.isExistsNextOrders = true;
                }
            }

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

        // **** настройки из config-файла сервиса
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
                retval.Add("AutoGotoReadyConfirmPeriod", AppProperties.GetIntProperty("AutoGotoReadyConfirmPeriod"));
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

            getClientInfo(machineName).SetDataFlag = true;

            if (_ordersModel.Orders.ContainsKey(orderId))
            {
                _ordersModel.Orders[orderId].UpdateStatus(orderStatus, true, machineName);
                logMsg += "Ok";
            }
            else
            {
                logMsg += "order not found in the Model";
            }

            getClientInfo(machineName).SetDataFlag = false;

            AppEnv.WriteLogClientAction(machineName, logMsg + " - FINISH" + (DateTime.Now - dtTmr).ToString());
        }

        // обновление статуса блюда с КДСа
        public void ChangeOrderDishStatus(string machineName, int orderId, int orderDishId, OrderStatusEnum orderDishStatus)
        {
            string logMsg = string.Format("ChangeOrderDishStatus(orderId:{0}, dishId:{1}, status:{2}): ", orderId, orderDishId, orderDishStatus);
            DateTime dtTmr = DateTime.Now;
            AppEnv.WriteLogClientAction(machineName, logMsg + " - START");

            getClientInfo(machineName).SetDataFlag = true;

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

            AppEnv.WriteLogClientAction(machineName, logMsg + " - FINISH - " + (DateTime.Now - dtTmr).ToString());

            getClientInfo(machineName).SetDataFlag = false;
        }
        #endregion

        private ClientInfo getClientInfo(string machineName)
        {
            if (_clients.ContainsKey(machineName) == false)
                _clients.Add(machineName, new ClientInfo() { Name = machineName });
            return _clients[machineName];
        }


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
