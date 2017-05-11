﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace KDSService.AppModel
{
    // служебный класс словарей
    // используется как для службы, так и для клиента
    // Для испльзования в службе классы моделей должны получать данные из БД,
    // а при использовании на стороне клиента данные получаются в соотв.свойствах из десериализации

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

    }


    // отделы
    internal class Departments
    {
        private Dictionary<int, Department> _deps;

        //ctor
        public Departments()
        {
            _deps = new Dictionary<int, Department>();
        }

        internal Department GetDepartmentById(int id)
        {
            return (_deps.ContainsKey(id)) ? _deps[id] : null;
        }
        public Dictionary<int, Department> GetDictionary()
        {
            return _deps;
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

        // для передачи клиенту списка Ид групп отделов
        [DataMember]
        public List<int> DepGroupsIdList
        {
            get { return _depGroups.Select(dg => dg.Id).ToList(); }
            set { }
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
        internal DepartmentGroups()
        {
            _groups = new Dictionary<int, DepartmentGroup>();
        }

        internal Dictionary<int, DepartmentGroup> GetDictionary()
        {
            return _groups;
        }

        // для сервиса
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

    }

    // клиенту не передается
    // через ссылку на проект использовать у клиента dll
    public class DepartmentGroup
    {
        public int Id { get; set; }

        public string Name { get; set; }
    }
}