using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace KDSService.AppModel
{
    // служебный класс словарей

    internal static class ServiceDics
    {
        // группы отделов
        private static DepartmentGroups _depGroups;
        internal static DepartmentGroups DepGroups { get { return _depGroups; } }

        // отделы
        private static Departments _deps;
        internal static Departments Departments { get { return _deps; } }

        static ServiceDics()
        {
            _depGroups = new DepartmentGroups();
            _deps = new Departments();
        }

        internal static Department GetDepartmentById(int departmentId)
        {
            return _deps.GetDepartmentById(departmentId);
        }
        internal static DepartmentGroup GetDeGroupById(int id)
        {
            return _depGroups.GetDepGroupById(id);
        }

    }


    // отделы
    internal class Departments
    {
        private Dictionary<int, Department> _deps;
        
        //ctor
        public Departments()
        {
            _deps = new Dictionary<int, Department>();
            UpdateFromDB();

        }

        internal Department GetDepartmentById(int id)
        {
            return (_deps.ContainsKey(id)) ? _deps[id] : null;
        }

        internal void UpdateFromDB()
        {
            using (KDSService.DataSource.DBContext db = new KDSService.DataSource.DBContext())
            {
                if (_deps == null) _deps = new Dictionary<int, Department>();
                else _deps.Clear();

                foreach (KDSService.DataSource.Department dbDep in db.Department)
                {
                    Department dep = new Department()
                    {
                        Id = dbDep.Id,
                        Name = dbDep.Name,
                        IsAutoStart = dbDep.IsAutoStart ?? false,
                        DishQuantity = dbDep.DishQuantity ?? 0,
                    };
                    dep.DepGroups = dbDep.DepartmentDepartmentGroup.Select<KDSService.DataSource.DepartmentDepartmentGroup, DepartmentGroup>(dbGroup => new DepartmentGroup()
                    {
                        Id = dbGroup.DepartmentGroup.Id,
                        Name = dbGroup.DepartmentGroup.Name
                    }).ToList();

                    _deps.Add(dbDep.Id, dep);
                }
            }
        }

        public Department[] GetClientInstance()
        {
            return _deps.Values.ToArray();
        }

    }  // class Departments

    [DataContract]
    public class Department
    {
        [DataMember]
        public int Id { get; set; }

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public bool IsAutoStart { get; set; }

        [DataMember]
        public int DishQuantity { get; set; }

        private List<DepartmentGroup> _depGroups;
        internal List<DepartmentGroup> DepGroups { get; set; }

        [DataMember]
        public DepartmentGroup[] DepGroupsArray {
            get { return _depGroups.ToArray(); }
            set {
                _depGroups.Clear();
                _depGroups.AddRange(value);
            }
        }

        public Department()
        {
        }

    }  // class Department


    // группы отделов
    internal class DepartmentGroups
    {
        private Dictionary<int, DepartmentGroup> _groups;

        // ctor
        public DepartmentGroups()
        {
            _groups = new Dictionary<int, DepartmentGroup>();
            UpdateFromDB();
        }

        internal void UpdateFromDB()
        {
            using (KDSService.DataSource.DBContext db = new KDSService.DataSource.DBContext())
            {
                if (_groups == null) _groups = new Dictionary<int, DepartmentGroup>();
                else _groups.Clear();

                foreach  (DataSource.DepartmentGroup dbGroup in db.DepartmentGroup)
                {
                    _groups.Add(dbGroup.Id, new DepartmentGroup() { Id = dbGroup.Id,Name = dbGroup.Name });
                }
            }
        }

        internal DepartmentGroup GetDepGroupById(int id)
        {
            return (_groups.ContainsKey(id)) ? _groups[id] : null;
        }


        public DepartmentGroup[] GetClientInstance()
        {
            return _groups.Values.ToArray();
        }
    }

    [DataContract]
    public class DepartmentGroup
    {
        [DataMember]
        public int Id { get; set; }

        [DataMember]
        public string Name { get; set; }

    }
}
