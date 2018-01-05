using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDSService.DataSource
{
    public class Order
    {
        public int Id { get; set; }
        public int OrderStatusId { get; set; }
        public int DepartmentId { get; set; }
        public string UID { get; set; }
        public int Number { get; set; }
        public string TableNumber { get; set; }
        public DateTime CreateDate { get; set; }
        public string RoomNumber { get; set; }
        public DateTime StartDate { get; set; }
        public string Waiter { get; set; }
        public int QueueStatusId { get; set; }
        public int LanguageTypeId { get; set; }
        public string DivisionColorRGB { get; set; }

        private List<OrderDish> _dishes;
        public List<OrderDish> Dishes { get { return _dishes; } }

        // CTOR
        public Order()
        {
            _dishes = new List<OrderDish>();
        }

    }  // class
}
