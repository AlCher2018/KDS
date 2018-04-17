using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDSService.DataSource
{
    public class Department
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string UID { get; set; }

        public bool IsAutoStart { get; set; }

        public int DishQuantity { get; set; }
    }
}
