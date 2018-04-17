using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDSService.DataSource
{
    public class OrderDish
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int DishStatusId { get; set; }
        public int DepartmentId { get; set; }
        public string UID { get; set; }
        public string DishName { get; set; }
        public int FilingNumber { get; set; }
        public decimal Quantity { get; set; }
        public string ParentUid { get; set; }
        public int EstimatedTime { get; set; }
        public string Comment { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime StartDate { get; set; }
        public string UID1C { get; set; }
        public int DelayedStartTime { get; set; }
    }
}
