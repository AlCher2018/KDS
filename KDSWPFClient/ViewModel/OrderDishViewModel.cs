using KDSClient.ServiceReference1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDSClient.ViewModel
{
    public class OrderDishViewModel
    {
        public int Id { get; set; }

        public Nullable<int> DishStatusId { get; set; }

        public int DepartmentId { get; set; }

        public string UID { get; set; }

        public string DishName { get; set; }

        public int FilingNumber { get; set; }

        public decimal Quantity { get; set; }

        public int EstimatedTime { get; set; }

        public string Comment { get; set; }

        public System.DateTime CreateDate { get; set; }

        public Nullable<System.DateTime> StartDate { get; set; }

        public string UID1C { get; set; }

        public OrderDishViewModel()
        {
        }
        public OrderDishViewModel(OrderDishModel svcOrderDish)
        {
            //svcOrderDish.Id;
        }

    }  // class
}
