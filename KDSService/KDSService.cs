using IntegraLib;
using KDSService.Lib;
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
using System.ServiceProcess;


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
        private double _observeInterval = 1000;   // интервал в мсек опроса БД

        // сервис WCF
        private ServiceHost _host;
        public ServiceHost ServiceHost { get { return _host; } }

        // словарь клиентов
        Dictionary<string, ClientInfo> _clients;

        // заказы на стороне службы (с таймерами)
        private OrdersModel _ordersModel;

        private string _sqlServerErrorString;

        // максимальное количество архивных файлов
        private int _maxLogFilesCount = 0;
        // дата последней проверки максимального количества архивных файлов
        private DateTime _lastCheckDateMaxLogFiles = DateTime.MinValue;

        private bool _isUseReadyConfirmed;


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

            msg = AppLib.LoggerInit();
            if (msg != null)
                throw new Exception("Error: " + msg);

            // инициализация приложения
            AppLib.WriteLogInfoMessage("**** НАЧАЛО работы КДС-сервиса ****");

            // информация о файлах и сборках
            AppLib.WriteLogInfoMessage(" - host-файл: '{0}', ver. {1}", AppEnvironment.GetAppFullFile(), AppEnvironment.GetAppVersion());
            ITSAssemblyInfo asmInfo = new ITSAssemblyInfo("KDSService");
            AppLib.WriteLogInfoMessage(" - KDSService lib: '{0}', ver. {1}, changed {2}", asmInfo.FullFileName, asmInfo.Version, asmInfo.DateChanged);
            asmInfo.LoadInfo("IntegraLib");
            AppLib.WriteLogInfoMessage(" - Integra lib: '{0}', ver. {1}", asmInfo.FullFileName, asmInfo.Version);

            AppLib.WriteLogInfoMessage("Инициализация КДС-сервиса...");

            // чтение настроек из config-файла
            isResultOk = AppLib.AppInit(out msg);
            if (!isResultOk)
                throw new Exception("Ошибка инициализации КДС-сервиса: " + msg);

            _maxLogFilesCount = AppProperties.GetIntProperty("MaxLogFiles");
            _isUseReadyConfirmed = AppProperties.GetBoolProperty("UseReadyConfirmedState");

            #region проверка наличия службы MSSQLServer
            // проверка наличия службы MSSQLServer
            if (!MSSQLService.IsExists)
            {
                msg = string.Format("На компьютере ({0}) не установлен MS SQL Server!", Environment.MachineName);
                AppLib.WriteLogErrorMessage(msg);
                throw new Exception(msg);
            }
            //ServiceController sqlService = MSSQLService.Controller;
            string sqlSvcNameFromConfig = (string)AppProperties.GetProperty("MSSQLServiceName");
            // если имя найденного сервиса MS SQL не совпадает с тем, что задано в config-файле, то переопределяем
            if ((sqlSvcNameFromConfig != null) && (MSSQLService.Controller.ServiceName != sqlSvcNameFromConfig))
            {
                AppLib.WriteLogTraceMessage("Имя службы MS SQL Server по умолчанию {0} не совпадает с именем службы, заданной в config-файле ({1}) - используем имя {1}.", MSSQLService.Controller.ServiceName, sqlSvcNameFromConfig);
                if (MSSQLService.FindService(sqlSvcNameFromConfig) == false)
                {
                    msg = string.Format("Имя службы MS SQL Server, заданное в config-файле {0}, не найдено в системе!", sqlSvcNameFromConfig);
                    AppLib.WriteLogErrorMessage(msg);
                    throw new Exception(msg);
                }
            }
            AppLib.WriteLogTraceMessage("Служба {0} находится в состоянии {1}", MSSQLService.Controller.ServiceName, MSSQLService.Status.ToString());
            // попытка перезапуска службы MS SQL Server
            if (AppProperties.GetBoolProperty("MSSQLServiceRestartEnable") 
                && (MSSQLService.Status != ServiceControllerStatus.Running))
            {
                startSQLService();
            }
            #endregion

            // инициализация класса DBContext через статические поля
            DBContext.ConfigConnectionStringName = "KDSEntities";
            // глобальные обработчики DBContext
            DBContext.OnBeforeExecute += new Action<string>(dbBeforeCommandAction);
            DBContext.OnDBErrorAction += new Action<string>(dbErrorAction);
            DBContext.CommandTimeout = (int)AppProperties.GetProperty("MSSQLCommandTimeout");

            // проверить доступность БД
            AppLib.WriteLogInfoMessage("  Проверка доступа к базе данных...");
            AppLib.WriteLogInfoMessage(" - строка подключения: {0}", DBContext.ConnectionString);

            msg = DBContext.CheckDBConnectionAlt();
            if (!msg.IsNull())
            {
                AppLib.WriteLogErrorMessage(msg);
            }
            else
            {
                // проверка уровня совместимости базы данных - должна быть 120 (MS SQL Server 2014)
                checkMSSQLServerCompatibleLevel();

                // проверка справочных таблиц (в классе ModelDicts)
                AppLib.WriteLogInfoMessage("  Проверка наличия справочных таблиц...");
                isResultOk = DBOrderHelper.CheckAppDBTable();
                if (!isResultOk)
                {
                    AppLib.WriteLogErrorMessage("Ошибка проверки справочных таблиц: " + DBOrderHelper.ErrorMessage);
                }
                // получение словарей приложения из БД
                AppLib.WriteLogInfoMessage("  Получение справочных таблиц из БД...");
                isResultOk = ModelDicts.UpdateModelDictsFromDB(out msg);
                if (!isResultOk)
                {
                    AppLib.WriteLogErrorMessage("Ошибка получения словарей из БД: " + msg);
                }
            }


            // периодичность опроса БД - 750 в мсек
            _observeTimer = new Timer(_observeInterval) { AutoReset = false };
            _observeTimer.Elapsed += _observeTimer_Elapsed;

            try
            {
                msg = "  Инициализация внутренней коллекции заказов...";
                _ordersModel = new OrdersModel();
                AppLib.WriteLogInfoMessage(msg + " УСПЕШНО");
            }
            catch (Exception ex)
            {
                AppLib.WriteLogErrorMessage(msg + " ОШИБКА: " + ex.Message);
            }
        }

        // проверка уровня совместимости MS SQL Server
        private void checkMSSQLServerCompatibleLevel()
        {
            AppLib.WriteLogInfoMessage("Проверка уровня совместимости БД MS SQL Server...");

            int cfgCompatibleLevel = AppProperties.GetIntProperty("MSSQLServerCompatibleLevel");
            string cfgCompatibleName = DBContext.getSQLServerNameByCompatibleLevel(cfgCompatibleLevel);
            AppLib.WriteLogInfoMessage(" - из config-файла: {0} ({1})",
                cfgCompatibleLevel.ToString(), cfgCompatibleName);
            if ((cfgCompatibleLevel == 0) || (!DBContext.IsValidCompatibleLevel(cfgCompatibleLevel)))
            {
                AppLib.WriteLogInfoMessage("   ...уровень совместимости не проверяется");
                return;
            }

            int dbCompatibleLevel = DBContext.GetDBCompatibleLevel();
            string dbCompatibleName = DBContext.getSQLServerNameByCompatibleLevel(dbCompatibleLevel);
            string dbName = DBContext.GetDBName();
            AppLib.WriteLogInfoMessage(" - БД '{0}' на сервере '{1}': {2} ({3})", 
                dbName, MSSQLService.Controller.DisplayName,
                dbCompatibleLevel.ToString(), cfgCompatibleName);
            if (dbCompatibleLevel == 0)
            {
                AppLib.WriteLogErrorMessage(DBContext.LastErrorText);
                return;
            }
            // уровни одинаковые - выходим
            if (cfgCompatibleLevel == dbCompatibleLevel) return;

            // попытка изменить уровень совместимости
            AppLib.WriteLogInfoMessage(" - попытка изменить уровень совместимости БД '{0}' на {1} ({2})",
                dbName, cfgCompatibleLevel, cfgCompatibleName);
            // только ПОНИЖАЕМ уровень совместимости
            if (dbCompatibleLevel > cfgCompatibleLevel)  
            {
                if (DBContext.SetDBCompatibleLevel(cfgCompatibleLevel) == false)
                {
                    AppLib.WriteLogErrorMessage(DBContext.LastErrorText);
                    AppLib.WriteLogErrorMessage("Произошла ошибка при изменении уровня совместимости БД. Приложение может работать нестабильно.");
                }
                else
                {
                    AppLib.WriteLogInfoMessage(" - уровень совместимости изменен успешно");
                }
            }
            // повышить нельзя
            else
            {
                AppLib.WriteLogInfoMessage(" - запрещено повышать уровень совместимости");
            }
        }

        private static void startSQLService()
        {
            MSSQLService.Refresh();
            if (MSSQLService.Status == ServiceControllerStatus.Running) return;

            ServiceController sqlService = MSSQLService.Controller;

            int waitCircles = 10, waitSvcActionInterval = 500;
            DateTime dtTmr = DateTime.Now;

            // сначала остановить
            if (MSSQLService.Status != ServiceControllerStatus.Stopped)
            {
                sqlService.Stop();
                sqlService.Refresh();
                // цикл ожидания останова службы
                while (sqlService.Status != ServiceControllerStatus.Stopped)
                {
                    AppLib.WriteLogTraceMessage("    - статус {0}, ждем 0.5 сек...", sqlService.Status.ToString());
                    System.Threading.Thread.Sleep(waitSvcActionInterval);  // задержка в 500 мсек
                    sqlService.Refresh();
                    if ((DateTime.Now - dtTmr).TotalSeconds >= waitCircles)
                    {
                        throw new Exception(string.Format("Истек период в {0} секунд для останова службы MS SQL Server ({1})", waitCircles.ToString(), sqlService.ServiceName));
                    }
                }
                AppLib.WriteLogTraceMessage(" - служба {0} остановлена успешно.", sqlService.ServiceName);
            }

            // запуск службы
            if (MSSQLService.Status != ServiceControllerStatus.Running)
            {
                AppLib.WriteLogTraceMessage(" - запуск службы...");
                sqlService.Start();
                sqlService.Refresh();
                dtTmr = DateTime.Now;
                // цикл ожидания запуска службы
                while (sqlService.Status != ServiceControllerStatus.Running)
                {
                    AppLib.WriteLogTraceMessage("    - статус {0}, ждем 0.5 сек...", sqlService.Status.ToString());
                    System.Threading.Thread.Sleep(waitSvcActionInterval);  // задержка в 500 мсек
                    sqlService.Refresh();
                    if ((DateTime.Now - dtTmr).TotalSeconds >= waitCircles)
                    {
                        throw new Exception(string.Format("Истек период в {0} секунд для запуска службы MS SQL Server ({1})", waitCircles.ToString(), sqlService.ServiceName));
                    }
                }
            }

            AppLib.WriteLogTraceMessage(" - статус службы {0}", sqlService.Status.ToString());
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
                AppLib.WriteLogInfoMessage("Создание канала для приема сообщений...");
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
                AppLib.WriteLogInfoMessage("Создание канала для приема сообщений... Ok");
            }
            catch (Exception ex)
            {
                // исключение записать в лог
                AppLib.WriteLogErrorMessage("Ошибка открытия канала сообщений: {0}{1}\tTrace: {2}", ex.Message, Environment.NewLine, ex.StackTrace);
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
                AppLib.WriteLogInfoMessage(" - host info: address {0}; binding {1}; contract {2}", se.Address, se.Binding.Name, se.Contract.Name);
            }
        }

        // периодический просмотр заказов
        private void _observeTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            StopTimer();
            AppLib.WriteLogOrderDetails("** Elapse DB read timer.");

            // проверка количества архивных файлов
            DateTime dt = DateTime.Now;
            if ((_maxLogFilesCount > 0)
                && ((dt.Minute == 1) && (dt.Second <= 10) && ((dt - _lastCheckDateMaxLogFiles).TotalSeconds > 10)
                    || (_lastCheckDateMaxLogFiles.Equals(DateTime.MinValue)))
                )
            {
                AppLib.WriteLogTraceMessage("Удаление архивных файлов журнала (max {0})...", _maxLogFilesCount);
                List<string> delFileNames = AppEnvironment.CheckLogFilesCount(_maxLogFilesCount);
                if ((delFileNames == null) || (delFileNames.Count() == 0))
                    AppLib.WriteLogTraceMessage(" - удалено файлов: 0");
                else
                    AppLib.WriteLogTraceMessage(" - удалено файлов: {0} ({1})", delFileNames.Count, string.Join("; ", delFileNames));
                _lastCheckDateMaxLogFiles = DateTime.Now;
            }

            // если ни один клиент не читает буфер заказов (_dbOrders), то можно буфер обновить данными из БД
            if (_clients.All(kvp => (kvp.Value.GetOrdersFlag == false)))
            {
                // проверка заблокированных заказов на наличие зависших блокировок
                checkLockedOrders();

                string errDBMsg = _ordersModel.UpdateOrders();

                #region ошибка получения данных от MS SQL Server
                // ошибка получения данных от MS SQL Server
                if (errDBMsg != null)
                {
                    if ((_sqlServerErrorString == null) || (_sqlServerErrorString != errDBMsg))
                        _sqlServerErrorString = errDBMsg;
                    AppLib.WriteLogTraceMessage("mssql|{0}", _sqlServerErrorString);
                    
                    // очистить внутр.набор заказов службы
                    //_ordersModel.Orders.Clear();

                    MSSQLService.Refresh();
                    AppLib.WriteLogTraceMessage("mssql| - статус службы {0} - {1}", MSSQLService.Controller.ServiceName, MSSQLService.Status.ToString());

                    if (AppProperties.GetBoolProperty("MSSQLServiceRestartEnable"))
                    {
                        // если сервис остановлен - пытаемся запустить его
                        if (MSSQLService.Status != ServiceControllerStatus.Running)
                        {
                            AppLib.WriteLogTraceMessage("mssql| - sql server status - {0} - restart service...", MSSQLService.Status.ToString());
                            startSQLService();
                        }
                        // иначе пытаемся 5 раз через 2 секунды прочитать данные из БД и обновить внутр.наборы
                        else
                        {
                            int waitCircles = 5, waitSvcActionInterval = 2000;
                            int i = 0;
                            for (; i < waitCircles; i++)
                            {
                                System.Threading.Thread.Sleep(waitSvcActionInterval);
                                AppLib.WriteLogTraceMessage("mssql|Попытка {0} повторного чтения заказов из БД.", (i + 1).ToString());
                                errDBMsg = _ordersModel.UpdateOrders();
                                if (errDBMsg == null)
                                    break;
                                else
                                    AppLib.WriteLogErrorMessage(errDBMsg);
                            }
                            // sql-служба не ответила - перезапуск
                            if (i >= waitCircles)
                            {
                                DebugTimer.Init();
                                errDBMsg = null;
                                AppLib.WriteLogTraceMessage("mssql|Попытка перезапуска службы MS SQL Server... - START");
                                // попытка перезапуска службы MS SQL Server
                                startSQLService();
                                AppLib.WriteLogTraceMessage("mssql|Попытка перезапуска службы MS SQL Server... - FINISH - {0}{1}",
                                    DebugTimer.GetInterval(),
                                    ((errDBMsg == null) ? "" : " - " + errDBMsg)
                                );
                            }
                        }
                    }
                }
                else
                {
                    if (_sqlServerErrorString != null) _sqlServerErrorString = null;
                }
                #endregion

                // установить стандартный интервал опроса БД
                if (_observeTimer.Interval != _observeInterval) _observeTimer.Interval = _observeInterval; // msec
            }
            // иначе установить минимальный интервал для наискорейшего чтения данных из БД
            //else
            //{
            //    if (_observeTimer.Interval != 50) _observeTimer.Interval = 50; // msec
            //}

            StartTimer();
            AppLib.WriteLogOrderDetails("** Start DB read timer.");
        }

        private void checkLockedOrders()
        {
            int[] lockedOrders = OrderLocker.GetLockedOrders();
            if (lockedOrders.Length == 0) return;

            foreach (int orderId in lockedOrders)
            {
                // если заказ заблокирован более чем 5 сек, то 
                if (OrderLocker.GetTimeOrderLocked(orderId).TotalSeconds > 5d)
                {
                    OrderModel order = _ordersModel.Orders[orderId];
                    // обновить статус заказа статусом всех блюд
                    order.UpdateStatusByVerificationDishes();
                    // и снять блокировку
                    OrderLocker.DelockOrder(orderId);
                }
            }
        }


        // ****  SERVICE CONTRACT  *****
        #region IKDSService inplementation

        public List<OrderStatusModel> GetOrderStatuses(string machineName)
        {
            string logMsg = "GetOrderStatuses(): ";

            List<OrderStatusModel> retVal = DBOrderHelper.GetOrderStatusesList();

            if (retVal != null)
                logMsg += string.Format("Ok ({0} records)", retVal.Count);
            else
                logMsg += DBOrderHelper.ErrorMessage;
            AppLib.WriteLogClientAction(machineName, logMsg);

            return retVal;
        }

        public List<DepartmentModel> GetDepartments(string machineName)
        {
            string logMsg = "GetDepartments(): ";

            List<DepartmentModel> retVal = DBOrderHelper.GetDepartmentsList();

            if (retVal != null)
                logMsg += string.Format("Ok ({0} records)", retVal.Count);
            else
                logMsg += DBOrderHelper.ErrorMessage;
            AppLib.WriteLogClientAction(machineName, logMsg);

            return retVal;
        }


        // *** ЗАПРОС ЗАКАЗОВ ОТ КЛИЕНТОВ ***
        // сюда передаются условия отбора и группировки, также здесь происходить сортировка позиций заказа
        public ServiceResponce GetOrders(string machineName, ClientDataFilter clientFilter)
        {
            ClientInfo curClient = getClientInfo(machineName);
            curClient.IsDishGroupAndSumQuantity = clientFilter.IsDishGroupAndSumQuantity;

            ServiceResponce retVal = new ServiceResponce();

            AppLib.WriteLogClientAction(machineName, "GetOrders('{0}', '{1}')", machineName, clientFilter.ToString());

            // таймер чтения из БД выключен - ждем, пока не закончится чтение данных из БД
            if (_observeTimer.Enabled == false)
            {
                AppLib.WriteLogClientAction(machineName, " - return 0, svc reading data yet...");
                retVal.ServiceErrorMessage = (_sqlServerErrorString.IsNull()) ? "KDS service is reading data..." : _sqlServerErrorString;
                return retVal;
            }
            else if (!_sqlServerErrorString.IsNull())
            {
                retVal.ServiceErrorMessage = _sqlServerErrorString;
                return retVal;
            }

            // "хвосты" по умолчанию - false, набор List<OrderModel> создается в конструкторе ServiceResponce
            List<OrderModel> retValList = retVal.OrdersList;
            DateTime dtTmr = DateTime.Now;

            // временные буферы
            OrderModel validOrder;
            List<OrderDishModel> validDishes = new List<OrderDishModel>();
            // key - блюдо, value - список ингредиентов
            Dictionary<OrderDishModel, List<OrderDishModel>> dishIngr = new Dictionary<OrderDishModel, List<OrderDishModel>>();

            // установить флаг чтения заказов из внутренней коллекции клиентом machineName
            curClient.GetOrdersFlag = true;

            // получить в retVal отфильтрованные заказы
            foreach (OrderModel order in _ordersModel.Orders.Values)
            {
                //    iCnt++;
                //Debug.Print(string.Format("- {0}. обработка Id - {1}", iCnt, order.Id.ToString()));

                // разобрать плоский список блюд и ингр. в иерархический Dish(1)->Ingr(n)
                dishIngr.Clear();
                foreach (OrderDishModel dish in order.Dishes.Values.Where(d => d.ParentUid.IsNull()))
                {
                    List<OrderDishModel> ingrList = new List<OrderDishModel>(order.Dishes.Values.Where(d => (!d.ParentUid.IsNull()) && (d.ParentUid == dish.Uid)));
                    dishIngr.Add(dish, ingrList);
                }

                // собрать в validDishes блюда и ингр., которые проходят проверку
                validDishes.Clear();
                if ((clientFilter.StatusesList != null ) && (clientFilter.DepIDsList != null))
                {
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
                }

                if (validDishes.Count > 0)
                {
                    validOrder = order.Copy();
                    // к заказу добавить правильные блюда
                    foreach (OrderDishModel dish in validDishes)
                    {
                        if (validOrder.Dishes.ContainsKey(dish.Id) == false)
                            validOrder.Dishes.Add(dish.Id, dish.Copy());
                    }

                    // и добавить к результату правильный заказ
                    retValList.Add(validOrder);
                }

            }  // foreach OrderModel

            // сбросить флаг чтения заказов из внутренней коллекции клиентом machineName
            curClient.GetOrdersFlag = false;

            // группировка и сортировка retVal
            if (retValList.Count > 0)
            {
                // группировка по CreateDate блюд может увеличить кол-во заказов
                #region группировка по CreateDate блюд может увеличить кол-во заказов
                if (clientFilter.GroupBy == OrderGroupEnum.ByCreateTime)
                {
                    // разбить заказы по датам (CreateDate) БЛЮД !!!!
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
                    {
                        // установить дату заказов равной дате блюд !!!
                        foreach (KeyValuePair<DateTime, OrderModel> item in sortedOrders)
                        {
                            if (item.Value.CreateDate != item.Key)
                            {
                                item.Value.CreateDate = item.Key;
                            }
                        }
                        retValList = sortedOrders.Values.ToList();
                        retVal.OrdersList = retValList;
                    }
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
                                tmpOrder.Dishes.Add(tmpDish.Id, tmpDish.Copy());
                            // добавить заказ в выходную коллекцию
                            retValList.Add(tmpOrder);
                        }
                    }
                    
                    string sortMode = Convert.ToString(AppProperties.GetProperty("SortOrdersByCreateDate"));
                    // сортировка заказов по убыванию времени заказа
                    if (sortMode == "Desc")
                    {
                        retValList.Sort((om1, om2) => 
                        {
                            if (om1.CreateDate < om2.CreateDate) return 1;
                            else if (om1.CreateDate > om2.CreateDate) return -1;
                            else return 0;
                        }
                        );
                    }
                    // или по возрастанию времени заказа
                    else
                    {
                        retValList.Sort((om1, om2) =>
                        {
                            if (om1.CreateDate < om2.CreateDate) return -1;
                            else if (om1.CreateDate > om2.CreateDate) return 1;
                            else return 0;
                        }
                        );
                    }
                }
                #endregion

                // сортировка по возрастанию номера заказа
                else if (clientFilter.GroupBy == OrderGroupEnum.ByOrderNumber)
                {
                    retValList.Sort((om1, om2) => om1.Number.CompareTo(om2.Number));
                }

                // группировка блюд по OrderId, DepartmentId, DishStatusId, FilingNumber, ParentUid, Comment, CreateDate, UID1C
                if (curClient.IsDishGroupAndSumQuantity)
                {
                    #region IsDishGroupAndSumQuantity == true
                    List<OrderDishModel> dmIngrs;
                    // набор уникальных блюд вместе с набором уникальных ингредиентов для текущего заказа
                    List<Tuple<DishWithQty, List<DishWithQty>>> uniqDishList = new List<Tuple<DishWithQty, List<DishWithQty>>>();
                    // набор уникальных ингредиетов (c кол-вом порций) для текущего блюда
                    List<DishWithQty> uniqIngrList = new List<DishWithQty>();
                    // буфера для удаления повторяющихся блюд/ингредиентов
                    List<int> delDishIds = new List<int>();
                    List<int> delIngrIds = new List<int>();
                    int i = 0;

                    foreach (OrderModel ord in retValList)
                    {
                        if (ord.Dishes.Count <= 1) continue;

                        uniqDishList.Clear();
                        delDishIds.Clear();
                        // цикл по блюдам
                        List<OrderDishModel> dishes = ord.Dishes.Values.Where(d => d.ParentUid.IsNull()).ToList();
                        foreach (OrderDishModel dm in dishes)
                        {
                            // ингредиенты текущего блюда
                            dmIngrs = ord.Dishes.Values.Where(d => (d.ParentUid != null) && (d.ParentUid == dm.Uid)).ToList();  // набор создается, даже если элементы не найдены, Count = 0

                            // сложить одинаковые ингредиенты
                            if (dmIngrs.Count > 0)
                            {
                                #region сложить одинаковые ингредиенты
                                uniqIngrList.Clear();  // набор, содержащий уникальные ингредиенты данного блюда
                                delIngrIds.Clear();
                                // цикл по ингр.текущего блюда
                                foreach (OrderDishModel dmIngr in dmIngrs)
                                {
                                    i = 0;
                                    for (; i < uniqIngrList.Count; i++)
                                    {
                                        if (compareIngredientsForGroup(uniqIngrList[i].DishModel, dmIngr)) break;
                                    }
                                    // ингр.не найден - добавить в набор уникальных ингредиентов
                                    if (i == uniqIngrList.Count)
                                    {
                                        uniqIngrList.Add(new DishWithQty(dmIngr));
                                    }
                                    // ингр.найден - складываем кол-во порций
                                    else
                                    {
                                        uniqIngrList[i].Quantity += dmIngr.Quantity;
                                        uniqIngrList[i].AddGroupedId(dmIngr.Id);
                                        delIngrIds.Add(dmIngr.Id);
                                    }
                                }

                                // если есть одинаковые ингредиенты, то удалить их из набора блюд заказа и dmIngrs
                                if (delIngrIds.Count > 0)
                                {
                                    delIngrIds.ForEach(id =>
                                        {
                                            ord.Dishes.Remove(id);
                                            dmIngrs.RemoveAll(dmI => dmI.Id == id);
                                        });
                                    delIngrIds.Clear();
                                }

                                // перенести количество в retValList
                                if (uniqIngrList.Count > 0)
                                {
                                    uniqIngrList.ForEach(uniqDish =>
                                        {
                                            i = uniqDish.DishModel.Id;
                                            if (ord.Dishes[i].Quantity != uniqDish.Quantity)
                                                ord.Dishes[i].Quantity = uniqDish.Quantity;
                                            ord.Dishes[i].GroupedDishIds = uniqDish.GetGroupedIds();
                                        });
                                    uniqIngrList.Clear();
                                }

                                // получить заново набор уникальных ингредиентов
                                dmIngrs = ord.Dishes.Values.Where(d => (d.ParentUid != null) && (d.ParentUid == dm.Uid)).ToList();
                                #endregion
                            }

                            // поиск dm в uniqDishList
                            for (i = 0; i < uniqDishList.Count; i++)
                            {
                                if (compareDishesWithIngredients(uniqDishList[i].Item1, uniqDishList[i].Item2, dm, dmIngrs)) break;
                            }
                            // блюдо не найдено - добавить
                            if (i == uniqDishList.Count)
                            {
                                uniqDishList.Add(new Tuple<DishWithQty, List<DishWithQty>>(
                                    new DishWithQty(dm),
                                    dmIngrs.Select(ingr => new DishWithQty(ingr)).ToList()));
                            }
                            // блюдо найдено - сложить количество (в retValList) и удалить блюдо dm с его ингредиентами dmIngrs из ord.Dishes
                            else
                            {
                                // порции блюда
                                uniqDishList[i].Item1.Quantity += dm.Quantity;
                                uniqDishList[i].Item1.AddGroupedId(dm.Id);

                                // порции ингредиентов
                                for (int j = 0; j < uniqDishList[i].Item2.Count; j++)
                                {
                                    uniqDishList[i].Item2[j].Quantity += dmIngrs[j].Quantity;
                                    uniqDishList[i].Item2[j].AddGroupedId(dmIngrs[j].Id);
                                    delDishIds.Add(dmIngrs[j].Id);
                                }
                                delDishIds.Add(dm.Id);
                            }
                        }

                        // если есть одинаковые блюда, то удалить их из набора блюд заказа
                        if (delDishIds.Count > 0)
                        {
                            delDishIds.ForEach(id => ord.Dishes.Remove(id));
                            delDishIds.Clear();
                        }

                        // перенести количество в retValList
                        if (uniqDishList.Count > 0)
                        {
                            uniqDishList.ForEach(uniqDish =>
                            {
                                // блюдо
                                i = uniqDish.Item1.DishModel.Id;
                                if (ord.Dishes[i].Quantity != uniqDish.Item1.Quantity)
                                    ord.Dishes[i].Quantity = uniqDish.Item1.Quantity;
                                ord.Dishes[i].GroupedDishIds = uniqDish.Item1.GetGroupedIds();

                                // ингредиенты
                                uniqDish.Item2.ForEach(uniqIngr =>
                                {
                                    i = uniqIngr.DishModel.Id;
                                    if (ord.Dishes[i].Quantity != uniqIngr.Quantity)
                                        ord.Dishes[i].Quantity = uniqIngr.Quantity;
                                    ord.Dishes[i].GroupedDishIds = uniqIngr.GetGroupedIds();
                                });
                            });
                            uniqDishList.Clear();
                        }
                    }
                    #endregion
                }
                else
                {
                    #region IsDishGroupAndSumQuantity == false
                    foreach (OrderModel ord in retValList)
                    {
                        if (ord.Dishes.Count <= 1) continue;
                        foreach (OrderDishModel dm in ord.Dishes.Values)
                        {
                            if (dm.GroupedDishIds != null) dm.GroupedDishIds = null;
                        }
                    }
                    #endregion
                }

                // сортировка блюд в заказах по номеру подачи
                Dictionary<int, OrderDishModel> sortedDishes;
                foreach (OrderModel order in retValList)
                {
                    sortedDishes = (from dish in order.Dishes.Values orderby dish.FilingNumber select dish).ToDictionary(d => d.Id);
                    order.Dishes = sortedDishes;
                }

                // если появились новые заказы, то передать клиенту заказы с самого первого
                List<OrderModel> newOrdersList = curClient.IsAppearNewOrder(retValList);
                if ((newOrdersList != null) && (newOrdersList.Count > 0))
                {
                    // передать клиенту перечень Id новых заказов через ;
                    retVal.NewOrderIds = string.Join(";", newOrdersList.Select(o => o.Id.ToString()));
                //    clientFilter.EndpointOrderID = retValList[0].Id;
                //    clientFilter.EndpointOrderItemID = retValList[0].Dishes.First().Value.Id;
                }

                string ids = (retValList.Count > 50) ? "> 50" : getOrdersLogString(retValList);
                AppLib.WriteLogTraceMessage(" - orders for client ({0}): {1}", retValList.Count, ids);
                // ограничение количества отдаваемых клиенту объектов
                if (
                    (clientFilter.EndpointOrderID > 0) 
                    || (clientFilter.EndpointOrderItemID > 0)
                    || (clientFilter.ApproxMaxDishesCountOnPage > 0)
                    )
                {
                    limitOrderItems(retVal, clientFilter);

                    ids = (retValList.Count > 50) ? "> 50" : getOrdersLogString(retValList);
                    AppLib.WriteLogTraceMessage(" - limit orders for client({0}): {1}", retValList.Count, ids);
                }

            }  // if (retVal.Count > 0)

            AppLib.WriteLogClientAction(machineName, " - result: {0} orders - {1}", retValList.Count, (DateTime.Now - dtTmr).ToString());
            return retVal;
        }

        // сравнение только блюд, НЕ ингредиентов, т.е. ParentUid = null
        private bool compareDishesForGroup(OrderDishModel dm1, OrderDishModel dm2)
        {
            return (dm1.DepartmentId == dm2.DepartmentId)
                && (dm1.DishStatusId == dm2.DishStatusId)
                && (dm1.FilingNumber == dm2.FilingNumber)
                && (dm1.Comment == dm2.Comment)
                && (dm1.CreateDate == dm2.CreateDate)
                && (dm1.UID1C == dm2.UID1C);
        }
        // сравнение ингредиентов (ParentUid != null)
        private bool compareIngredientsForGroup(OrderDishModel ingr1, OrderDishModel ingr2)
        {
            return (ingr1.DepartmentId == ingr2.DepartmentId)
                && (ingr1.DishStatusId == ingr2.DishStatusId)
                && (ingr1.FilingNumber == ingr2.FilingNumber)
                && (ingr1.Comment == ingr2.Comment)
                && (ingr1.CreateDate == ingr2.CreateDate)
                && (ingr1.UID1C == ingr2.UID1C);
        }
        private bool compareDishesWithIngredients(
            DishWithQty dish1, List<DishWithQty> dish1Ingrs, 
            OrderDishModel dish2, List<OrderDishModel> dish2Ingrs)
        {
            bool retVal = false;
            if (compareDishesForGroup(dish1.DishModel, dish2))
            {
                if ((dish1Ingrs.Count == 0) && (dish2Ingrs.Count == 0))
                    retVal = true;
                else if (dish1Ingrs.Count == dish2Ingrs.Count)
                {
                    retVal = true;
                    for (int i = 0; i < dish1Ingrs.Count; i++)
                    {
                        if (compareIngredientsForGroup(dish1Ingrs[i].DishModel, dish2Ingrs[i]) == false)
                        {
                            retVal = false; break;
                        }
                    }
                }
            }

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

        private string getOrdersLogString(List<OrderModel> orders)
        {
            return string.Join(",", 
                orders.Select(o => 
                    string.Format("{0}/{1}/{2}", o.Id.ToString(), o.Number.ToString(), o.Dishes.Count.ToString()))
                );
        }

        // ограничение количества отдаваемых клиенту объектов
        private void limitOrderItems(ServiceResponce svcResp, ClientDataFilter clientFilter)
        {
            int idxOrder = -1;
            List<OrderModel> orderList = svcResp.OrdersList;

            if (svcResp.NewOrderIds.IsNull() == false)
            {
                idxOrder = 0;
            }
            // найти индекс элемента коллекции заказов, 
            // у которого order.Id==clientFilter.OrderId && dish.Id==clientFilter.DishId
            // элементов order.Id==clientFilter.OrderId может быть несколько при группировке по времени
            else if ((clientFilter.EndpointOrderID > 0) && (clientFilter.EndpointOrderItemID > 0))
            {
                idxOrder = orderList.FindIndex(om =>
                (om.Id == clientFilter.EndpointOrderID) && (om.Dishes.Any(kvp => kvp.Value.Id == clientFilter.EndpointOrderItemID)));
                AppLib.WriteLogTraceMessage("   - limit from OrderId={0} & OrderDishId={1}, order {2}", clientFilter.EndpointOrderID, clientFilter.EndpointOrderItemID, ((idxOrder==-1)?"NOT found":"FOUND"));
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

            //  движение вперед или по месту
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
            if ((clientStatusIDs == null) || (clientDepIDs == null))
                return false;
            else
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
                retval.Add("IsReadTakenDishes", AppProperties.GetBoolProperty("IsReadTakenDishes"));
                retval.Add("UseReadyConfirmedState", AppProperties.GetBoolProperty("UseReadyConfirmedState"));
                retval.Add("AutoGotoReadyConfirmPeriod", AppProperties.GetIntProperty("AutoGotoReadyConfirmPeriod"));
                retval.Add("TakeCancelledInAutostartCooking", AppProperties.GetBoolProperty("TakeCancelledInAutostartCooking"));
                retval.Add("TimeOfAutoCloseYesterdayOrders", ts1.ToString());
                retval.Add("UnusedDepartments", s2);

                // настройки файлов-уведомлений
                /*
  <add key="NoticeOrdermanFeature" value="1"/>
  <add key="NoticeOrdermanFolder" value="c:\Users\Leschenko.V\Documents\Visual Studio 2015\Projects\Integra OrdermanServer\bin\Release distrib\OmanResources\Export\KDS"/>
  <add key="NoticeOrdermanDishNotice" value="1"/>
                 */
                retval.Add("NoticeOrdermanFeature", AppProperties.GetBoolProperty("NoticeOrdermanFeature"));
                retval.Add("NoticeOrdermanFolder", AppProperties.GetProperty("NoticeOrdermanFolder"));
                retval.Add("NoticeOrdermanDishNotice", AppProperties.GetBoolProperty("NoticeOrdermanDishNotice"));

                logMsg += "Ok";
            }
            catch (Exception ex)
            {
                logMsg += ex.Message;
            }
            AppLib.WriteLogClientAction(machineName, logMsg);

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
            AppLib.WriteLogClientAction(machineName, logMsg);
        }

        #endregion

        #region IKDSCommandService implementation
        // заблокировать заказ от изменения по таймеру
        public bool LockOrder(string machineName, int orderId)
        {
            string logMsg = string.Format("LockOrder({0}): ", orderId);
            bool retVal = OrderLocker.LockOrder(orderId);

            if (retVal)
                logMsg += "Ok";
            else
                logMsg += "Error: " + OrderLocker.ErrMsg;
            AppLib.WriteLogClientAction(machineName, logMsg);

            return retVal;
        }
        // разблокировать заказ от изменения по таймеру
        public bool DelockOrder(string machineName, int orderId)
        {
            string logMsg = string.Format("DelockOrder({0}): ", orderId);
            bool retVal = OrderLocker.DelockOrder(orderId);

            if (retVal)
                logMsg += "Ok";
            else
                logMsg += "Error: " + OrderLocker.ErrMsg;
            AppLib.WriteLogClientAction(machineName, logMsg);

            return retVal;
        }

        // заблокировать блюдо от изменения по таймеру
        public bool LockDish(string machineName, int dishId)
        {
            string logMsg = string.Format("LockDish({0}): ", dishId);
            bool retVal = OrderLocker.LockDish(dishId);

            if (retVal)
                logMsg += "Ok";
            else
                logMsg += "Error: " + OrderLocker.ErrMsg;
            AppLib.WriteLogClientAction(machineName, logMsg);

            return retVal;
        }
        // разблокировать блюдо от изменения по таймеру
        public bool DelockDish(string machineName, int dishId)
        {
            string logMsg = string.Format("DelockDish({0}): ", dishId);
            bool retVal = OrderLocker.DelockDish(dishId);

            if (retVal)
                logMsg += "Ok";
            else
                logMsg += "Error: " + OrderLocker.ErrMsg;
            AppLib.WriteLogClientAction(machineName, logMsg);

            return retVal;
        }

        // обновление статуса заказа с КДСа
        public bool ChangeOrderStatus(string machineName, int orderId, int statusId)
        {
            bool retVal = false;
            OrderStatusEnum orderStatus = (OrderStatusEnum)statusId;
            string logMsg = string.Format("ChangeOrderStatus({0}, {1}): ", orderId, orderStatus);
            DateTime dtTmr = DateTime.Now;
            AppLib.WriteLogClientAction(machineName, logMsg + " - START");

            getClientInfo(machineName).SetDataFlag = true;

            if (_ordersModel.Orders.ContainsKey(orderId))
            {
                OrderModel order = _ordersModel.Orders[orderId];
                try
                {
                    retVal = order.UpdateStatus(orderStatus, true, machineName);
                    logMsg += (retVal ? "Ok" : "Not changed");
                }
                catch (Exception ex)
                {
                    logMsg += "Error - " + ex.Message;
                }
            }
            else
            {
                logMsg += $"order id {orderId} not found in the Model";
            }

            getClientInfo(machineName).SetDataFlag = false;
            AppLib.WriteLogClientAction(machineName, logMsg + " - FINISH" + (DateTime.Now - dtTmr).ToString());

            return retVal;
        }

        // обновление статуса блюда с КДСа
        public bool ChangeOrderDishStatus(string machineName, int orderId, int orderDishId, int dishStatusId)
        {
            bool retVal = false;
            OrderStatusEnum dishStatus = (OrderStatusEnum)dishStatusId;
            string logMsg = string.Format("ChangeOrderDishStatus(orderId:{0}, dishId:{1}, status:{2}): ", orderId, orderDishId, dishStatus);
            DateTime dtTmr = DateTime.Now;
            AppLib.WriteLogClientAction(machineName, logMsg + " - START");

            getClientInfo(machineName).SetDataFlag = true;

            if (_ordersModel.Orders.ContainsKey(orderId))
            {
                OrderModel modelOrder = _ordersModel.Orders[orderId];
                if (modelOrder.Dishes.ContainsKey(orderDishId))
                {
                    OrderDishModel modelDish = modelOrder.Dishes[orderDishId];
                    try
                    {
                        retVal = modelDish.UpdateStatus(dishStatus, machineName: machineName);
                        logMsg += (retVal ? "Ok" : "Not changed");
                    }
                    catch (Exception ex)
                    {
                        logMsg += "Error - " + ex.Message;
                    }
                }
                else
                {
                    logMsg += $"dish id {orderDishId} not found in the Order id {orderId}";
                }
            }
            else
            {
                logMsg += $"order id {orderId} not found in the Model";
            }

            AppLib.WriteLogClientAction(machineName, logMsg + " - FINISH - " + (DateTime.Now - dtTmr).ToString());
            getClientInfo(machineName).SetDataFlag = false;

            return retVal;
        }
        #endregion

        private ClientInfo getClientInfo(string machineName)
        {
            if (_clients.ContainsKey(machineName) == false)
                _clients.Add(machineName, new ClientInfo() { Name = machineName });
            return _clients[machineName];
        }

        private static void dbBeforeCommandAction(string sqlText)
        {
            if (AppProperties.GetBoolProperty("TraceQueryToMSSQL"))
            {
                AppLib.WriteLogTraceMessage("DBContext| " + sqlText);
            }
        }

        private static void dbErrorAction(string errMsg)
        {
            AppLib.WriteLogErrorMessage("DBContext error: " + errMsg);
        }


        public void Dispose()
        {
            AppLib.WriteLogTraceMessage("Закрытие служебного класса KDSService...");
            // таймер остановить, отписаться от события и уничтожить
            StopTimer();
            _observeTimer.Dispose();
            MSSQLService.Dispose();

            AppLib.WriteLogTraceMessage("   close ServiceHost...");
            if (_host != null)
            {
                try
                {
                    _host.Close(); _host = null;
                }
                catch (Exception ex)
                {
                    AppLib.WriteLogErrorMessage("   Error: " + ex.ToString());
                }
            }

            AppLib.WriteLogTraceMessage("   clear inner Orders collection...");
            if (_ordersModel != null)
            {
                try
                {
                    _ordersModel.Dispose();
                }
                catch (Exception ex)
                {
                    AppLib.WriteLogErrorMessage("   Error: " + ex.ToString());
                }
            }

            AppLib.WriteLogInfoMessage("**** ЗАВЕРШЕНИЕ работы КДС-сервиса ****");
        }


    }  // class
}
