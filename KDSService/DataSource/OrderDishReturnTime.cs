using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDSService.DataSource
{
    public class OrderDishReturnTime
    {
        public int Id { get; set; }
        public int OrderDishId { get; set; }
        public DateTime ReturnDate { get; set; }
        public int StatusFrom { get; set; }
        public int StatusFromTimeSpan { get; set; }
        public int StatusTo { get; set; }
    }
}
