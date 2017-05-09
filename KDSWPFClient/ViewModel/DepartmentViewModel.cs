using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDSClient.ViewModel
{
    public class DepartmentViewModel
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string UID { get; set; }

        public bool IsAutoStart { get; set; }

        public int DishQuantity { get; set; }

        private List<DepartmentGroupViewModel> _depGroups;
        public List<DepartmentGroupViewModel> DepartmentGroups { get { return _depGroups; } }

        public DepartmentViewModel()
        {
            _depGroups = new List<DepartmentGroupViewModel>();
        }

        public void setDepGroupsByIdList(List<int> idGroupList, Dictionary<int, DepartmentGroupViewModel> groups)
        {
            _depGroups.Clear();
            foreach (int groupId in idGroupList)
            {
                if (groups.ContainsKey(groupId)) _depGroups.Add(groups[groupId]);
            }
        }

    }  // class DepartmentViewModel
}
