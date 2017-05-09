using KDSClient.Lib;
using KDSClient.ServiceReference1;
using KDSClient.ViewModel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDSClient
{
    public class AppDataProvider: IDisposable
    {
        private KDSServiceClient _client = null;

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

        public AppDataProvider(KDSServiceClient client)
        {
            _client = client;

            _ordStatuses = new Dictionary<int, OrderStatusViewModel>();
            _depGroups = new Dictionary<int, DepartmentGroupViewModel>();
            _deps = new Dictionary<int, DepartmentViewModel>();
        }


        #region set dictionaries from service
        public void SetDictDataFromService()
        {
            setOrderStatusFromService();
            setDepGroupsFromService();
            setDepartmentsFromService();
        }
        private void setOrderStatusFromService()
        {
            _ordStatuses.Clear();
            try
            {
                List<OrderStatusModel> svcList = _client.GetOrderStatusList();
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
                AppLib.WriteLogErrorMessage("{0}: {1}", ex.Message, ex.InnerException.Message);
            }
        }
        private void setDepGroupsFromService()
        {
            _depGroups.Clear();
            try
            {
                Dictionary<int, DepartmentGroupModel> svcDict = _client.GetDepartmentGroups();
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
                AppLib.WriteLogErrorMessage("{0}: {1}", ex.Message, ex.InnerException.Message);
            }
        }
        private void setDepartmentsFromService()
        {
            _deps.Clear();
            try
            {
                Dictionary<int, DepartmentModel> svcDict = _client.GetDepartments();
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
                AppLib.WriteLogErrorMessage("{0}: {1}", ex.Message, ex.InnerException.Message);
            }
        }
        #endregion

        public List<OrderModel> GetOrders()
        {
            return _client.GetOrders();
        }

        public void Dispose()
        {
            if (_client != null)
            {
                _client.Close(); _client = null;
            }
            _ordStatuses.Clear(); _ordStatuses = null;
            _depGroups.Clear(); _depGroups = null;
            _deps.Clear(); _deps = null;
        }
    }  // class
}
