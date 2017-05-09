using KDSConsoleClient.ServiceReference1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDSConsoleClient
{
    public class AppDataProvider
    {
        private KDSServiceClient _client = null;

        // **** СЛОВАРИ
        //   групп отделов
        private Dictionary<int, DepartmentGroupModel> _depGroups;
        public Dictionary<int, DepartmentGroupModel> DepartmentGroups { get { return _depGroups; } }
        //   отделов
        private Dictionary<int, DepartmentModel> _deps;
        public Dictionary<int, DepartmentModel> Departments { get { return _deps; } }

        public AppDataProvider(KDSServiceClient client)
        {
            _client = client;

            _depGroups =  _client.GetDepartmentGroups();
            _deps = _client.GetDepartments();
        }

        public List<OrderModel> GetOrders()
        {
            return _client.GetOrders();
        }
    }  // class
}
