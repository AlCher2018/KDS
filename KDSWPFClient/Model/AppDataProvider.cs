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
        //   групп отделов
        private Dictionary<int, DepartmentGroupViewModel> _depGroups;
        public Dictionary<int, DepartmentGroupViewModel> DepartmentGroups { get { return _depGroups; } }
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
            _depGroups = new Dictionary<int, DepartmentGroupViewModel>();
            _deps = new Dictionary<int, DepartmentViewModel>();
        }


        #region set dictionaries from service
        public bool SetDictDataFromService()
        {
            bool retVal = false;
            try
            {
                setOrderStatusFromService();
                setDepGroupsFromService();
                setDepartmentsFromService();
                retVal = true;
            }
            catch (Exception)
            { }
            return retVal;
        }
        private void setOrderStatusFromService()
        {
            _ordStatuses.Clear();
            try
            {
                List<OrderStatusModel> svcList = _getClient.GetOrderStatusList();
                svcList.ForEach((OrderStatusModel o) => _ordStatuses.Add(o.Id,
                    new OrderStatusViewModel()
                    {
                        Id = o.Id,
                        Name = o.Name,
                        UID = o.UID
                    }));
            }
            catch (Exception ex)
            {
                ErrorMessage = string.Format("{0}{1}", ex.Message, (ex.InnerException==null) ? "" : ex.InnerException.Message);
                throw;
            }
        }
        private void setDepGroupsFromService()
        {
            _depGroups.Clear();
            try
            {
                Dictionary<int, DepartmentGroupModel> svcDict = _getClient.GetDepartmentGroups();
                foreach (KeyValuePair<int, DepartmentGroupModel> kvp in svcDict)
                {
                    _depGroups.Add(kvp.Key, new DepartmentGroupViewModel()
                    {
                        Id = kvp.Value.Id, Name = kvp.Value.Name
                    });
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = string.Format("{0}{1}", ex.Message, (ex.InnerException==null) ? "" : ex.InnerException.Message);
                throw;
            }
        }
        private void setDepartmentsFromService()
        {
            _deps.Clear();
            try
            {
                Dictionary<int, DepartmentModel> svcDict = _getClient.GetDepartments();
                foreach (KeyValuePair<int, DepartmentModel> kvp in svcDict)
                {
                    DepartmentViewModel newDep = new DepartmentViewModel()
                    {
                        Id = kvp.Value.Id,
                        Name = kvp.Value.Name,
                        UID = kvp.Value.UID,
                        DishQuantity = kvp.Value.DishQuantity,
                        IsAutoStart = kvp.Value.IsAutoStart
                    };
                    newDep.setDepGroupsByIdList(kvp.Value.DepGroupsIdList, _depGroups);

                    _deps.Add(kvp.Key, newDep);
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
            return _getClient.GetOrders();
        }

        public void Dispose()
        {
            disposeServiceClient(_getClient);
            disposeServiceClient(_setClient);

            if (_ordStatuses != null) { _ordStatuses.Clear(); _ordStatuses = null; }
            if (_depGroups != null) { _depGroups.Clear(); _depGroups = null; }
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
