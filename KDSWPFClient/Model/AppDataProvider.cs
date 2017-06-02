using KDSWPFClient.ServiceReference1;
using KDSWPFClient.Lib;
using KDSWPFClient.ViewModel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;


namespace KDSWPFClient
{
    public class AppDataProvider: IDisposable
    {
        private KDSServiceClient _getClient = null;
        private KDSCommandServiceClient _setClient = null;

        // **** СЛОВАРИ
        //   статусов
        private Dictionary<int, OrderStatusViewModel> _ordStatuses;
        public Dictionary<int, OrderStatusViewModel> OrderStatuses { get { return _ordStatuses; } }
        //   отделов
        private Dictionary<int, DepartmentViewModel> _deps;
        public Dictionary<int, DepartmentViewModel> Departments { get { return _deps; } }

        public string ErrorMessage { get; set; }

        public AppDataProvider()
        {
            ErrorMessage = null;
            try
            {
                _getClient = new KDSServiceClient();
                _setClient = new KDSCommandServiceClient();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                return;
            }
            
            _ordStatuses = new Dictionary<int, OrderStatusViewModel>();
            _deps = new Dictionary<int, DepartmentViewModel>();
        }


        #region get dictionaries from service
        public bool SetDictDataFromService()
        {
            bool retVal = false;
            try
            {
                setOrderStatusFromService();

                // получить отделы со службы
                setDepartmentsFromService();
                // прочитать из конфига отделы для отображения и сохранить их в _deps
                string[] cfgDepUIDs = AppLib.GetDepartmentsUID();
                if (cfgDepUIDs != null)
                {
                    DepartmentViewModel curDep;
                    foreach (string uid in cfgDepUIDs)
                    {
//                        curDep = _deps.Values.FirstOrDefault(d => d.UID == uid);
                        curDep = _deps.Values.FirstOrDefault(d => d.UID == uid);
                        if (curDep != null) curDep.IsViewOnKDS = true;
                    }
                }

                // прочие настройки от службы
                bool isIngrIndepend = _getClient.GetIsIngredientsIndependent();
                AppLib.SetAppGlobalValue("IsIngredientsIndependent", isIngrIndepend);

                retVal = true;
            }
            catch (Exception ex)
            {
                ErrorMessage = string.Format("{0}{1}", ex.Message, ((ex.InnerException == null) ? "" : ex.InnerException.Message));
            }

            return retVal;
        }
        private void setOrderStatusFromService()
        {
            _ordStatuses.Clear();
            try
            {
                List<OrderStatusModel> svcList = _getClient.GetOrderStatuses();
                svcList.ForEach((OrderStatusModel o) => _ordStatuses.Add(o.Id,
                    new OrderStatusViewModel() { Id = o.Id, Name = o.Name, UID = o.UID }
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
                List<DepartmentModel> svcDict = _getClient.GetDepartments();
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
                ErrorMessage = string.Format("{0}: {1}", ex.Message, (ex.InnerException == null) ? "" : ex.InnerException.Message);
            }
        }
        #endregion

        public List<OrderModel> GetOrders()
        {
            if (_getClient.State == CommunicationState.Faulted)
            {
                //_getClient.Close();
                _getClient = new KDSServiceClient();
            }

            List<OrderModel> retVal = null;
            try
            {
                retVal = _getClient.GetOrders();
            }
            catch (Exception ex)
            {
                AppLib.WriteLogErrorMessage("Error: " + ex.ToString());
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

        public int GetExpectedTakeValue()
        {
            int retVal=0;

            try
            {
                retVal = _getClient.GetExpectedTakeValue();
            }
            catch (Exception ex)
            {
                AppLib.WriteLogErrorMessage("Error: " + ex.ToString());
            }
            return retVal;
        }

        public void SetExpectedTakeValue(int value)
        {
            try
            {
                _getClient.SetExpectedTakeValue(value);
            }
            catch (Exception ex)
            {
                AppLib.WriteLogErrorMessage("Error: " + ex.ToString());
            }
        }
        #endregion

        public void SetNewOrderStatus(int orderId, OrderStatusEnum newStatus)
        {
            if (_setClient == null) return;

            if (_setClient.State == CommunicationState.Faulted)
            {
                _setClient.Close();
                _setClient = new KDSCommandServiceClient();
            }

            try
            {
                _setClient.ChangeOrderStatus(orderId, newStatus);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void SetNewDishStatus(int orderId, int dishId, OrderStatusEnum newStatus)
        {
            if (_setClient == null) return;

            if (_setClient.State == CommunicationState.Faulted)
            {
                _setClient.Close();
                _setClient = new KDSCommandServiceClient();
            }

            try
            {
                _setClient.ChangeOrderDishStatus(orderId, dishId, newStatus);
            }
            catch (Exception)
            {
                throw;
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
                if (client.State == System.ServiceModel.CommunicationState.Opened) client.Close();
                client = null;
            }
        }
    }  // class
}
