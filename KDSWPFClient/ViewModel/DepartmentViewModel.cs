using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDSWPFClient.ViewModel
{
    public class DepartmentViewModel
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string UID { get; set; }

        public bool IsAutoStart { get; set; }

        public decimal DishQuantity { get; set; }

        public bool IsViewOnKDS { get; set; }

    }  // class DepartmentViewModel
}
