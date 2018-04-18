using KDSWPFClient.ServiceReference1;
using KDSWPFClient.Lib;
using KDSWPFClient.ViewModel;
using IntegraLib;
using IntegraWPFLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using System.Diagnostics;
using System.Windows;
using System.Configuration;
using System.Threading;

namespace KDSWPFClient
{
    public class AppDataProvider: IDisposable
    {
        Random rnd = new Random();

        private readonly string _machineName = Environment.MachineName;
        private KDSServiceClient _getClient = null;
        private KDSCommandServiceClient _setClient = null;
        int openTimeoutSeconds = (int)WpfHelper.GetAppGlobalValue("OpenTimeoutSeconds", 1);

        public bool IsGetServiceData {
            get
            {
                return ((_ordStatuses.Count > 0) && (_deps.Count > 0));
            }
        }

        // **** СЛОВАРИ
        //   статусов
        private Dictionary<int, OrderStatusViewModel> _ordStatuses;
        public Dictionary<int, OrderStatusViewModel> OrderStatuses { get { return _ordStatuses; } }
        //   отделов
        private Dictionary<int, DepartmentViewModel> _deps;
        public Dictionary<int, DepartmentViewModel> Departments { get { return _deps; } }

        private string _errMsg;
        public string ErrorMessage { get { return _errMsg; } }

        public bool EnableGetChannel { get
            { return (_getClient != null)
                    && ((_getClient.State == CommunicationState.Created) || (_getClient.State== CommunicationState.Opened));
            }
        }
        public bool EnableSetChannel
        {
            get
            {
                return (_setClient != null)
                      && ((_setClient.State == CommunicationState.Created) || (_setClient.State == CommunicationState.Opened));
            }
        }


        public AppDataProvider()
        {
            _ordStatuses = new Dictionary<int, OrderStatusViewModel>();
            _deps = new Dictionary<int, DepartmentViewModel>();
            _machineName += "." + App.ClientName;
        }

        public bool CreateGetChannel()
        {
            bool retVal = false;

            _errMsg = null;
            try
            {
                if ((_getClient != null) && (_getClient.State != CommunicationState.Faulted)) _getClient.Close();

                // 2017-10-04 вместо config-файла, создавать биндинги в коде, настройки брать из appSettings
                NetTcpBinding getBinding = new NetTcpBinding(SecurityMode.None, false);
                setBindingBuffers(getBinding);
                getBinding.OpenTimeout = new TimeSpan(0, 0, openTimeoutSeconds);

                string hostName = (string)WpfHelper.GetAppGlobalValue("KDSServiceHostName", "");
                if (hostName.IsNull()) throw new Exception("В файле AppSettings.config не указано имя хоста КДС-службы, проверьте наличие ключа KDSServiceHostName");
                string addr = string.Format("net.tcp://{0}:8733/KDSService", hostName);
                EndpointAddress getEndpointAddress = new EndpointAddress(addr);

                _getClient = new KDSServiceClient(getBinding, getEndpointAddress);
                // для отладки службы из-под клиента увеличить время операции
                _getClient.InnerChannel.OperationTimeout = new TimeSpan(0,20,0);

                //_getClient.Open();
                logClientInfo(_getClient);

                retVal = true;
            }
            catch (Exception ex)
            {
                _errMsg = ex.Message;
                throw;
            }

            return retVal;
        }

        private void setBindingBuffers(NetTcpBinding getBinding)
        {
            // set max buffer size
            int maxIntValue = int.MaxValue;
            getBinding.MaxBufferSize = maxIntValue;
            getBinding.MaxReceivedMessageSize = maxIntValue;
            /*
            XmlDictionaryReaderQuotas myReaderQuotas = new XmlDictionaryReaderQuotas(); myReaderQuotas.MaxStringContentLength = 2147483647; myReaderQuotas.MaxArrayLength = 2147483647; myReaderQuotas.MaxBytesPerRead = 2147483647; myReaderQuotas.MaxDepth = 64; myReaderQuotas.MaxNameTableCharCount = 2147483647; binding.GetType().GetProperty("ReaderQuotas").SetValue(bindi‌​ng, myReaderQuotas, null);
             */
            System.Xml.XmlDictionaryReaderQuotas readQuotas = getBinding.ReaderQuotas;
            readQuotas.MaxArrayLength = 1048576;
            readQuotas.MaxBytesPerRead = 1048576;
            readQuotas.MaxDepth = 1048576;
            readQuotas.MaxNameTableCharCount = 1048576;
            readQuotas.MaxStringContentLength = maxIntValue;
        }

        public bool CreateSetChannel()
        {
            bool retVal = false;

            _errMsg = null;
            try
            {
                if ((_setClient != null) && (_setClient.State != CommunicationState.Faulted)) _setClient.Close();

                // 2017-10-04 вместо config-файла, создавать биндинги в коде, настройки брать из appSettings
                NetTcpBinding setBinding = new NetTcpBinding(SecurityMode.None, true);
                setBinding.OpenTimeout = new TimeSpan(0, 0, openTimeoutSeconds);
                setBinding.ReceiveTimeout = new TimeSpan(5,0,0);
                setBinding.ReliableSession.InactivityTimeout = new TimeSpan(5, 0, 0);
                string hostName = (string)WpfHelper.GetAppGlobalValue("KDSServiceHostName", "");
                if (hostName.IsNull()) throw new Exception("В файле AppSettings.config не указано имя хоста КДС-службы, проверьте наличие ключа KDSServiceHostName");
                string addr = string.Format("net.tcp://{0}:8734/KDSCommandService", hostName);
                EndpointAddress setEndpointAddress = new EndpointAddress(addr);

                _setClient = new KDSCommandServiceClient(setBinding, setEndpointAddress);

                //_setClient.Open();
                logClientInfo(_setClient);

                retVal = true;
            }
            catch (Exception ex)
            {
                _errMsg = ex.Message;
                throw;
            }

            return retVal;
        }

        private void logClientInfo<T>(System.ServiceModel.ClientBase<T> client) where T: class
        {
            System.ServiceModel.Description.ServiceEndpoint se = client.Endpoint;
            AppLib.WriteLogInfoMessage("Client Info: type: {0}, address {1}; binding: {2}; contract: {3}",typeof(T).Name,  se.Address, se.Binding.Name, se.Contract.Name);
        }


        #region get dictionaries from service
        public bool SetDictDataFromService()
        {
            if ((_ordStatuses.Count > 0) && (_deps.Count > 0)) return true;

            bool retVal = false;
            try
            {
                if (this.EnableGetChannel == false) CreateGetChannel();

                // получить со службы статусы заказов и сохранить их в _ordStatuses
                AppLib.WriteLogInfoMessage("  - clt: получаю словарь статусов от службы...");
                setOrderStatusFromService();

                // получить отделы со службы и сохранить их в _deps
                AppLib.WriteLogInfoMessage("  - clt: получаю словарь отделов от службы...");
                setDepartmentsFromService();

                // прочитать из конфига отделы для установки флажка IsViewOnKDS
                string sBuf = ConfigurationManager.AppSettings["depUIDs"];
                if (sBuf != null)
                {
                    string[] cfgDepUIDs = sBuf.Split(',');
                    DepartmentViewModel curDep;
                    foreach (string id in cfgDepUIDs)
                    {
                        curDep = _deps.Values.FirstOrDefault(d => d.Id.ToString() == id);
                        if (curDep != null) curDep.IsViewOnKDS = true;
                    }
                }

                // *** ПРОЧИЕ НАСТРОЙКИ ОТ СЛУЖБЫ
                AppLib.WriteLogInfoMessage("  - получаю настройки от КДС-службы...");
                sBuf = "";
                Dictionary<string, object> hostAppSettings = GetHostAppSettings();
                if (hostAppSettings != null)
                {
                    string s1;
                    foreach (KeyValuePair<string, object> pair in hostAppSettings)
                    {
                        WpfHelper.SetAppGlobalValue(pair.Key, pair.Value);
                        if (sBuf.Length > 0) sBuf += "; ";
                        sBuf += string.Format("{0}: {1}", pair.Key, pair.Value);
                    }

                    // получить и преобразовать сложные типы из строк
                    //    TimeSpan
                    s1 = (string)WpfHelper.GetAppGlobalValue("TimeOfAutoCloseYesterdayOrders");
                    if (!s1.IsNull()) WpfHelper.SetAppGlobalValue("TimeOfAutoCloseYesterdayOrders", TimeSpan.Parse(s1));

                    //    HashSet<int>
                    s1 = (string)WpfHelper.GetAppGlobalValue("UnusedDepartments");
                    if (!s1.IsNull())
                    {
                        int[] iArr = s1.Split(',').Select(s => s.ToInt()).ToArray();
                        List<int> hsInt = new List<int>(iArr);
                        WpfHelper.SetAppGlobalValue("UnusedDepartments", hsInt);
                    }
                }
                AppLib.WriteLogInfoMessage("  - получено: " + sBuf);

                retVal = true;
            }
            catch (Exception ex)
            {
                _errMsg = ErrorHelper.GetShortErrMessage(ex);
            }

            return retVal;
        }
        private void setOrderStatusFromService()
        {
            _ordStatuses.Clear();
            try
            {
                List<OrderStatusModel> svcList = _getClient.GetOrderStatuses(_machineName);
                svcList.ForEach((OrderStatusModel o) => _ordStatuses.Add(o.Id,
                    new OrderStatusViewModel() { Id = o.Id, Name = o.Name, AppName = o.AppName, Description = o.Description }
                    ));
            }
            catch (Exception)
            {
                throw;
            }
        }

        private void setDepartmentsFromService()
        {
            _deps.Clear();
            try
            {
                List<DepartmentModel> svcDict = _getClient.GetDepartments(_machineName);
                foreach (DepartmentModel dep in svcDict)
                {
                    DepartmentViewModel newDep = new DepartmentViewModel()
                    {
                        Id = dep.Id, Name = dep.Name, UID = dep.UID,
                        DishQuantity = dep.DishQuantity,
                        IsAutoStart = dep.IsAutoStart,
                        IsViewOnKDS = false
                    };


                    _deps.Add(dep.Id, newDep);
                }
            }
            catch (Exception ex)
            {
                _errMsg = ErrorHelper.GetShortErrMessage(ex);
            }
        }
        #endregion

        public ServiceResponce GetOrders(ClientDataFilter clientFilter)
        {
            ServiceResponce retVal = null;
            try
            {
                retVal = _getClient.GetOrders(_machineName, clientFilter);
            }
            catch (Exception)
            {
            }

            return retVal;
        }

        /// <summary>
        /// Calculates the lenght in bytes of an object 
        /// and returns the size 
        /// </summary>
        /// <param name="TestObject"></param>
        /// <returns></returns>
        private long GetObjectSize(object TestObject)
        {
            long retVal = 0;
            using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
            {
                System.Runtime.Serialization.Formatters.Binary.BinaryFormatter bf = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                bf.Serialize(ms, TestObject);
                retVal = ms.Length;
            }
            return retVal;
        }


        #region get app dict item
        public OrderStatusViewModel GetOrderStatusModelById(int statusId)
        {
            if (_ordStatuses.ContainsKey(statusId)) return _ordStatuses[statusId];
            return null;
        }

        public DepartmentViewModel GetDepartmentById(int depId)
        {
            if (_deps.ContainsKey(depId)) return _deps[depId];
            return null;
        }

        public string GetDepNames()
        {
            StringBuilder sb = new StringBuilder();
            foreach (DepartmentViewModel item in _deps.Values)
            {
                if (item.IsViewOnKDS)
                {
                    if (sb.Length > 0) sb.Append("; ");
                    sb.Append(item.Name);
                }
            }
            
            return sb.ToString();
        }

        // *** communication object methods WRAPPERs
        public Dictionary<string, object> GetHostAppSettings()
        {
            Dictionary<string, object> retVal = null;
            try
            {
                retVal = _getClient.GetHostAppSettings(_machineName);
            }
            catch (Exception ex)
            {
                _errMsg = ErrorHelper.GetShortErrMessage(ex);
                AppLib.WriteLogErrorMessage(_errMsg);
            }
            return retVal;
        }


        public void SetExpectedTakeValue(int value)
        {
            try
            {
                _getClient.SetExpectedTakeValue(_machineName, value);
                WpfHelper.SetAppGlobalValue("ExpectedTakeValue", value);
            }
            catch (Exception ex)
            {
                _errMsg = ErrorHelper.GetShortErrMessage(ex);
                AppLib.WriteLogErrorMessage(_errMsg);
            }
        }

        #endregion

        #region изменение статуса ЗАКАЗА
        public bool LockOrder(int orderId)
        {
            if (_setClient == null) return false;

            bool retVal = false;
            try
            {
                checkSvcState();
                retVal = _setClient.LockOrder(_machineName, orderId);
            }
            catch (Exception)
            {
            }
            return retVal;
        }
        public bool DelockOrder(int orderId)
        {
            if (_setClient == null) return false;

            bool retVal = false;
            try
            {
                checkSvcState();
                retVal = _setClient.DelockOrder(_machineName, orderId);
            }
            catch (Exception)
            {
            }
            return retVal;
        }

        public void SetNewOrderStatus(int orderId, OrderStatusEnum newStatus)
        {
            if (_setClient == null) return;

            AppLib.WriteLogClientAction("Установить статус ЗАКАЗА, состояние службы: {0}", _getClient.State);
            checkSvcState();

            try
            {
                DateTime dtTmr = DateTime.Now;
                AppLib.WriteLogClientAction(" - svc.ChangeOrderStatus({0}, {1}) - START", orderId, newStatus);

                _setClient.ChangeOrderStatus(_machineName, orderId, (int)newStatus);

                AppLib.WriteLogClientAction(" - svc.ChangeOrderStatus({0}, {1}) - FINISH - {2}", orderId, newStatus, (DateTime.Now - dtTmr).ToString());
            }
            catch (Exception)
            {
                throw;
            }
        }
        #endregion

        #region изменение статуса БЛЮДА
        public void LockDish(int dishId)
        {
            if (_setClient == null) return;
            checkSvcState();
            try
            {
                _setClient.LockDish(_machineName, dishId);
            }
            catch (Exception)
            {
                throw;
            }
        }
        public void DelockDish(int dishId)
        {
            if (_setClient == null) return;
            checkSvcState();
            try
            {
                _setClient.DelockDish(_machineName, dishId);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public bool SetNewDishStatus(int orderId, int dishId, OrderStatusEnum newStatus)
        {
            if (_setClient == null) return false;

            AppLib.WriteLogClientAction("Установить статус БЛЮДА, состояние службы: {0}", _getClient.State);
            bool retVal = false;
            try
            {
                checkSvcState();
                DateTime dtTmr = DateTime.Now;
                AppLib.WriteLogClientAction(" - svc.ChangeOrderDishStatus({0}, {1}, {2}) - START", orderId, dishId, newStatus);

                retVal = _setClient.ChangeOrderDishStatus(_machineName, orderId, dishId, (int)newStatus);

                AppLib.WriteLogClientAction(" - svc.ChangeOrderDishStatus({0}, {1}, {2}) - FINISH - {3}", orderId, dishId, newStatus, (DateTime.Now - dtTmr).ToString());
                retVal = true;
            }
            catch (Exception)
            {
                throw;
            }

            return retVal;
        }
        #endregion

        #region создание файлов-уведомлений
        public bool CreateNoticeFileForOrder(int orderId, string dishIdsStr)
        {
            if (_setClient == null) return false;

            bool retVal = false;
            try
            {
                checkSvcState();
                retVal = _setClient.CreateNoticeFileForOrder(_machineName, orderId, dishIdsStr);
            }
            catch (Exception)
            {
            }
            return retVal;
        }

        public bool CreateNoticeFileForDish(int orderId, int orderDishId)
        {
            if (_setClient == null) return false;

            bool retVal = false;
            try
            {
                checkSvcState();
                retVal = _setClient.CreateNoticeFileForDish(_machineName, orderId, orderDishId);
            }
            catch (Exception)
            {
            }
            return retVal;
        }

        #endregion

        private void checkSvcState()
        {
            if (_setClient.State == CommunicationState.Faulted)
            {
                AppLib.WriteLogClientAction(" - restart KDSCommandServiceClient !!!");
                _setClient.Close();
                _setClient = new KDSCommandServiceClient();
            }
        }

        public void Dispose()
        {
            disposeServiceClient(_getClient);
            disposeServiceClient(_setClient);

            if (_ordStatuses != null) { _ordStatuses.Clear(); _ordStatuses = null; }
            if (_deps != null) { _deps.Clear(); _deps = null; }
        }

        private void disposeServiceClient(System.ServiceModel.ICommunicationObject client)
        {
            if (client != null)
            {
                try
                {
                    if (client.State == System.ServiceModel.CommunicationState.Opened) client.Close();
                }
                catch (Exception)
                {
                }
                client = null;
            }
        }
    }  // class
}
