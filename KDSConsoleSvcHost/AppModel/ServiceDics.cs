using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using KDSConsoleSvcHost;


namespace KDSService.AppModel
{
    // служебный класс словарей
    // используется как для службы, так и для клиента
    // Для испльзования в службе классы моделей должны получать данные из БД,
    // а при использовании на стороне клиента данные получаются в соотв.свойствах из десериализации

    internal static class ServiceDics
    {
        // группы отделов
        private static DepartmentGroupsModel _depGroups;
        internal static DepartmentGroupsModel DepGroups { get { return _depGroups; } }

        // отделы
        private static DepartmentsModel _deps;
        internal static DepartmentsModel Departments { get { return _deps; } }

        static ServiceDics()
        {
            _depGroups = new DepartmentGroupsModel();
            _deps = new DepartmentsModel();
        }

        public static List<OrderStatusModel> GetOrderStatusList(out string errMsg)
        {
            List<OrderStatusModel> retVal = null;
            errMsg = null;
            try
            {
                using (KDSEntities db = new KDSEntities())
                {
                    retVal = db.OrderStatus
                        .Select(os => new OrderStatusModel() {Id = os.Id, Name = os.Name, UID = os.UID})
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                errMsg = string.Format("{0}: {1}", ex.Message, ex.InnerException.Message);
            }
            return retVal;
        }  // method

    }  // class ServiceDics



    // статусы заказа/блюда
    [DataContract]
    public class OrderStatusModel
    {
        [DataMember]
        public int Id { get; set; }

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public string UID { get; set; }
    }  // class


    // отделы
    internal class DepartmentsModel
    {
        private Dictionary<int, DepartmentModel> _deps;

        //ctor
        public DepartmentsModel()
        {
            _deps = new Dictionary<int, DepartmentModel>();
            UpdateFromDB();
        }

        internal DepartmentModel GetDepartmentById(int id)
        {
            return (_deps.ContainsKey(id)) ? _deps[id] : null;
        }
        public Dictionary<int, DepartmentModel> GetDictionary()
        {
            return _deps;
        }

        internal string UpdateFromDB()
        {
            try
            {
                using (KDSEntities db = new KDSEntities())
                {
                    if (_deps == null) _deps = new Dictionary<int, DepartmentModel>();
                    else _deps.Clear();

                    foreach (Department dbDep in db.Department)
                    {
                        DepartmentModel dep = new DepartmentModel(dbDep);
                        _deps.Add(dbDep.Id, dep);
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                return string.Format("{0}: {1}", ex.Message, ex.InnerException.Message);
            }
        }  // method

    }  // class Departments

    [DataContract]
    public class DepartmentModel
    {
        [DataMember]
        public int Id { get; set; }

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public string UID { get; set; }

        [DataMember]
        public bool IsAutoStart { get; set; }

        [DataMember]
        public decimal DishQuantity { get; set; }

        private List<DepartmentGroupModel> _depGroups;
        internal List<DepartmentGroupModel> DepGroups
        {
            get { return _depGroups; }
            set { _depGroups = value; }
        }

        // для передачи клиенту списка Ид групп отделов
        [DataMember]
        public List<int> DepGroupsIdList
        {
            get {
                List<int> retVal = _depGroups.Select(dg => dg.Id).ToList();
                return _depGroups.Select(dg => dg.Id).ToList();
            }
            set { }
        }

        public DepartmentModel(Department dbDep)
        {
            Id = dbDep.Id; Name = dbDep.Name; UID = dbDep.UID;
            IsAutoStart = dbDep.IsAutoStart; DishQuantity = dbDep.DishQuantity;

            _depGroups = new List<DepartmentGroupModel>();
            _depGroups = dbDep.DepartmentDepartmentGroup.Select<DepartmentDepartmentGroup, DepartmentGroupModel>(dbGroup => new DepartmentGroupModel()
            {
                Id = dbGroup.DepartmentGroup.Id,
                Name = dbGroup.DepartmentGroup.Name
            }).ToList();
        }

    }  // class Department


    // группы отделов
    internal class DepartmentGroupsModel
    {
        private Dictionary<int, DepartmentGroupModel> _groups;

        // ctor
        internal DepartmentGroupsModel()
        {
            _groups = new Dictionary<int, DepartmentGroupModel>();
        }

        internal Dictionary<int, DepartmentGroupModel> GetDictionary()
        {
            return _groups;
        }

        // для сервиса
        internal string UpdateFromDB()
        {
            try
            {
                using (KDSEntities db = new KDSEntities())
                {
                    if (_groups == null) _groups = new Dictionary<int, DepartmentGroupModel>();
                    else _groups.Clear();

                    foreach (DepartmentGroup dbGroup in db.DepartmentGroup)
                    {
                        _groups.Add(dbGroup.Id, new DepartmentGroupModel() { Id = dbGroup.Id, Name = dbGroup.Name });
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                return string.Format("{0}: {1}", ex.Message, ex.InnerException.Message);
            }
        }

        internal DepartmentGroupModel GetDepGroupById(int id)
        {
            return (_groups.ContainsKey(id)) ? _groups[id] : null;
        }

    }

    [DataContract]
    public class DepartmentGroupModel
    {
        [DataMember]
        public int Id { get; set; }

        [DataMember]
        public string Name { get; set; }
    }  // class

}
